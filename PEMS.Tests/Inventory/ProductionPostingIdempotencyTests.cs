using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Inventory;

/// <summary>
/// Phase 2 / P4 (Audit A) - ثبت خروجی/ورودی تولید در موجودی باید برای «همان سند» دقیقاً یک‌بار انجام شود، حتی با دو کلیک هم‌زمان
/// (PostWithRetry تعارض ردیف موجودی را حل می‌کند، پس نگهبان باید روی سند باشد: PostingScope) و حتی پس از شکست بین دو ذخیره.
/// </summary>
public class ProductionPostingIdempotencyTests : IAsyncLifetime
{
    private sealed class Numbering : INumberingService
    {
        private int _n;
        public Task<string> GetNextNumberAsync(string documentType, string? scopeKey = null) => Task.FromResult($"{documentType}-{Interlocked.Increment(ref _n):0000}");
    }

    private readonly string _dbName = "PEMS_TEST_PRODPOST_" + Guid.NewGuid().ToString("N")[..12];
    private string _connection = "";

    public Task InitializeAsync()
    {
        _connection = new SqlConnectionStringBuilder(PemsDbContextFactory.ConnectionString) { InitialCatalog = _dbName }.ConnectionString;
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        SqlConnection.ClearAllPools();
        var master = new SqlConnectionStringBuilder(_connection) { InitialCatalog = "master" }.ConnectionString;
        await using var c = new SqlConnection(master);
        await c.OpenAsync();
        await using var cmd = c.CreateCommand();
        cmd.CommandText = $"IF DB_ID('{_dbName}') IS NOT NULL BEGIN ALTER DATABASE [{_dbName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{_dbName}]; END";
        await cmd.ExecuteNonQueryAsync();
    }

    // همان پیکربندی برنامه (Program.cs: EnableRetryOnFailure) - تراکنش کاربر فقط داخل Execution Strategy مجاز است؛ تست باید همین را بسنجد
    private PEMSDbContext NewSql() => new(new DbContextOptionsBuilder<PEMSDbContext>().UseSqlServer(_connection, o => o.EnableRetryOnFailure()).Options);

    private static ProductionRunService Svc(PEMSDbContext ctx)
    {
        var numbering = new Numbering();
        return new ProductionRunService(ctx, numbering, new MaterialStockService(ctx), new ProductionOrderService(ctx, numbering));
    }

    /// <summary>خروجی Released شده‌ی ۲۵ واحدی؛ خروجی: (OutputId، WarehouseId، MaterialItemId).</summary>
    private static async Task<(int OutputId, int WarehouseId, int MaterialId)> SeedReleasedOutputAsync(PEMSDbContext ctx)
    {
        var tag = Guid.NewGuid().ToString("N")[..6];
        var fl = new FunctionalLocation { Code = "FL" + tag, Name = "Area " + tag, IsActive = true };
        var material = new MaterialItem { MaterialCode = "M" + tag, Description = "Prod " + tag, IsActive = true };
        var warehouse = new Warehouse { Code = "W" + tag, Name = "WH " + tag, IsActive = true };
        ctx.AddRange(fl, material, warehouse);
        await ctx.SaveChangesAsync();
        var wc = new ProductionWorkCenter { Code = "WC" + tag, Name = "Line " + tag, FunctionalLocationId = fl.FunctionalLocationId, IsActive = true };
        ctx.ProductionWorkCenters.Add(wc);
        await ctx.SaveChangesAsync();

        var svc = Svc(ctx);
        var orderService = new ProductionOrderService(ctx, new Numbering());
        var (_, _, orderId) = await orderService.CreateAsync(material.MaterialItemId, 100m, null, wc.ProductionWorkCenterId, null, null, null, null, null, null);
        await orderService.PlanAsync(orderId!.Value, null);
        await orderService.ReleaseAsync(orderId.Value, null);
        var (_, _, runId) = await svc.CreateAsync(orderId.Value, wc.ProductionWorkCenterId, null, null, null, null, null);
        await svc.StartAsync(runId!.Value, null);
        var (_, _, batchId) = await svc.RecordOutputAsync(runId.Value, material.MaterialItemId, null, 25m, null, "Primary", null, null);
        await svc.SubmitBatchForQcAsync(batchId!.Value, null);
        await svc.ReleaseBatchAsync(batchId.Value, null);
        var run = await svc.GetByIdAsync(runId.Value);
        return (run!.Outputs.Single().ProductionOutputId, warehouse.WarehouseId, material.MaterialItemId);
    }

    [Fact]
    public async Task ConcurrentPostsOfTheSameOutput_PostExactlyOnce_OnRealSqlServer()
    {
        int outputId, wh, material;
        await using (var seed = NewSql())
        {
            await seed.Database.MigrateAsync();
            (outputId, wh, material) = await SeedReleasedOutputAsync(seed);
        }

        async Task<(bool Success, string? Error)> Post()
        {
            await using var ctx = NewSql();
            return await Svc(ctx).PostOutputToInventoryAsync(outputId, wh, null, null);
        }

        var results = await Task.WhenAll(Task.Run(Post), Task.Run(Post), Task.Run(Post));

        Assert.Equal(1, results.Count(r => r.Success));
        Assert.All(results.Where(r => !r.Success), r => Assert.Contains("قبلاً", r.Error));
        await using var verify = NewSql();
        Assert.Equal(25m, (await verify.MaterialStocks.SingleAsync(s => s.MaterialItemId == material && s.WarehouseId == wh)).OnHandQuantity);
        Assert.Equal(1, await verify.InventoryTransactions.CountAsync(t => t.ReferenceType == "ProductionOutput" && t.ReferenceId == outputId));
        Assert.NotNull((await verify.ProductionOutputs.SingleAsync(o => o.ProductionOutputId == outputId)).InventoryTransactionId);
    }

    [Fact]
    public async Task PostOutput_WhenAnEarlierPostingLeftNoLink_LinksTheLedgerRow_AndNeverPostsAgain()
    {
        var ctx = PemsDbContextFactory.CreateInMemory("p4prod_" + Guid.NewGuid().ToString("N"));
        var (outputId, wh, material) = await SeedReleasedOutputAsync(ctx);
        var svc = Svc(ctx);

        // شبیه‌سازی شکست بین دو ذخیره: موجودی و تراکنش ثبت شده ولی لینک خروجی ذخیره نشده
        var stock = new MaterialStockService(ctx);
        Assert.True((await stock.ReceiveAsync(material, wh, null, 25m, "first", null, "ProductionOutput", outputId)).Success);
        Assert.Null((await ctx.ProductionOutputs.SingleAsync()).InventoryTransactionId);

        var result = await svc.PostOutputToInventoryAsync(outputId, wh, null, null);

        Assert.False(result.Success);
        Assert.Contains("قبلاً", result.Error);
        Assert.Equal(25m, (await ctx.MaterialStocks.SingleAsync()).OnHandQuantity);                                   // دوباره اضافه نشد
        Assert.Equal(1, await ctx.InventoryTransactions.CountAsync(t => t.ReferenceType == "ProductionOutput"));
        Assert.NotNull((await ctx.ProductionOutputs.SingleAsync()).InventoryTransactionId);                           // لینک ترمیم شد

        var again = await svc.PostOutputToInventoryAsync(outputId, wh, null, null);
        Assert.False(again.Success);
        Assert.Equal(25m, (await ctx.MaterialStocks.SingleAsync()).OnHandQuantity);
    }
}
