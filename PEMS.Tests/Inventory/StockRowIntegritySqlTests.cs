using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Inventory;

/// <summary>
/// Phase 2 / P4 - اثبات روی SQL Server واقعیِ یک‌بارمصرف (PEMS_TEST_STOCKROW_*، هرگز PEMSDB): ایندکس یکتای NULL-location،
/// ساخت هم‌زمان ردیف اول، تعارض RowVersion با تلاش مجدد محدود، و Migration (Pre-flight / ساخت ایندکس / Down).
/// </summary>
public class StockRowIntegritySqlTests : IAsyncLifetime
{
    private const string PreviousMigration = "20261008075704_OrganizationAssignmentEmployeeIndex";
    private const string IndexName = "UX_MaterialStocks_Item_Warehouse_NoLocation";

    private readonly string _dbName = "PEMS_TEST_STOCKROW_" + Guid.NewGuid().ToString("N")[..12];
    private string _connection = "";

    /// <summary>یک‌بار، درست قبل از اولین SaveChanges، Callback را اجرا می‌کند (برای شبیه‌سازی رقیب هم‌زمان به‌صورت قطعی).</summary>
    private sealed class OnceBeforeSave : SaveChangesInterceptor
    {
        private Func<Task>? _hook;
        public int SaveCalls;
        public List<string> Log = new();
        public int PostingAttempts => Log.Count(l => l.Contains("InventoryTransaction:Added"));
        public OnceBeforeSave(Func<Task> hook) => _hook = hook;
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken ct = default)
        {
            SaveCalls++;
            Log.Add(string.Join(",", eventData.Context!.ChangeTracker.Entries().Where(e => e.State != EntityState.Unchanged).Select(e => e.Entity.GetType().Name + ":" + e.State)));
            var hook = _hook; _hook = null;
            if (hook != null) await hook();
            return await base.SavingChangesAsync(eventData, result, ct);
        }
    }

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

    private PEMSDbContext NewCtx(params IInterceptor[] interceptors)
    {
        var b = new DbContextOptionsBuilder<PEMSDbContext>().UseSqlServer(_connection);
        if (interceptors.Length > 0) b.AddInterceptors(interceptors);
        return new PEMSDbContext(b.Options);
    }

    private async Task<PEMSDbContext> MigratedAsync()
    {
        var ctx = NewCtx();
        await ctx.Database.MigrateAsync();
        return ctx;
    }

    private static async Task<(int Item, int Wh)> SeedKeysAsync(PEMSDbContext ctx)
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        var wh = new Warehouse { Code = "W" + tag, Name = "W " + tag, IsActive = true };
        var item = new MaterialItem { MaterialCode = "M" + tag, Description = "item " + tag, IsActive = true, IsStockItem = true };
        ctx.AddRange(wh, item);
        await ctx.SaveChangesAsync();
        return (item.MaterialItemId, wh.WarehouseId);
    }

    private async Task<bool> IndexExistsAsync()
    {
        await using var ctx = NewCtx();
        var n = await ctx.Database.SqlQueryRaw<int>($"SELECT COUNT(*) AS Value FROM sys.indexes WHERE name = '{IndexName}' AND object_id = OBJECT_ID('MaterialStocks')").SingleAsync();
        return n == 1;
    }

    // ───────── ایندکس ─────────

    [Fact]
    public async Task Index_RejectsADuplicateNullLocationRow_ButAllowsDistinctLocationsAndWarehouses()
    {
        await using var ctx = await MigratedAsync();
        var (item, wh) = await SeedKeysAsync(ctx);
        var (_, wh2) = await SeedKeysAsync(ctx);
        var loc = new StorageLocation { WarehouseId = wh, Code = "L1", Name = "L1", IsActive = true };
        ctx.StorageLocations.Add(loc);
        await ctx.SaveChangesAsync();

        ctx.MaterialStocks.Add(new MaterialStock { MaterialItemId = item, WarehouseId = wh, IsActive = true });
        await ctx.SaveChangesAsync();
        ctx.MaterialStocks.Add(new MaterialStock { MaterialItemId = item, WarehouseId = wh, StorageLocationId = loc.StorageLocationId, IsActive = true });   // محل متفاوت: مجاز
        ctx.MaterialStocks.Add(new MaterialStock { MaterialItemId = item, WarehouseId = wh2, IsActive = true });                                             // انبار متفاوت: مجاز
        await ctx.SaveChangesAsync();

        ctx.MaterialStocks.Add(new MaterialStock { MaterialItemId = item, WarehouseId = wh, IsActive = true });                                              // تکراری NULL-location
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => ctx.SaveChangesAsync());
        Assert.True(StockRowResolver.IsUniqueViolation(ex));
    }

    // ───────── ساخت هم‌زمان ردیف اول ─────────

    [Fact]
    public async Task FirstCreation_WhenAConcurrentCreatorWinsTheRace_ReReadsTheWinner_NoDuplicateRow()
    {
        int item, wh;
        await using (var seed = await MigratedAsync()) (item, wh) = await SeedKeysAsync(seed);

        var interceptor = new OnceBeforeSave(async () =>
        {
            await using var rival = NewCtx();
            rival.MaterialStocks.Add(new MaterialStock { MaterialItemId = item, WarehouseId = wh, OnHandQuantity = 5, IsActive = true });
            await rival.SaveChangesAsync();
        });

        await using var ctx = NewCtx(interceptor);
        var (stock, error) = await StockRowResolver.GetOrCreateAsync(ctx, item, wh, null);

        Assert.Null(error);
        Assert.Equal(5m, stock!.OnHandQuantity);                       // ردیف برنده، نه ردیف تازه‌ساخته‌ی ناموفق
        await using var verify = NewCtx();
        Assert.Equal(1, await verify.MaterialStocks.CountAsync(x => x.MaterialItemId == item && x.WarehouseId == wh));
    }

    [Fact]
    public async Task Receive_WhenAConcurrentCreatorWinsTheRace_AddsOnTopOfTheWinner_OneRowOneTransaction()
    {
        int item, wh;
        await using (var seed = await MigratedAsync()) (item, wh) = await SeedKeysAsync(seed);

        var interceptor = new OnceBeforeSave(async () =>
        {
            await using var rival = NewCtx();
            rival.MaterialStocks.Add(new MaterialStock { MaterialItemId = item, WarehouseId = wh, OnHandQuantity = 5, IsActive = true });
            await rival.SaveChangesAsync();
        });

        await using var ctx = NewCtx(interceptor);
        var (ok, err) = await new MaterialStockService(ctx).ReceiveAsync(item, wh, null, 4, "race", null);

        Assert.True(ok, err);
        await using var verify = NewCtx();
        var row = await verify.MaterialStocks.SingleAsync(x => x.MaterialItemId == item && x.WarehouseId == wh);
        Assert.Equal(9m, row.OnHandQuantity);
        Assert.Equal(1, await verify.InventoryTransactions.CountAsync(t => t.MaterialItemId == item));
    }

    [Fact]
    public async Task Receive_OnRowVersionConflict_RetriesOnceOnFreshValue_NoDuplicateTransaction()
    {
        int item, wh;
        await using (var seed = await MigratedAsync())
        {
            (item, wh) = await SeedKeysAsync(seed);
            seed.MaterialStocks.Add(new MaterialStock { MaterialItemId = item, WarehouseId = wh, OnHandQuantity = 10, IsActive = true });
            await seed.SaveChangesAsync();
        }

        // رقیب بعد از خواندن ردیف و قبل از ذخیره‌ی ما، موجودی را 10→13 می‌کند ⇒ RowVersion ما کهنه است
        var interceptor = new OnceBeforeSave(async () =>
        {
            await using var rival = NewCtx();
            var r = await rival.MaterialStocks.SingleAsync(x => x.MaterialItemId == item && x.WarehouseId == wh);
            r.OnHandQuantity = 13;
            await rival.SaveChangesAsync();
        });

        await using var ctx = NewCtx(interceptor);
        var (ok, err) = await new MaterialStockService(ctx).ReceiveAsync(item, wh, null, 4, "conflict", null);

        Assert.True(ok, err);
        Assert.Equal(2, interceptor.PostingAttempts);                   // تلاش اول (تعارض RowVersion) + تلاش دوم (موفق) - محدود، نه حلقه‌ی بی‌پایان
        await using var verify = NewCtx();
        Assert.Equal(17m, (await verify.MaterialStocks.SingleAsync(x => x.MaterialItemId == item && x.WarehouseId == wh)).OnHandQuantity);
        Assert.Equal(1, await verify.InventoryTransactions.CountAsync(t => t.MaterialItemId == item));   // فقط یک تراکنش، نه دو تا
    }

    [Fact]
    public async Task AdjustBy_OnRowVersionConflict_RetriesOnTheFreshBalance_AndKeepsTheConcurrentReceipt()
    {
        int item, wh;
        await using (var seed = await MigratedAsync())
        {
            (item, wh) = await SeedKeysAsync(seed);
            seed.MaterialStocks.Add(new MaterialStock { MaterialItemId = item, WarehouseId = wh, OnHandQuantity = 10, IsActive = true });
            await seed.SaveChangesAsync();
        }

        // رقیب بعد از خواندن ردیف و قبل از ذخیره‌ی ما ۵ واحد دریافت می‌کند (10→15)؛ مصرف ما ۳ واحد است ⇒ باید ۱۲ شود (نه ۷ که دریافت رقیب را گم کند)
        var interceptor = new OnceBeforeSave(async () =>
        {
            await using var rival = NewCtx();
            var r = await new MaterialStockService(rival).ReceiveAsync(item, wh, null, 5, "rival", null);
            Assert.True(r.Success, r.Error);
        });

        await using var ctx = NewCtx(interceptor);
        var (ok, err) = await new MaterialStockService(ctx).AdjustByAsync(item, wh, null, -3, "use", null, "ProductionInput", 1);

        Assert.True(ok, err);
        await using var verify = NewCtx();
        Assert.Equal(12m, (await verify.MaterialStocks.SingleAsync(x => x.MaterialItemId == item && x.WarehouseId == wh)).OnHandQuantity);
        Assert.Equal(1, await verify.InventoryTransactions.CountAsync(t => t.MaterialItemId == item && t.ReferenceType == "ProductionInput"));
    }

    [Fact]
    public async Task ParallelFirstReceipts_NeverCreateDuplicateRows_AndTheLedgerMatchesTheBalance()
    {
        int item, wh;
        await using (var seed = await MigratedAsync()) (item, wh) = await SeedKeysAsync(seed);

        async Task<(bool Done, string? Error)> One(decimal qty)
        {
            await using var ctx = NewCtx();
            var (ok, err) = await new MaterialStockService(ctx).ReceiveAsync(item, wh, null, qty, "parallel", null);
            return (ok, err);
        }

        var results = await Task.WhenAll(Task.Run(() => One(1)), Task.Run(() => One(2)), Task.Run(() => One(4)), Task.Run(() => One(8)));

        await using var verify = NewCtx();
        Assert.Equal(1, await verify.MaterialStocks.CountAsync(x => x.MaterialItemId == item && x.WarehouseId == wh));   // هرگز تکراری
        var onHand = (await verify.MaterialStocks.SingleAsync(x => x.MaterialItemId == item && x.WarehouseId == wh)).OnHandQuantity;
        var ledger = await verify.InventoryTransactions.Where(t => t.MaterialItemId == item).SumAsync(t => t.Quantity);
        Assert.Equal(ledger, onHand);                                  // موجودی = Σ تراکنش‌ها: هیچ ثبت دوباره/گم‌شده‌ای نیست
        Assert.Equal(results.Count(r => r.Done), await verify.InventoryTransactions.CountAsync(t => t.MaterialItemId == item));
        // پس از سقف ۳ تلاش، شکست یک نتیجه‌ی صریح و قابل‌اقدام است (نه استثنای خام) و هیچ تراکنشی از آن ثبت نشده (شمارش بالا)
        Assert.All(results.Where(r => !r.Done), r => Assert.Contains("3", r.Error));
    }

    // ───────── Work Order spare return ─────────

    [Fact]
    public async Task SpareReturn_ConcurrentSecondReturnOfTheSameUsage_IsRejected_StockAndLedgerReflectOnlyOne()
    {
        int item, wh, wo, usageId;
        await using (var seed = await MigratedAsync())
        {
            (item, wh) = await SeedKeysAsync(seed);
            var order = new WorkOrder { WorkOrderNumber = "WO-" + Guid.NewGuid().ToString("N")[..8], WorkOrderType = "Corrective", Status = "Open", EquipmentId = (await SeedEquipmentAsync(seed)) };
            seed.WorkOrders.Add(order);
            await seed.SaveChangesAsync();
            wo = order.WorkOrderId;
            seed.MaterialStocks.Add(new MaterialStock { MaterialItemId = item, WarehouseId = wh, OnHandQuantity = 0, IsActive = true });
            var usage = new MaintenanceSpareUsage { WorkOrderId = wo, MaterialItemId = item, Quantity = 5, IsActive = true };
            seed.MaintenanceSpareUsages.Add(usage);
            seed.InventoryTransactions.Add(new InventoryTransaction { MaterialItemId = item, WarehouseId = wh, TransactionType = "Issue", Quantity = 5, ReferenceType = "WorkOrder", ReferenceId = wo, WorkOrderId = wo });
            await seed.SaveChangesAsync();
            usageId = usage.MaintenanceSpareUsageId;
        }

        // رقیب درست قبل از ذخیره‌ی ما، همان ۴ واحد را برمی‌گرداند؛ ما هم ۴ واحد می‌خواهیم برگردانیم (جمعاً ۸ > ۵ مصرف)
        var interceptor = new OnceBeforeSave(async () =>
        {
            await using var rival = NewCtx();
            var r = await WorkOrderSpareReturn.ReturnAsync(rival, usageId, wo, 4, "rival", null);
            Assert.True(r.Success, r.Error);
        });

        await using var ctx = NewCtx(interceptor);
        var (ok, err, _) = await WorkOrderSpareReturn.ReturnAsync(ctx, usageId, wo, 4, "ours", null);

        Assert.False(ok);
        Assert.Contains("3", err);                                     // پیام سقف تلاش هم‌زمانی
        await using var verify = NewCtx();
        Assert.Equal(4m, (await verify.MaterialStocks.SingleAsync(x => x.MaterialItemId == item && x.WarehouseId == wh)).OnHandQuantity);
        Assert.Equal(4m, (await verify.MaintenanceSpareUsages.SingleAsync(u => u.MaintenanceSpareUsageId == usageId)).ReturnedQuantity);
        Assert.Equal(1, await verify.InventoryTransactions.CountAsync(t => t.MaterialItemId == item && t.TransactionType == "Return"));
    }

    private static async Task<int> SeedEquipmentAsync(PEMSDbContext ctx)
    {
        var eq = await ctx.Equipments.FirstOrDefaultAsync();
        if (eq != null) return eq.EquipmentId;
        var tag = Guid.NewGuid().ToString("N")[..8];
        var disc = new Discipline { Code = tag[..6], Name = "D " + tag, IsActive = true };
        var project = new Project { ProjectCode = "P" + tag, ProjectName = "Project " + tag, IsActive = true };
        ctx.AddRange(disc, project);
        await ctx.SaveChangesAsync();
        var e = new Equipment { TagNumber = "EQ-" + tag, Name = "Eq " + tag, EquipmentType = "Pump", DisciplineId = disc.DisciplineId, ProjectId = project.ProjectId, IsActive = true };
        ctx.Equipments.Add(e);
        await ctx.SaveChangesAsync();
        return e.EquipmentId;
    }

    // ───────── Migration ─────────

    [Fact]
    public async Task Migration_WithPreExistingDuplicates_FailsPreflight_ChangesNothing_AndSucceedsAfterManualReconciliation()
    {
        await using var ctx = NewCtx();
        var migrator = ctx.GetService<IMigrator>();
        await migrator.MigrateAsync(PreviousMigration);

        var (item, wh) = await SeedKeysAsync(ctx);
        ctx.MaterialStocks.AddRange(
            new MaterialStock { MaterialItemId = item, WarehouseId = wh, OnHandQuantity = 5, IsActive = true },
            new MaterialStock { MaterialItemId = item, WarehouseId = wh, OnHandQuantity = 9, IsActive = true });
        await ctx.SaveChangesAsync();
        Assert.False(await IndexExistsAsync());

        var ex = await Assert.ThrowsAnyAsync<Exception>(() => migrator.MigrateAsync());
        Assert.Contains("P4 pre-flight failed", Flatten(ex));

        // هیچ ردیفی تغییر نکرد و Migration در تاریخچه ثبت نشد و ایندکس ساخته نشد
        Assert.False(await IndexExistsAsync());
        await using (var check = NewCtx())
        {
            Assert.Equal(new[] { 5m, 9m }, (await check.MaterialStocks.Where(x => x.MaterialItemId == item).OrderBy(x => x.MaterialStockId).Select(x => x.OnHandQuantity).ToListAsync()).ToArray());
            Assert.DoesNotContain("20261010141504_MaterialStockNoLocationUniqueness", await check.Database.GetAppliedMigrationsAsync());
        }

        // اصلاح دستی (کار انسان، نه Migration): ردیف تکراری با تأیید حذف می‌شود، سپس Migration موفق می‌شود
        await ctx.Database.ExecuteSqlRawAsync("DELETE FROM MaterialStocks WHERE MaterialItemId = {0} AND OnHandQuantity = 9", item);
        await migrator.MigrateAsync();
        Assert.True(await IndexExistsAsync());
    }

    [Fact]
    public async Task Migration_OnCleanData_CreatesTheFilteredUniqueIndex_AndDownRemovesIt_LeavingDataUntouched()
    {
        await using var ctx = NewCtx();
        var migrator = ctx.GetService<IMigrator>();
        await migrator.MigrateAsync(PreviousMigration);
        var (item, wh) = await SeedKeysAsync(ctx);
        ctx.MaterialStocks.Add(new MaterialStock { MaterialItemId = item, WarehouseId = wh, OnHandQuantity = 7, IsActive = true });
        await ctx.SaveChangesAsync();

        await migrator.MigrateAsync();
        Assert.True(await IndexExistsAsync());
        await using (var check = NewCtx())
        {
            var def = await check.Database.SqlQueryRaw<string>($"SELECT filter_definition AS Value FROM sys.indexes WHERE name = '{IndexName}'").SingleAsync();
            Assert.Contains("StorageLocationId", def);
            Assert.True(await check.Database.SqlQueryRaw<bool>($"SELECT is_unique AS Value FROM sys.indexes WHERE name = '{IndexName}'").SingleAsync());
        }

        await migrator.MigrateAsync(PreviousMigration);                 // Down
        Assert.False(await IndexExistsAsync());
        await using var after = NewCtx();
        Assert.Equal(7m, (await after.MaterialStocks.SingleAsync(x => x.MaterialItemId == item)).OnHandQuantity);
    }

    private static string Flatten(Exception ex)
    {
        var parts = new List<string>();
        for (Exception? e = ex; e != null; e = e.InnerException) parts.Add(e.Message);
        return string.Join(" | ", parts);
    }
}
