using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;
using PEMS.Persistence.Identity;
using PEMS.Persistence.Seed;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Inventory;

/// <summary>
/// Phase 6.5 closure: Close Short (Reserved→Closed و PartiallyIssued→Closed) با ارقام دقیق، مجوزها با نقش‌های واقعی Seed‌شده
/// (WH.Manager / WH.Storekeeper / ...) روی سرویس واقعی RoleModuleAction، پوشش AuditLog برای همه‌ی عمل‌ها، و ردیابی بدون حدس.
/// </summary>
public class StoreRequisitionClosureGateTests
{
    private const int Requester = 1, Manager = 2, Keeper = 3, Outsider = 4, Inactive = 5, Wh = 1, Item = 10;

    private sealed class Fx
    {
        public required PEMSDbContext Db { get; init; }
        public required StoreRequisitionLifecycleService Svc { get; init; }
        public int ReqId, Line;
    }

    /// <summary>سرویس واقعی با Seed واقعی نقش‌ها/مجوزها (همان مسیر AccessControlParityTests).</summary>
    private static async Task<Fx> NewAsync(decimal onHand = 200, decimal requested = 100)
    {
        var db = PemsDbContextFactory.CreateInMemory("cg_" + Guid.NewGuid().ToString("N"));
        foreach (var name in SystemRoles.All.Concat(RoleCatalog.Definitions.Select(d => d.Name)).Distinct())
            db.Roles.Add(new ApplicationRole(name) { NormalizedName = name.ToUpperInvariant() });
        await db.SaveChangesAsync();

        var access = new AccessControlService(db);
        await new PermissionService(db, access).SeedDefaultsAsync();
        var actions = new RoleModuleActionService(db, access);
        await actions.SeedDefaultsAsync();
        var services = new ServiceCollection(); services.AddSingleton(db);
        await AccessControlSeeder.SeedFromLegacyAsync(services.BuildServiceProvider());

        db.Warehouses.Add(new Warehouse { WarehouseId = Wh, Code = "W", Name = "W" });
        foreach (var id in new[] { Requester, Manager, Keeper, Outsider }) db.Employees.Add(new Employee { EmployeeId = id, FirstName = "E" + id, LastName = "T" });
        db.Employees.Add(new Employee { EmployeeId = Inactive, FirstName = "Gone", LastName = "T", IsActive = false });
        db.MaterialItems.Add(new MaterialItem { MaterialItemId = Item, MaterialCode = "A", Description = "A" });
        db.MaterialStocks.Add(new MaterialStock { MaterialItemId = Item, WarehouseId = Wh, OnHandQuantity = onHand });
        var req = new StoreRequisition { RequisitionNumber = "SR-C", WarehouseId = Wh, RequestedById = Requester };
        req.Lines.Add(new StoreRequisitionLine { MaterialItemId = Item, RequestedQuantity = requested });
        db.StoreRequisitions.Add(req);
        db.InventoryTransactions.Add(new InventoryTransaction { MaterialItemId = Item, WarehouseId = Wh, TransactionType = "Receipt", Quantity = onHand });
        await db.SaveChangesAsync();
        return new Fx { Db = db, Svc = new StoreRequisitionLifecycleService(db, actions), ReqId = req.StoreRequisitionId, Line = req.Lines.Single().StoreRequisitionLineId };
    }

    private static StoreRequisitionActor As(int emp, params string[] roles) => new(emp, roles);
    private static readonly string[] Wm = { RoleCatalog.WarehouseManager };
    private static readonly string[] Sk = { RoleCatalog.WarehouseStorekeeper };

    // ------------------------------------------------------------------ Close Short — دقیقاً دو سناریوی دستورالعمل

