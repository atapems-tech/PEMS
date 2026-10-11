using Microsoft.EntityFrameworkCore;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Application.Inventory;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Inventory;

/// <summary>Phase 2 / P1-P3 - ترکیب فقط‌خواندنی: مجوز حداقلی، مثال‌های عددی کامل، پوشاندن هویت منبع، بدون هیچ نوشتن.</summary>
public class NetDemandServiceTests
{
    private sealed class FakePermissions : IPermissionService
    {
        public HashSet<(string Role, string Module, string Action)> Grants { get; } = new();
        public Task<bool> HasPermissionAsync(IEnumerable<string> userRoles, string moduleName, string action) =>
            Task.FromResult(userRoles.Any(r => Grants.Contains((r, moduleName, action))));
        public Task<List<RolePermissionDto>> GetMatrixAsync() => throw new NotSupportedException();
        public Task SetPermissionsAsync(string roleName, IEnumerable<ModulePermissionInput> permissions) => throw new NotSupportedException();
        public Task SeedDefaultsAsync() => throw new NotSupportedException();
    }

    private sealed class Env
    {
        public required PEMSDbContext Db { get; init; }
        public required NetDemandService Svc { get; init; }
        public required FakePermissions Perms { get; init; }
        public int Each, Item, Wh;
        public int Sr = 1;
    }

    private static readonly NetDemandCaller Viewer = new(new[] { "INV" });
    private static readonly NetDemandCaller Everything = new(new[] { "ALL" });

    private static async Task<Env> NewAsync()
    {
        var db = PemsDbContextFactory.CreateInMemory("netsvc_" + Guid.NewGuid().ToString("N"));
        var uom = new MaterialUOM { Code = "EA", Name = "Each" };
        var wh = new Warehouse { Code = "WH-1", Name = "Main" };
        db.AddRange(uom, wh);
        await db.SaveChangesAsync();
        var item = new MaterialItem { MaterialCode = "M-1", Description = "Bearing", UomId = uom.MaterialUOMId };
        db.MaterialItems.Add(item);
        await db.SaveChangesAsync();

        var perms = new FakePermissions();
        perms.Grants.Add(("INV", "MaterialItem", "View"));
        foreach (var m in new[] { "MaterialItem", "StoreRequisition", "WorkOrder" }) perms.Grants.Add(("ALL", m, "View"));
        return new Env
        {
            Db = db, Perms = perms, Each = uom.MaterialUOMId, Item = item.MaterialItemId, Wh = wh.WarehouseId,
            Svc = new NetDemandService(new NetDemandCollector(db), new UsableStockService(db), perms)
        };
    }

    private static async Task<StoreRequisition> SrAsync(Env e, string status, decimal qty, int? workOrderId = null, decimal reserved = 0)
    {
        var sr = new StoreRequisition { RequisitionNumber = "SR-" + e.Sr++, Status = status, WarehouseId = e.Wh, RequestedById = 1, WorkOrderId = workOrderId };
        sr.Lines.Add(new StoreRequisitionLine { MaterialItemId = e.Item, RequestedQuantity = qty, ApprovedQuantity = qty });
        e.Db.StoreRequisitions.Add(sr);
        await e.Db.SaveChangesAsync();
        if (reserved > 0)
        {
            e.Db.MaterialReservations.Add(new MaterialReservation
            {
                MaterialItemId = e.Item, WarehouseId = e.Wh, Quantity = reserved, Status = "Active", ReservedById = 1,
                StoreRequisitionId = sr.StoreRequisitionId, StoreRequisitionLineId = sr.Lines.Single().StoreRequisitionLineId
            });
            await e.Db.SaveChangesAsync();
        }
        return sr;
    }

    private static async Task StockAsync(Env e, decimal onHand, decimal reserved)
    {
        e.Db.MaterialStocks.Add(new MaterialStock { MaterialItemId = e.Item, WarehouseId = e.Wh, OnHandQuantity = onHand, ReservedQuantity = reserved });
        await e.Db.SaveChangesAsync();
    }

    private static async Task<NetDemandReport> RunAsync(Env e, NetDemandCaller caller)
    {
        e.Db.ChangeTracker.Clear();
        return await e.Svc.CalculateAsync(new NetDemandScope(), caller);
    }

    [Fact]
    public async Task CallerWithoutInventoryView_GetsNoResultsAtAll()
    {
        var e = await NewAsync();
        await StockAsync(e, 10, 0);
        await SrAsync(e, "Approved", 5);

        var report = await RunAsync(e, new NetDemandCaller(new[] { "NOBODY" }));

        Assert.False(report.Authorized);
        Assert.Empty(report.Results);
    }

