using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Replenishment;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;
using static PEMS.Tests.Inventory.ReplenishmentTestKit;

namespace PEMS.Tests.Inventory;

/// <summary>یک دیتابیس یک‌بارمصرف (PEMS_TEST_REPL_*) با Migrationهای واقعی برای همه‌ی تست‌های SQL توصیه‌ی تأمین؛ هرگز PEMSDB / PEMSDB_PcatClone.</summary>
public sealed class ReplenishmentSqlFixture : IAsyncLifetime
{
    public string DbName { get; } = "PEMS_TEST_REPL_" + Guid.NewGuid().ToString("N")[..12];
    public string Connection { get; private set; } = "";

    public async Task InitializeAsync()
    {
        Connection = new SqlConnectionStringBuilder(PemsDbContextFactory.ConnectionString) { InitialCatalog = DbName }.ConnectionString;
        await using var ctx = New();
        await ctx.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        SqlConnection.ClearAllPools();
        var master = new SqlConnectionStringBuilder(Connection) { InitialCatalog = "master" }.ConnectionString;
        await using var c = new SqlConnection(master);
        await c.OpenAsync();
        await using var cmd = c.CreateCommand();
        cmd.CommandText = $"IF DB_ID('{DbName}') IS NOT NULL BEGIN ALTER DATABASE [{DbName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{DbName}]; END";
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>همان پیکربندی برنامه (EnableRetryOnFailure).</summary>
    public PEMSDbContext New() => new(new DbContextOptionsBuilder<PEMSDbContext>().UseSqlServer(Connection, o => o.EnableRetryOnFailure()).Options);
}

public class ReplenishmentSqlTests : IClassFixture<ReplenishmentSqlFixture>
{
    private readonly ReplenishmentSqlFixture _fx;
    public ReplenishmentSqlTests(ReplenishmentSqlFixture fx) => _fx = fx;

    private async Task<(int Item, int Wh)> SeedKeysAsync()
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        await using var ctx = _fx.New();
        var wh = new Warehouse { Code = "W" + tag, Name = "W " + tag, IsActive = true };
        var item = new MaterialItem { MaterialCode = "M" + tag, Description = "item " + tag, IsActive = true, IsStockItem = true };
        ctx.AddRange(wh, item);
        await ctx.SaveChangesAsync();
        return (item.MaterialItemId, wh.WarehouseId);
    }

    private ReplenishmentRecommendationService NewService(PEMSDbContext ctx, FakeNetDemand demand) => new(ctx, demand, StandardPerms());

    // ───────── Schema ─────────

    [Fact]
    public async Task Migration_CreatesTheTablesTheLiveIndexAndTheOneSourceCheckConstraint()
    {
        await using var ctx = _fx.New();

        var tables = await ctx.Database.SqlQueryRaw<string>("SELECT name AS Value FROM sys.tables WHERE name IN ('NetDemandRuns','ReplenishmentRecommendations','ReplenishmentRecommendationSources','ReplenishmentRecommendationHistories')").ToListAsync();
        var live = await ctx.Database.SqlQueryRaw<string>("SELECT filter_definition AS Value FROM sys.indexes WHERE name = 'UX_ReplenishmentRecommendations_Live' AND is_unique = 1").ToListAsync();
        var check = await ctx.Database.SqlQueryRaw<string>("SELECT name AS Value FROM sys.check_constraints WHERE name = 'CK_ReplenishmentRecommendationSources_OneSource'").ToListAsync();

        Assert.Equal(4, tables.Count);
        Assert.Contains("Open", Assert.Single(live));
        Assert.Single(check);
    }

    [Fact]
    public async Task OnlyOneLiveRecommendationPerKey_ButAClosedOneDoesNotBlockANewOne()
    {
        var (item, wh) = await SeedKeysAsync();
        await using var ctx = _fx.New();
        var run = new NetDemandRun { Status = "Completed", CalculatorVersion = 1 };
        ctx.NetDemandRuns.Add(run);
        await ctx.SaveChangesAsync();

        ReplenishmentRecommendation New(string status) => new()
        {
            MaterialItemId = item, WarehouseId = wh, Status = status, FirstRunId = run.NetDemandRunId, LastRunId = run.NetDemandRunId, InputFingerprint = new string('a', 64)
        };

        ctx.ReplenishmentRecommendations.Add(New("Open"));
        await ctx.SaveChangesAsync();

        ctx.ReplenishmentRecommendations.Add(New("Open"));
        var dup = await Assert.ThrowsAsync<DbUpdateException>(() => ctx.SaveChangesAsync());
        Assert.True(StockRowResolver.IsUniqueViolation(dup));
        ctx.ChangeTracker.Clear();

        var first = await ctx.ReplenishmentRecommendations.SingleAsync(r => r.MaterialItemId == item && r.WarehouseId == wh);
        first.Status = "Dismissed";
        await ctx.SaveChangesAsync();
        ctx.ReplenishmentRecommendations.Add(New("Open"));                        // Dismissed هنوز «زنده» است
        await Assert.ThrowsAsync<DbUpdateException>(() => ctx.SaveChangesAsync());
        ctx.ChangeTracker.Clear();

        var again = await ctx.ReplenishmentRecommendations.SingleAsync(r => r.MaterialItemId == item && r.WarehouseId == wh);
        again.Status = "Closed";
        await ctx.SaveChangesAsync();
        ctx.ReplenishmentRecommendations.Add(New("Open"));                        // بعد از Closed مجاز است
        await ctx.SaveChangesAsync();
        Assert.Equal(2, await ctx.ReplenishmentRecommendations.CountAsync(r => r.MaterialItemId == item && r.WarehouseId == wh));
    }