    [Fact]
    public async Task CloseShort_FromReserved_NothingIssued_Approved70_Shortfall70_Released70()
    {
        var fx = await NewAsync();
        Assert.True((await fx.Svc.AdvanceStatusAsync(fx.ReqId, "Submitted", null, As(Requester, Wm))).Success);
        Assert.True((await fx.Svc.SetApprovedQuantitiesAsync(fx.ReqId, new Dictionary<int, decimal> { [fx.Line] = 70 }, As(Manager, Wm))).Success);
        Assert.True((await fx.Svc.AdvanceStatusAsync(fx.ReqId, "Approved", null, As(Manager, Wm))).Success);
        Assert.True((await fx.Svc.AdvanceStatusAsync(fx.ReqId, "Reserved", null, As(Keeper, Sk))).Success);
        Assert.Equal(70, (await fx.Db.MaterialStocks.AsNoTracking().SingleAsync()).ReservedQuantity);

        Assert.True((await fx.Svc.AdvanceStatusAsync(fx.ReqId, "Closed", "not needed any more", As(Manager, Wm))).Success);

        var req = await fx.Db.StoreRequisitions.AsNoTracking().Include(r => r.Lines).SingleAsync();
        var line = req.Lines.Single();
        var stock = await fx.Db.MaterialStocks.AsNoTracking().SingleAsync();
        Assert.Equal("Closed", req.Status);
        Assert.Equal((70m, 0m), (line.ApprovedQuantity, line.IssuedQuantity));
        Assert.Equal(70m, line.ApprovedQuantity!.Value - line.IssuedQuantity);                       // کسری = 70
        Assert.Equal((200m, 0m), (stock.OnHandQuantity, stock.ReservedQuantity));
        Assert.Equal(70, (await fx.Db.InventoryTransactions.AsNoTracking().SingleAsync(t => t.TransactionType == "ReservationRelease")).Quantity);   // آزادسازی 70
        Assert.Equal("Released", (await fx.Db.MaterialReservations.AsNoTracking().SingleAsync()).Status);
        var reason = (await fx.Db.StoreRequisitionStatusHistories.AsNoTracking().OrderBy(h => h.StoreRequisitionStatusHistoryId).LastAsync()).Reason!;
        Assert.StartsWith("CloseShort:", reason);
        Assert.Contains($"line {fx.Line}: approved 70, issued 0", reason);
    }

    [Fact]
    public async Task CloseShort_AfterPartialIssue_Approved70_Issued50_Shortfall20_Released20()
    {
        var fx = await NewAsync();
        await fx.Svc.AdvanceStatusAsync(fx.ReqId, "Submitted", null, As(Requester, Wm));
        await fx.Svc.SetApprovedQuantitiesAsync(fx.ReqId, new Dictionary<int, decimal> { [fx.Line] = 70 }, As(Manager, Wm));
        await fx.Svc.AdvanceStatusAsync(fx.ReqId, "Approved", null, As(Manager, Wm));
        await fx.Svc.AdvanceStatusAsync(fx.ReqId, "Reserved", null, As(Keeper, Sk));
        Assert.True((await fx.Svc.IssueAsync(fx.ReqId, new Dictionary<int, decimal> { [fx.Line] = 50 }, null, As(Keeper, Sk))).Success);

        Assert.True((await fx.Svc.AdvanceStatusAsync(fx.ReqId, "Closed", "short", As(Manager, Wm))).Success);

        var req = await fx.Db.StoreRequisitions.AsNoTracking().Include(r => r.Lines).SingleAsync();
        var stock = await fx.Db.MaterialStocks.AsNoTracking().SingleAsync();
        Assert.Equal("Closed", req.Status);
        Assert.Equal((70m, 50m), (req.Lines.Single().ApprovedQuantity, req.Lines.Single().IssuedQuantity));
        Assert.Equal(20m, req.Lines.Single().ApprovedQuantity!.Value - req.Lines.Single().IssuedQuantity);
        Assert.Equal((150m, 0m), (stock.OnHandQuantity, stock.ReservedQuantity));
        Assert.Equal(20, (await fx.Db.InventoryTransactions.AsNoTracking().SingleAsync(t => t.TransactionType == "ReservationRelease")).Quantity);
        Assert.Contains("approved 70, issued 50", (await fx.Db.StoreRequisitionStatusHistories.AsNoTracking().OrderBy(h => h.StoreRequisitionStatusHistoryId).LastAsync()).Reason);
    }