    [Fact]
    public async Task ReservedExample_UsesReservationOnce_ShortageIsOne_AndTheResultIsAStockOnlyEstimate()
    {
        // OnHand 10، Reserved 6 (SR-A رزرو کامل)، SR-B تأییدشده 5 بدون رزرو ⇒ Usable 4، Unreserved 5 ⇒ کمبود 1
        var e = await NewAsync();
        await StockAsync(e, 10, 6);
        await SrAsync(e, "Reserved", 6, reserved: 6);
        await SrAsync(e, "Approved", 5);

        var report = await RunAsync(e, Everything);

        var r = Assert.Single(report.Results);
        Assert.Equal(11m, r.GrossEligibleDemand);
        Assert.Equal(6m, r.CoveredByReservations);
        Assert.Equal(5m, r.UnreservedEligibleDemand);
        Assert.Equal(4m, r.UsableStockForNetting);
        Assert.Equal(1m, r.StockOnlyShortage);
        Assert.Equal(NetDemandResultKind.StockOnlyShortageEstimate, r.Kind);
        Assert.Equal(IncomingSupplyStatus.NotModeled, r.IncomingSupply);
        Assert.Null(r.CommittedShortage);
        Assert.True(r.IsDefinitive);
        Assert.Equal(2, r.Contributions.Count);
    }

    [Fact]
    public async Task NormalAndPartialIssueExamples()
    {
        var e = await NewAsync();
        await StockAsync(e, 4, 0);
        await SrAsync(e, "Approved", 10);              // E1: کمبود 6

        var r = Assert.Single((await RunAsync(e, Everything)).Results);

        Assert.Equal(6m, r.StockOnlyShortage);
    }

    [Fact]
    public async Task WorkOrderDemand_IsNeverNettedAgainstAWarehouse_AndMakesTheMaterialsWarehouseResultNonDefinitive()
    {
        var e = await NewAsync();
        await StockAsync(e, 100, 0);
        await SrAsync(e, "Approved", 5);
        var wo = new WorkOrder { EquipmentId = 1, WorkOrderNumber = "WO-1", WorkOrderType = "Corrective", Priority = "Low", Status = "InProgress" };
        e.Db.WorkOrders.Add(wo);
        await e.Db.SaveChangesAsync();
        e.Db.WorkOrderMaterials.Add(new WorkOrderMaterial { WorkOrderId = wo.WorkOrderId, MaterialItemId = e.Item, RequiredQuantity = 20 });
        await e.Db.SaveChangesAsync();

        var report = await RunAsync(e, Everything);

        var warehouse = report.Results.Single(x => x.WarehouseId == e.Wh);
        var unassigned = report.Results.Single(x => x.WarehouseId == null);
        Assert.Equal(NetDemandResultKind.NotComparableNoWarehouse, unassigned.Kind);
        Assert.Equal(20m, unassigned.GrossEligibleDemand);
        Assert.Null(unassigned.StockOnlyShortage);
        Assert.Equal(5m, warehouse.GrossEligibleDemand);                   // WO در هیچ انباری Pool نشد
        Assert.Equal(0m, warehouse.StockOnlyShortage);
        Assert.False(warehouse.IsDefinitive);
        Assert.Contains(warehouse.Issues, i => i.Code == NetDemandIssueCode.WorkOrderDemandNotAttributableToWarehouse);
    }

    [Fact]
    public async Task AmbiguousWorkOrderStoreRequisitionOverlap_ExcludesTheWorkOrderQuantity_AndFlagsTheWarehouseResult()
    {
        var e = await NewAsync();
        await StockAsync(e, 1, 0);
        var wo = new WorkOrder { EquipmentId = 1, WorkOrderNumber = "WO-1", WorkOrderType = "Corrective", Priority = "Low", Status = "InProgress" };
        e.Db.WorkOrders.Add(wo);
        await e.Db.SaveChangesAsync();
        e.Db.WorkOrderMaterials.Add(new WorkOrderMaterial { WorkOrderId = wo.WorkOrderId, MaterialItemId = e.Item, RequiredQuantity = 8 });
        await e.Db.SaveChangesAsync();
        await SrAsync(e, "Approved", 5, workOrderId: wo.WorkOrderId);

        var report = await RunAsync(e, Everything);

        var r = report.Results.Single(x => x.WarehouseId == e.Wh);
        Assert.Equal(5m, r.GrossEligibleDemand);                           // 13 دوبار شمرده نشد
        Assert.Equal(4m, r.StockOnlyShortage);
        Assert.Contains(r.Issues, i => i.Code == NetDemandIssueCode.AmbiguousOverlapWithStoreRequisition);
        Assert.False(r.IsDefinitive);
        Assert.DoesNotContain(report.Results, x => x.WarehouseId == null && x.GrossEligibleDemand > 0);
    }

