using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Application.Inventory;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Inventory;

/// <summary>
/// Phase 2 / P1-P3 روی SQL Server واقعی (دیتابیس یک‌بارمصرف با MigrateAsync کامل - نه PEMSDB/PEMSDB_PcatClone): ترجمه‌ی LINQ به SQL،
/// ثابت‌بودن تعداد Queryها (بدون N+1) و فقط‌خواندنی‌بودن. InMemory ترجمه‌ی SQL را اثبات نمی‌کند.
/// </summary>
public class NetDemandSqlTests : IAsyncLifetime
{
    private readonly string _dbName = "PEMS_TEST_NETDEMAND_" + Guid.NewGuid().ToString("N")[..12];
    private string _connection = "";

    private sealed class CountingInterceptor : DbCommandInterceptor
    {
        public int Queries;
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref Queries);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }

    private sealed class AllowAll : IPermissionService
    {
        public Task<bool> HasPermissionAsync(IEnumerable<string> userRoles, string moduleName, string action) => Task.FromResult(true);
        public Task<List<RolePermissionDto>> GetMatrixAsync() => throw new NotSupportedException();
        public Task SetPermissionsAsync(string roleName, IEnumerable<ModulePermissionInput> permissions) => throw new NotSupportedException();
        public Task SeedDefaultsAsync() => throw new NotSupportedException();
    }

    public async Task InitializeAsync()
    {
        _connection = new SqlConnectionStringBuilder(PemsDbContextFactory.ConnectionString) { InitialCatalog = _dbName }.ConnectionString;
        await using var ctx = NewCtx();
        await ctx.Database.MigrateAsync();
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

    private PEMSDbContext NewCtx(CountingInterceptor? counter = null)
    {
        var b = new DbContextOptionsBuilder<PEMSDbContext>().UseSqlServer(_connection);
        if (counter != null) b.AddInterceptors(counter);
        return new PEMSDbContext(b.Options);
    }

    private sealed record Base(int Warehouse, int Uom, int Employee, int Equipment);

    private static async Task<Base> SeedBaseAsync(PEMSDbContext ctx)
    {
        var dep = new Department { Code = "D1", Name = "Dept" };
        var disc = new Discipline { Code = "X1", Name = "Disc" };
        var uom = new MaterialUOM { Code = "EA", Name = "Each" };
        var wh = new Warehouse { Code = "W1", Name = "WH" };
        ctx.AddRange(dep, disc, uom, wh);
        await ctx.SaveChangesAsync();
        var emp = new Employee { FirstName = "A", LastName = "B", DepartmentId = dep.DepartmentId, DisciplineId = disc.DisciplineId };
        var project = new Project { ProjectCode = "PX", ProjectName = "Proj", Status = "Active" };
        ctx.AddRange(emp, project);
        await ctx.SaveChangesAsync();
        var eq = new Equipment { TagNumber = "EQ-1", Name = "Pump", EquipmentType = "Pump", ProjectId = project.ProjectId, DisciplineId = disc.DisciplineId, Status = "Active" };
        ctx.Equipments.Add(eq);
        await ctx.SaveChangesAsync();
        return new Base(wh.WarehouseId, uom.MaterialUOMId, emp.EmployeeId, eq.EquipmentId);
    }

    private static async Task<string> SnapshotAsync(PEMSDbContext ctx) =>
        string.Join("|", await ctx.MaterialStocks.AsNoTracking().OrderBy(s => s.MaterialStockId).Select(s => s.OnHandQuantity + ":" + s.ReservedQuantity).ToListAsync())
        + "#" + await ctx.InventoryTransactions.CountAsync() + "#" + await ctx.MaterialReservations.CountAsync()
        + "#" + string.Join(",", await ctx.StoreRequisitions.AsNoTracking().OrderBy(s => s.StoreRequisitionId).Select(s => s.Status).ToListAsync());

    [Fact]
    public async Task MixedScenario_OnRealSql_ProducesTheAuditedNumbers_WithoutWriting()
    {
        await using var seed = NewCtx();
        var b = await SeedBaseAsync(seed);
        var a = new MaterialItem { MaterialCode = "A", Description = "A", UomId = b.Uom };
        var itemB = new MaterialItem { MaterialCode = "B", Description = "B", UomId = b.Uom };
        seed.MaterialItems.AddRange(a, itemB);
        await seed.SaveChangesAsync();
        seed.MaterialStocks.Add(new MaterialStock { MaterialItemId = a.MaterialItemId, WarehouseId = b.Warehouse, OnHandQuantity = 10, ReservedQuantity = 6 });

        // E3: SR-1 Reserved با رزرو 6؛ SR-2 Approved 5 ⇒ کمبود 1
        var sr1 = new StoreRequisition { RequisitionNumber = "SR-1", Status = "Reserved", WarehouseId = b.Warehouse, RequestedById = b.Employee };
        sr1.Lines.Add(new StoreRequisitionLine { MaterialItemId = a.MaterialItemId, RequestedQuantity = 6, ApprovedQuantity = 6 });
        var sr2 = new StoreRequisition { RequisitionNumber = "SR-2", Status = "Approved", WarehouseId = b.Warehouse, RequestedById = b.Employee };
        sr2.Lines.Add(new StoreRequisitionLine { MaterialItemId = a.MaterialItemId, RequestedQuantity = 5, ApprovedQuantity = 5 });
        seed.StoreRequisitions.AddRange(sr1, sr2);

        // WO بدون SR: مصرف 1 از 3 ⇒ 2 بدون انبار
        var wo1 = new WorkOrder { EquipmentId = b.Equipment, WorkOrderNumber = "WO-1", WorkOrderType = "Corrective", Priority = "Low", Status = "InProgress" };
        // WO با SR هم‌پوشان ⇒ مبهم
        var wo2 = new WorkOrder { EquipmentId = b.Equipment, WorkOrderNumber = "WO-2", WorkOrderType = "Corrective", Priority = "Low", Status = "Assigned" };
        seed.WorkOrders.AddRange(wo1, wo2);
        await seed.SaveChangesAsync();
        var sr3 = new StoreRequisition { RequisitionNumber = "SR-3", Status = "Approved", WarehouseId = b.Warehouse, RequestedById = b.Employee, WorkOrderId = wo2.WorkOrderId };
        sr3.Lines.Add(new StoreRequisitionLine { MaterialItemId = itemB.MaterialItemId, RequestedQuantity = 2, ApprovedQuantity = 2 });
        seed.StoreRequisitions.Add(sr3);
        seed.WorkOrderMaterials.AddRange(
            new WorkOrderMaterial { WorkOrderId = wo1.WorkOrderId, MaterialItemId = itemB.MaterialItemId, RequiredQuantity = 3 },
            new WorkOrderMaterial { WorkOrderId = wo2.WorkOrderId, MaterialItemId = itemB.MaterialItemId, RequiredQuantity = 4 });
        seed.MaintenanceSpareUsages.Add(new MaintenanceSpareUsage { WorkOrderId = wo1.WorkOrderId, MaterialItemId = itemB.MaterialItemId, Quantity = 1 });
        await seed.SaveChangesAsync();
        seed.MaterialReservations.Add(new MaterialReservation
        {
            MaterialItemId = a.MaterialItemId, WarehouseId = b.Warehouse, Quantity = 6, Status = "Active", ReservedById = b.Employee,
            StoreRequisitionId = sr1.StoreRequisitionId, StoreRequisitionLineId = sr1.Lines.Single().StoreRequisitionLineId
        });
        await seed.SaveChangesAsync();
        var before = await SnapshotAsync(seed);

        var counter = new CountingInterceptor();
        await using var ctx = NewCtx(counter);
        var svc = new NetDemandService(new NetDemandCollector(ctx), new UsableStockService(ctx), new AllowAll());
        var report = await svc.CalculateAsync(new NetDemandScope(), new NetDemandCaller(new[] { "X" }));

        Assert.True(report.Authorized);
        var ra = report.Results.Single(r => r.MaterialItemId == a.MaterialItemId && r.WarehouseId == b.Warehouse);
        Assert.Equal(11m, ra.GrossEligibleDemand);
        Assert.Equal(6m, ra.CoveredByReservations);
        Assert.Equal(4m, ra.UsableStockForNetting);
        Assert.Equal(1m, ra.StockOnlyShortage);
        Assert.True(ra.IsDefinitive);

        var rbWarehouse = report.Results.Single(r => r.MaterialItemId == itemB.MaterialItemId && r.WarehouseId == b.Warehouse);
        Assert.Equal(2m, rbWarehouse.GrossEligibleDemand);                                    // فقط SR-3
        Assert.Equal(NetDemandResultKind.StockOnlyShortageEstimate, rbWarehouse.Kind);      // بدون ردیف موجودی ⇒ NoStockRecord = موجودی صفر
        Assert.Equal(2m, rbWarehouse.StockOnlyShortage);
        Assert.Contains(rbWarehouse.Issues, i => i.Code == NetDemandIssueCode.AmbiguousOverlapWithStoreRequisition);
        Assert.False(rbWarehouse.IsDefinitive);

        var rbUnassigned = report.Results.Single(r => r.MaterialItemId == itemB.MaterialItemId && r.WarehouseId == null);
        Assert.Equal(2m, rbUnassigned.GrossEligibleDemand);                                  // 3 - 1 مصرف (WO-1)؛ WO-2 مبهم و حذف
        Assert.Equal(NetDemandResultKind.NotComparableNoWarehouse, rbUnassigned.Kind);

        Assert.Equal(before, await SnapshotAsync(seed));
        Assert.True(counter.Queries <= 12, $"expected a small constant number of queries, got {counter.Queries}");
    }

    [Fact]
    public async Task QueryCount_DoesNotGrowWithTheNumberOfItemsOrLines()
    {
        await using var seed = NewCtx();
        var b = await SeedBaseAsync(seed);

        async Task<int> RunAsync(int itemCount)
        {
            var counter = new CountingInterceptor();
            await using var ctx = NewCtx(counter);
            var svc = new NetDemandService(new NetDemandCollector(ctx), new UsableStockService(ctx), new AllowAll());
            var report = await svc.CalculateAsync(new NetDemandScope(), new NetDemandCaller(new[] { "X" }));
            Assert.Equal(itemCount, report.Results.Count(r => r.WarehouseId == b.Warehouse));
            return counter.Queries;
        }

        // 1 قلم
        var one = new MaterialItem { MaterialCode = "S-0", Description = "x", UomId = b.Uom };
        seed.MaterialItems.Add(one);
        await seed.SaveChangesAsync();
        seed.MaterialStocks.Add(new MaterialStock { MaterialItemId = one.MaterialItemId, WarehouseId = b.Warehouse, OnHandQuantity = 5 });
        var srOne = new StoreRequisition { RequisitionNumber = "SR-S0", Status = "Approved", WarehouseId = b.Warehouse, RequestedById = b.Employee };
        srOne.Lines.Add(new StoreRequisitionLine { MaterialItemId = one.MaterialItemId, RequestedQuantity = 2, ApprovedQuantity = 2 });
        seed.StoreRequisitions.Add(srOne);
        await seed.SaveChangesAsync();
        var queriesForOne = await RunAsync(1);

        // 300 قلم
        var items = Enumerable.Range(1, 299).Select(i => new MaterialItem { MaterialCode = "S-" + i, Description = "x", UomId = b.Uom }).ToList();
        seed.MaterialItems.AddRange(items);
        await seed.SaveChangesAsync();
        seed.MaterialStocks.AddRange(items.Select(i => new MaterialStock { MaterialItemId = i.MaterialItemId, WarehouseId = b.Warehouse, OnHandQuantity = 5 }));
        var big = new StoreRequisition { RequisitionNumber = "SR-BIG", Status = "Approved", WarehouseId = b.Warehouse, RequestedById = b.Employee };
        foreach (var i in items) big.Lines.Add(new StoreRequisitionLine { MaterialItemId = i.MaterialItemId, RequestedQuantity = 7, ApprovedQuantity = 7 });
        seed.StoreRequisitions.Add(big);
        await seed.SaveChangesAsync();
        var queriesForMany = await RunAsync(300);

        Assert.Equal(queriesForOne, queriesForMany);       // بدون N+1
        Assert.True(queriesForMany <= 12, $"got {queriesForMany}");
    }
}