    [Fact]
    public async Task NormalClose_OnlyAfterFullIssue_AndNeverReleasesOrImpliesShortfall()
    {
        var fx = await NewAsync();
        await fx.Svc.AdvanceStatusAsync(fx.ReqId, "Submitted", null, As(Requester, Wm));
        await fx.Svc.AdvanceStatusAsync(fx.ReqId, "Approved", null, As(Manager, Wm));
        await fx.Svc.AdvanceStatusAsync(fx.ReqId, "Reserved", null, As(Keeper, Sk));
        Assert.True((await fx.Svc.IssueAsync(fx.ReqId, null, null, As(Keeper, Sk))).Success);                 // کامل: 100
        Assert.True((await fx.Svc.AdvanceStatusAsync(fx.ReqId, "Closed", "done", As(Keeper, Sk))).Success);    // Storekeeper مجوز Close دارد
        var last = await fx.Db.StoreRequisitionStatusHistories.AsNoTracking().OrderBy(h => h.StoreRequisitionStatusHistoryId).LastAsync();
        Assert.DoesNotContain("CloseShort", last.Reason ?? "");
        Assert.Empty(await fx.Db.InventoryTransactions.AsNoTracking().Where(t => t.TransactionType == "ReservationRelease").ToListAsync());
    }

    [Fact]
    public async Task Storekeeper_CannotCloseShort_ButManagerCan()
    {
        var fx = await NewAsync();
        await fx.Svc.AdvanceStatusAsync(fx.ReqId, "Submitted", null, As(Requester, Wm));
        await fx.Svc.AdvanceStatusAsync(fx.ReqId, "Approved", null, As(Manager, Wm));
        await fx.Svc.AdvanceStatusAsync(fx.ReqId, "Reserved", null, As(Keeper, Sk));
        Assert.False((await fx.Svc.AdvanceStatusAsync(fx.ReqId, "Closed", "short", As(Keeper, Sk))).Success);
        Assert.True((await fx.Svc.AdvanceStatusAsync(fx.ReqId, "Closed", "short", As(Manager, Wm))).Success);
    }

    // ------------------------------------------------------------------ هویت + مجوز با نقش‌های واقعی

    [Fact]
    public async Task RealRoles_WhManagerAndStorekeeper_AreEnforcedServerSide()
    {
        var fx = await NewAsync();
        Assert.True((await fx.Svc.AdvanceStatusAsync(fx.ReqId, "Submitted", null, As(Requester, Wm))).Success);

        Assert.False((await fx.Svc.AdvanceStatusAsync(fx.ReqId, "Approved", null, As(Keeper, Sk))).Success);        // Storekeeper: بدون Approve
        Assert.False((await fx.Svc.AdvanceStatusAsync(fx.ReqId, "Approved", null, As(Outsider, "Viewer"))).Success);  // Viewer
        Assert.False((await fx.Svc.AdvanceStatusAsync(fx.ReqId, "Approved", null, As(Outsider))).Success);             // بدون نقش
        Assert.False((await fx.Svc.AdvanceStatusAsync(fx.ReqId, "Approved", null, As(Requester, Wm))).Success);        // WH.Manager اما درخواست‌دهنده ⇒ SoD
        Assert.True((await fx.Svc.AdvanceStatusAsync(fx.ReqId, "Approved", null, As(Manager, Wm))).Success);

        Assert.False((await fx.Svc.AdvanceStatusAsync(fx.ReqId, "Reserved", null, As(Outsider, "Viewer"))).Success);
        Assert.True((await fx.Svc.AdvanceStatusAsync(fx.ReqId, "Reserved", null, As(Keeper, Sk))).Success);          // Storekeeper می‌تواند Reserve کند

        Assert.False((await fx.Svc.IssueAsync(fx.ReqId, null, null, As(Requester, Wm))).Success);                     // درخواست‌دهنده نمی‌تواند تحویل دهد
        Assert.False((await fx.Svc.IssueAsync(fx.ReqId, null, null, As(Outsider, "Viewer"))).Success);
        Assert.True((await fx.Svc.IssueAsync(fx.ReqId, null, null, As(Keeper, Sk))).Success);
    }