    [Fact]
    public async Task AmbiguousStockRows_MakeTheResultNotCalculable()
    {
        var e = await NewAsync();
        await StockAsync(e, 5, 0);
        await StockAsync(e, 5, 0);                                         // دو ردیف بدون محل
        await SrAsync(e, "Approved", 3);

        var r = Assert.Single((await RunAsync(e, Everything)).Results);

        Assert.Equal(NetDemandResultKind.NotCalculable, r.Kind);
        Assert.Null(r.StockOnlyShortage);
        Assert.Contains(r.Issues, i => i.Code == NetDemandIssueCode.StockAmbiguousRows);
        Assert.False(r.IsDefinitive);
    }

    [Fact]
    public async Task IncompatibleUnit_IsExcluded_AndTheResultIsNotDefinitive()
    {
        var e = await NewAsync();
        var box = new MaterialUOM { Code = "BX", Name = "Box" };
        e.Db.MaterialUOMs.Add(box);
        await e.Db.SaveChangesAsync();
        await StockAsync(e, 10, 0);
        var sr = new StoreRequisition { RequisitionNumber = "SR-X", Status = "Approved", WarehouseId = e.Wh, RequestedById = 1 };
        sr.Lines.Add(new StoreRequisitionLine { MaterialItemId = e.Item, RequestedQuantity = 50, ApprovedQuantity = 50, UomId = box.MaterialUOMId });
        e.Db.StoreRequisitions.Add(sr);
        await e.Db.SaveChangesAsync();

        var r = Assert.Single((await RunAsync(e, Everything)).Results);

        Assert.Equal(0m, r.GrossEligibleDemand);                           // تبدیلی انجام نشد
        Assert.Contains(r.Excluded, x => x.Reason == NetDemandIssueCode.UnitIncompatible);
        Assert.False(r.IsDefinitive);
    }

    [Fact]
    public async Task CallerWithoutSourceViewPermission_SeesMaskedOrigins_ButQuantitiesStayInTheTotals()
    {
        var e = await NewAsync();
        await StockAsync(e, 0, 0);
        await SrAsync(e, "Approved", 9);

        var masked = Assert.Single((await RunAsync(e, Viewer)).Results);
        var full = Assert.Single((await RunAsync(e, Everything)).Results);

        Assert.Equal(9m, masked.StockOnlyShortage);                        // مقدار حذف نشد
        var origin = Assert.Single(masked.Contributions).Origin;
        Assert.True(origin.IsRestricted);
        Assert.Equal(0, origin.SourceLineId);
        Assert.Equal(0, origin.ParentId);
        Assert.Null(origin.ParentNumber);
        Assert.All(masked.Issues, i => Assert.Null(i.Detail));

        var visible = Assert.Single(full.Contributions).Origin;
        Assert.False(visible.IsRestricted);
        Assert.Equal("SR-1", visible.ParentNumber);
    }

    [Fact]
    public async Task Calculation_IsReadOnly_AndRepeatedRunsAreIdentical()
    {
        var e = await NewAsync();
        await StockAsync(e, 10, 2);
        await SrAsync(e, "Reserved", 2, reserved: 2);
        await SrAsync(e, "Approved", 12);
        string Snap() => string.Join("|", e.Db.MaterialStocks.AsNoTracking().Select(s => $"{s.OnHandQuantity}:{s.ReservedQuantity}").ToList())
            + "#" + e.Db.InventoryTransactions.Count() + "#" + e.Db.MaterialReservations.Count();
        var before = Snap();

        var a = await RunAsync(e, Everything);
        var b = await RunAsync(e, Everything);

        Assert.False(e.Db.ChangeTracker.HasChanges());
        Assert.Equal(before, Snap());
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(a), System.Text.Json.JsonSerializer.Serialize(b));
        Assert.Equal(4m, a.Results.Single().StockOnlyShortage);             // Gross 14، Covered 2، Unreserved 12، Usable 8 ⇒ 4
    }
}