    [Fact]
    public async Task SourceRow_MustReferenceExactlyOneDemandLine()
    {
        var (item, wh) = await SeedKeysAsync();
        var demand = new FakeNetDemand { Results = new() { Result(item, wh, Stock(item, wh, 1), SrLine(500, item, wh, 4)) } };
        await using var ctx = _fx.New();
        Assert.True((await NewService(ctx, demand).RunAsync(new ReplenishmentRunRequest(new[] { item }, null, null), Full)).Success);
        var recId = (await ctx.ReplenishmentRecommendations.AsNoTracking().SingleAsync(r => r.MaterialItemId == item)).ReplenishmentRecommendationId;

        ctx.ReplenishmentRecommendationSources.Add(new ReplenishmentRecommendationSource { ReplenishmentRecommendationId = recId, Revision = 9, SourceType = 1 });                                         // بدون شناسه
        await Assert.ThrowsAsync<DbUpdateException>(() => ctx.SaveChangesAsync());
        ctx.ChangeTracker.Clear();
        ctx.ReplenishmentRecommendationSources.Add(new ReplenishmentRecommendationSource { ReplenishmentRecommendationId = recId, Revision = 9, SourceType = 1, StoreRequisitionLineId = 1, WorkOrderMaterialId = 2 });   // هر دو
        await Assert.ThrowsAsync<DbUpdateException>(() => ctx.SaveChangesAsync());
    }

    // ───────── هم‌زمانی / Idempotency ─────────