    [Theory]
    [InlineData(null)] [InlineData(0)] [InlineData(999)] [InlineData(Inactive)]
    public async Task EveryAction_IsRejectedForMissingUnknownOrInactiveEmployee_EvenWithAllPermissions(int? employee)
    {
        var fx = await NewAsync();
        var actor = new StoreRequisitionActor(employee, Wm);
        Assert.False((await fx.Svc.AdvanceStatusAsync(fx.ReqId, "Submitted", null, actor)).Success);
        Assert.False((await fx.Svc.SetApprovedQuantitiesAsync(fx.ReqId, new Dictionary<int, decimal> { [fx.Line] = 1 }, actor)).Success);
        Assert.False((await fx.Svc.AdvanceStatusAsync(fx.ReqId, "Cancelled", "x", actor)).Success);
        Assert.False((await fx.Svc.IssueAsync(fx.ReqId, null, null, actor)).Success);
        Assert.Equal("Draft", (await fx.Db.StoreRequisitions.AsNoTracking().SingleAsync()).Status);
    }

    // ------------------------------------------------------------------ ممیزی برای همه‌ی عمل‌ها

    private static async Task<List<string>> AuditOf(Fx fx, string entity) =>
        (await fx.Db.AuditLogs.AsNoTracking().Where(a => a.EntityName == entity).OrderBy(a => a.AuditLogId).ToListAsync())
        .Select(a => a.Action + ":" + a.ChangedFieldsSummary).ToList();

    [Fact]
    public async Task AuditLog_CoversSubmitApproveReserveReleaseIssueClose_WithBeforeAfter()
    {
        var fx = await NewAsync();
        await fx.Svc.AdvanceStatusAsync(fx.ReqId, "Submitted", null, As(Requester, Wm));
        await fx.Svc.SetApprovedQuantitiesAsync(fx.ReqId, new Dictionary<int, decimal> { [fx.Line] = 70 }, As(Manager, Wm));
        await fx.Svc.AdvanceStatusAsync(fx.ReqId, "Approved", null, As(Manager, Wm));
        await fx.Svc.AdvanceStatusAsync(fx.ReqId, "Reserved", null, As(Keeper, Sk));
        await fx.Svc.IssueAsync(fx.ReqId, new Dictionary<int, decimal> { [fx.Line] = 50 }, null, As(Keeper, Sk));
        await fx.Svc.AdvanceStatusAsync(fx.ReqId, "Closed", "short", As(Manager, Wm));

        var reqAudit = string.Join(" | ", await AuditOf(fx, "StoreRequisition"));
        foreach (var step in new[] { "Draft -> Submitted", "Submitted -> Approved", "Approved -> Reserved", "Reserved -> PartiallyIssued", "PartiallyIssued -> Closed" })
            Assert.Contains("Status: " + step, reqAudit);

        var stock = string.Join(" | ", await AuditOf(fx, "MaterialStock"));
        Assert.Contains("ReservedQuantity: 0 -> 70", stock);                    // Reserve
        Assert.Contains("OnHandQuantity: 200 -> 150", stock);                   // Partial issue
        Assert.Contains("ReservedQuantity: 20 -> 0", stock);                    // Release (close short)
        Assert.Contains("ApprovedQuantity", string.Join(" | ", await AuditOf(fx, "StoreRequisitionLine")));
        Assert.Contains("IssuedQuantity: 0 -> 50", string.Join(" | ", await AuditOf(fx, "StoreRequisitionLine")));
        Assert.Contains("Status:", string.Join(" | ", await AuditOf(fx, "MaterialReservation")));
        Assert.True(await fx.Db.AuditLogs.AnyAsync(a => a.EntityName == "InventoryTransaction" && a.Action == "Insert"));
        Assert.True(await fx.Db.AuditLogs.AnyAsync(a => a.EntityName == "StoreRequisitionStatusHistory" && a.Action == "Insert"));
    }

    [Theory]
    [InlineData("Draft", "Rejected")] [InlineData("Submitted", "Rejected")] [InlineData("Submitted", "Cancelled")]
    public async Task AuditLog_CoversRejectAndCancel(string from, string to)
    {
        var fx = await NewAsync();
        if (from != "Draft") await fx.Svc.AdvanceStatusAsync(fx.ReqId, "Submitted", null, As(Requester, Wm));
        Assert.True((await fx.Svc.AdvanceStatusAsync(fx.ReqId, to, "why", As(Manager, Wm))).Success);
        Assert.Contains($"Status: {from} -> {to}", string.Join(" | ", await AuditOf(fx, "StoreRequisition")));
        Assert.Equal("why", (await fx.Db.StoreRequisitionStatusHistories.AsNoTracking().OrderBy(h => h.StoreRequisitionStatusHistoryId).LastAsync()).Reason);
    }

    [Fact]
    public async Task NoSecondAuditFramework_ServiceWritesNoCustomAuditTable()
    {
        // سرویس فقط از طریق DbContext می‌نویسد؛ تنها منبع ممیزی AuditLog است (+ تاریخچه‌ی وضعیت و تراکنش‌های موجودی به‌عنوان زمینه‌ی کسب‌وکاری)
        var types = typeof(StoreRequisitionLifecycleService).Assembly.GetTypes()
            .Where(t => t.Name.Contains("Audit", StringComparison.OrdinalIgnoreCase) && t.Name.Contains("StoreRequisition", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(types);
        await Task.CompletedTask;
    }

    // ------------------------------------------------------------------ ردیابی بدون حدس + legacy

    [Fact]
    public async Task Traceability_ForwardAndReverse_UsesOnlyStoredKeys_LegacyTransactionsStayUnresolved()
    {
        var fx = await NewAsync();
        var wo = new WorkOrder { WorkOrderNumber = "WO-T", WorkOrderType = "Corrective", EquipmentId = 1 };
        fx.Db.WorkOrders.Add(wo);
        await fx.Db.SaveChangesAsync();
        (await fx.Db.StoreRequisitions.SingleAsync()).WorkOrderId = wo.WorkOrderId;
        await fx.Db.SaveChangesAsync();

        await fx.Svc.AdvanceStatusAsync(fx.ReqId, "Submitted", null, As(Requester, Wm));
        await fx.Svc.AdvanceStatusAsync(fx.ReqId, "Approved", null, As(Manager, Wm));
        await fx.Svc.AdvanceStatusAsync(fx.ReqId, "Reserved", null, As(Keeper, Sk));
        await fx.Svc.IssueAsync(fx.ReqId, null, null, As(Keeper, Sk));

        // legacy: یک تراکنش Issue قدیمی که فقط ReferenceId دارد (بدون Line/Reservation)
        fx.Db.InventoryTransactions.Add(new InventoryTransaction { MaterialItemId = Item, WarehouseId = Wh, TransactionType = "Issue", Quantity = 1, ReferenceType = "StoreRequisition", ReferenceId = fx.ReqId, WorkOrderId = wo.WorkOrderId });
        await fx.Db.SaveChangesAsync();

        // forward: WO → requisition → line → reservation → transaction
        var req = await fx.Db.StoreRequisitions.AsNoTracking().SingleAsync(r => r.WorkOrderId == wo.WorkOrderId);
        var line = await fx.Db.StoreRequisitionLines.AsNoTracking().SingleAsync(l => l.StoreRequisitionId == req.StoreRequisitionId);
        var reservation = await fx.Db.MaterialReservations.AsNoTracking().SingleAsync(r => r.StoreRequisitionLineId == line.StoreRequisitionLineId);
        var issue = await fx.Db.InventoryTransactions.AsNoTracking().SingleAsync(t => t.MaterialReservationId == reservation.MaterialReservationId && t.TransactionType == "Issue");

        // reverse: transaction → reservation → line → requisition → WO
        var rRes = await fx.Db.MaterialReservations.AsNoTracking().SingleAsync(r => r.MaterialReservationId == issue.MaterialReservationId);
        var rLine = await fx.Db.StoreRequisitionLines.AsNoTracking().SingleAsync(l => l.StoreRequisitionLineId == rRes.StoreRequisitionLineId);
        var rReq = await fx.Db.StoreRequisitions.AsNoTracking().SingleAsync(r => r.StoreRequisitionId == rLine.StoreRequisitionId);
        Assert.Equal(wo.WorkOrderId, rReq.WorkOrderId);
        Assert.Equal(issue.StoreRequisitionLineId, rLine.StoreRequisitionLineId);

        // legacy تراکنش بدون Line/Reservation صریحاً حل‌نشده می‌ماند (حدس با کالا/مقدار/تاریخ ممنوع)
        var unresolved = await fx.Db.InventoryTransactions.AsNoTracking()
            .Where(t => t.ReferenceType == "StoreRequisition" && (t.StoreRequisitionLineId == null || t.MaterialReservationId == null)).ToListAsync();
        Assert.Single(unresolved);
        Assert.Null(unresolved[0].StoreRequisitionLineId);
    }
}