    [Fact]
    public async Task ConcurrentRunsOverTheSameDemand_NeverCreateMoreThanOneLiveRecommendation()
    {
        var (item, wh) = await SeedKeysAsync();
        var demand = new FakeNetDemand { Results = new() { Result(item, wh, Stock(item, wh, 3), SrLine(600, item, wh, 8)) } };

        async Task<ReplenishmentRunResult> One()
        {
            await using var ctx = _fx.New();
            return await NewService(ctx, demand).RunAsync(new ReplenishmentRunRequest(new[] { item }, null, null), Full);
        }

        var results = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Task.Run(One)));

        await using var verify = _fx.New();
        var recs = await verify.ReplenishmentRecommendations.AsNoTracking().Where(r => r.MaterialItemId == item && r.WarehouseId == wh).ToListAsync();
        Assert.Single(recs);
        Assert.Equal("Open", recs[0].Status);
        Assert.Contains(results, r => r.Success && r.Created == 1);                // دقیقاً یک اجرا آن را ساخت
        Assert.Equal(1, results.Sum(r => r.Success ? r.Created : 0));
        // اجراهای ناموفق (فقط پس از سقف ۳ تلاش) صریحاً Failed ثبت می‌شوند، نه بلعیده
        Assert.All(results.Where(r => !r.Success), r => Assert.False(string.IsNullOrWhiteSpace(r.Error)));
        Assert.Equal(1, await verify.ReplenishmentRecommendationSources.CountAsync(s => s.ReplenishmentRecommendationId == recs[0].ReplenishmentRecommendationId));
        Assert.Equal(1, await verify.ReplenishmentRecommendationHistories.CountAsync(h => h.ReplenishmentRecommendationId == recs[0].ReplenishmentRecommendationId && h.Action == "Created"));
    }

    [Fact]
    public async Task ConcurrentRunsWithTheSameRequestKey_ExecuteOnce()
    {
        var (item, wh) = await SeedKeysAsync();
        var key = "key-" + Guid.NewGuid().ToString("N");
        var demand = new FakeNetDemand { Results = new() { Result(item, wh, Stock(item, wh, 3), SrLine(700, item, wh, 8)) } };

        async Task<ReplenishmentRunResult> One()
        {
            await using var ctx = _fx.New();
            return await NewService(ctx, demand).RunAsync(new ReplenishmentRunRequest(new[] { item }, null, key), Full);
        }

        var results = await Task.WhenAll(Task.Run(One), Task.Run(One), Task.Run(One));

        await using var verify = _fx.New();
        Assert.Equal(1, await verify.NetDemandRuns.CountAsync(r => r.RequestKey == key));
        Assert.Equal(1, await verify.ReplenishmentRecommendations.CountAsync(r => r.MaterialItemId == item));
        Assert.Equal(1, results.Count(r => !r.Replayed));                          // فقط یک اجرای واقعی
        Assert.Equal(1, demand.Calls);
    }

    [Fact]
    public async Task StaleRowVersion_RefusesDismissAndReopen_AfterTheRecommendationChanged()
    {
        var (item, wh) = await SeedKeysAsync();
        var demand = new FakeNetDemand { Results = new() { Result(item, wh, Stock(item, wh, 3), SrLine(800, item, wh, 8)) } };
        int id;
        byte[] staleVersion;
        await using (var c1 = _fx.New())
        {
            var svc = NewService(c1, demand);
            await svc.RunAsync(new ReplenishmentRunRequest(new[] { item }, null, null), Full);
            id = (await c1.ReplenishmentRecommendations.AsNoTracking().SingleAsync(r => r.MaterialItemId == item)).ReplenishmentRecommendationId;
            staleVersion = (await svc.GetDetailsAsync(id, Full)).Data!.RowVersion!;
        }

        // بعد از خواندن نسخه، محاسبه‌ی دیگری ورودی را تغییر مادی می‌دهد ⇒ RowVersion عوض می‌شود
        demand.Results = new() { Result(item, wh, Stock(item, wh, 3), SrLine(800, item, wh, 9)) };
        await using (var c2 = _fx.New()) await NewService(c2, demand).RunAsync(new ReplenishmentRunRequest(new[] { item }, null, null), Full);

        await using var c3 = _fx.New();
        var svc3 = NewService(c3, demand);
        var stale = await svc3.DismissAsync(id, staleVersion, "late", Full);
        Assert.False(stale.Success);
        Assert.True(stale.Conflict);
        Assert.Equal("Open", (await c3.ReplenishmentRecommendations.AsNoTracking().SingleAsync(r => r.ReplenishmentRecommendationId == id)).Status);

        var fresh = (await svc3.GetDetailsAsync(id, Full)).Data!.RowVersion!;
        Assert.True((await svc3.DismissAsync(id, fresh, "ok", Full)).Success);
        var second = await svc3.DismissAsync(id, fresh, "again", Full);            // نسخه‌ی کهنه‌ی بعد از Dismiss
        Assert.False(second.Success);
    }
}

/// <summary>Migration ReplenishmentRecommendations: Up → Down → Up روی دیتابیس یک‌بارمصرف جدا؛ Down فقط جدول‌های P5 را برمی‌دارد و داده‌ی دیگر را لمس نمی‌کند.</summary>
public class ReplenishmentMigrationTests : IAsyncLifetime
{
    private const string Previous = "20261010141504_MaterialStockNoLocationUniqueness";
    private readonly string _dbName = "PEMS_TEST_REPLMIG_" + Guid.NewGuid().ToString("N")[..12];
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

    private PEMSDbContext New() => new(new DbContextOptionsBuilder<PEMSDbContext>().UseSqlServer(_connection).Options);

    private static async Task<int> CountTablesAsync(PEMSDbContext ctx) =>
        (await ctx.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM sys.tables WHERE name IN ('NetDemandRuns','ReplenishmentRecommendations','ReplenishmentRecommendationSources','ReplenishmentRecommendationHistories')").ToListAsync()).Single();

    [Fact]
    public async Task UpDownUp_AddsAndRemovesOnlyTheFourP5Tables()
    {
        await using var ctx = New();
        var migrator = Microsoft.EntityFrameworkCore.Infrastructure.AccessorExtensions.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrator>(ctx);

        await migrator.MigrateAsync(Previous);
        Assert.Equal(0, await CountTablesAsync(ctx));
        var stocksIndexBefore = await ctx.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM sys.indexes WHERE name = 'UX_MaterialStocks_Item_Warehouse_NoLocation'").SingleAsync();

        await migrator.MigrateAsync();
        Assert.Equal(4, await CountTablesAsync(ctx));

        await migrator.MigrateAsync(Previous);
        Assert.Equal(0, await CountTablesAsync(ctx));
        Assert.Equal(stocksIndexBefore, await ctx.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM sys.indexes WHERE name = 'UX_MaterialStocks_Item_Warehouse_NoLocation'").SingleAsync());

        await migrator.MigrateAsync();
        Assert.Equal(4, await CountTablesAsync(ctx));
    }
}
