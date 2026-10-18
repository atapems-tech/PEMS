using Microsoft.EntityFrameworkCore;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;
using static PEMS.Tests.Inventory.StoreRequisitionLifecycleServiceTests;

namespace PEMS.Tests.Inventory;

/// <summary>Phase 6.5: مجوز ریزدانه، هویت کارمند، Idempotency، ثبات مقدار تأییدشده، ردیابی کامل، ممیزی و Atomicity شکست.</summary>
public class StoreRequisitionHardeningTests
{
    private const int Requester = 1, Approver = 2, Issuer = 3, Inactive = 4, Wh = 1, Item = 10;

    private sealed class Fx
    {
        public required PEMSDbContext Db { get; init; }
        public required StoreRequisitionLifecycleService Svc { get; init; }
        public required MapActions Actions { get; init; }
        public int ReqId, Line;
    }

    private static async Task<Fx> NewAsync(decimal onHand = 100, decimal qty = 100, bool withWorkOrder = true)
    {
        var db = PemsDbContextFactory.CreateInMemory("srh_" + Guid.NewGuid().ToString("N"));
        db.Warehouses.Add(new Warehouse { WarehouseId = Wh, Code = "W", Name = "W" });
        foreach (var id in new[] { Requester, Approver, Issuer }) db.Employees.Add(new Employee { EmployeeId = id, FirstName = "E" + id, LastName = "T" });
        db.Employees.Add(new Employee { EmployeeId = Inactive, FirstName = "Gone", LastName = "T", IsActive = false });
        db.MaterialItems.Add(new MaterialItem { MaterialItemId = Item, MaterialCode = "A", Description = "A" });
        db.MaterialStocks.Add(new MaterialStock { MaterialItemId = Item, WarehouseId = Wh, OnHandQuantity = onHand });
        WorkOrder? wo = null;
        if (withWorkOrder) { wo = new WorkOrder { WorkOrderNumber = "WO-9", WorkOrderType = "Corrective", EquipmentId = 1 }; db.WorkOrders.Add(wo); }
        await db.SaveChangesAsync();

        var req = new StoreRequisition { RequisitionNumber = "SR-H", WarehouseId = Wh, RequestedById = Requester, Status = StoreRequisitionStatuses.Draft, WorkOrderId = wo?.WorkOrderId };
        req.Lines.Add(new StoreRequisitionLine { MaterialItemId = Item, RequestedQuantity = qty });
        db.StoreRequisitions.Add(req);
        db.InventoryTransactions.Add(new InventoryTransaction { MaterialItemId = Item, WarehouseId = Wh, TransactionType = "Receipt", Quantity = onHand });
        await db.SaveChangesAsync();

        var actions = new MapActions();
        return new Fx { Db = db, Actions = actions, Svc = new StoreRequisitionLifecycleService(db, actions), ReqId = req.StoreRequisitionId, Line = req.Lines.Single().StoreRequisitionLineId };
    }

    private static async Task<Fx> ReservedAsync(decimal onHand = 100, decimal qty = 100, decimal? approved = null)
    {
        var fx = await NewAsync(onHand, qty);
        Assert.True((await fx.Svc.AdvanceStatusAsync(fx.ReqId, "Submitted", null, A(Requester))).Success);
        if (approved.HasValue) Assert.True((await fx.Svc.SetApprovedQuantitiesAsync(fx.ReqId, new Dictionary<int, decimal> { [fx.Line] = approved.Value }, A(Approver))).Success);
        Assert.True((await fx.Svc.AdvanceStatusAsync(fx.ReqId, "Approved", null, A(Approver))).Success);
        var r = await fx.Svc.AdvanceStatusAsync(fx.ReqId, "Reserved", null, A(Approver));
        Assert.True(r.Success, r.Error);
        return fx;
    }

    private static async Task<int[]> Snapshot(Fx fx) => new[]
    {
        await fx.Db.MaterialReservations.CountAsync(), await fx.Db.InventoryTransactions.CountAsync(),
        await fx.Db.StoreRequisitionStatusHistories.CountAsync(), (int)(await fx.Db.MaterialStocks.SumAsync(s => s.ReservedQuantity)),
        (int)(await fx.Db.MaterialStocks.SumAsync(s => s.OnHandQuantity)), (int)(await fx.Db.StoreRequisitionLines.SumAsync(l => l.IssuedQuantity))
    };

    // ------------------------------------------------------------------ دقیقاً سناریوی دستورالعمل

    [Fact]
    public async Task Scenario_Requested100_Approved70_Issue50_Remaining20_ThenFinal20_ThenOneMoreFails()
    {
        var fx = await ReservedAsync(onHand: 200, qty: 100, approved: 70);
        var res = await fx.Db.MaterialReservations.AsNoTracking().SingleAsync();
        Assert.Equal(70, res.Quantity);
        Assert.Equal(70, (await fx.Db.MaterialStocks.AsNoTracking().SingleAsync()).ReservedQuantity);

        Assert.True((await fx.Svc.IssueAsync(fx.ReqId, new Dictionary<int, decimal> { [fx.Line] = 50 }, null, A(Issuer))).Success);
        var line = await fx.Db.StoreRequisitionLines.AsNoTracking().SingleAsync();
        Assert.Equal((70m, 50m), (line.ApprovedQuantity, line.IssuedQuantity));
        var stock = await fx.Db.MaterialStocks.AsNoTracking().SingleAsync();
        Assert.Equal(20, stock.ReservedQuantity);                       // باقی‌مانده‌ی رزرو
        Assert.Equal("PartiallyIssued", (await fx.Db.StoreRequisitions.AsNoTracking().SingleAsync()).Status);

        Assert.True((await fx.Svc.IssueAsync(fx.ReqId, new Dictionary<int, decimal> { [fx.Line] = 20 }, null, A(Issuer))).Success);
        line = await fx.Db.StoreRequisitionLines.AsNoTracking().SingleAsync();
        stock = await fx.Db.MaterialStocks.AsNoTracking().SingleAsync();
        Assert.Equal(70, line.IssuedQuantity);
        Assert.Equal((130m, 0m), (stock.OnHandQuantity, stock.ReservedQuantity));
        Assert.Equal("Issued", (await fx.Db.StoreRequisitions.AsNoTracking().SingleAsync()).Status);

        var snapshot = await Snapshot(fx);
        Assert.False((await fx.Svc.IssueAsync(fx.ReqId, new Dictionary<int, decimal> { [fx.Line] = 1 }, null, A(Issuer))).Success);
        Assert.Equal(snapshot, await Snapshot(fx));                      // شکست = بدون تغییر
    }

    [Fact]
    public async Task ApprovedQuantity_CannotBeChanged_AfterApproval_Reservation_OrIssue()
    {
        var fx = await ReservedAsync(onHand: 200, qty: 100, approved: 70);
        Assert.True((await fx.Svc.IssueAsync(fx.ReqId, new Dictionary<int, decimal> { [fx.Line] = 50 }, null, A(Issuer))).Success);
        var snapshot = await Snapshot(fx);

        foreach (var q in new[] { 40m, 60m, 100m, 70m })
        {
            var r = await fx.Svc.SetApprovedQuantitiesAsync(fx.ReqId, new Dictionary<int, decimal> { [fx.Line] = q }, A(Approver));
            Assert.False(r.Success);
        }
        Assert.Equal(70, (await fx.Db.StoreRequisitionLines.AsNoTracking().SingleAsync()).ApprovedQuantity);
        Assert.Equal(snapshot, await Snapshot(fx));
    }

    // ------------------------------------------------------------------ هویت

    [Theory]
    [InlineData(null)] [InlineData(0)] [InlineData(-5)] [InlineData(999)] [InlineData(Inactive)]
    public async Task MissingInvalidOrInactiveEmployee_IsRejectedExplicitly_NeverEmployeeIdZero(int? employeeId)
    {
        var fx = await NewAsync();
        var actor = A(employeeId);
        var snapshot = await Snapshot(fx);

        Assert.False((await fx.Svc.AdvanceStatusAsync(fx.ReqId, "Submitted", null, actor)).Success);
        Assert.False((await fx.Svc.SetApprovedQuantitiesAsync(fx.ReqId, new Dictionary<int, decimal> { [fx.Line] = 1 }, actor)).Success);
        Assert.False((await fx.Svc.IssueAsync(fx.ReqId, null, null, actor)).Success);
        Assert.Equal(snapshot, await Snapshot(fx));
        Assert.Equal("Draft", (await fx.Db.StoreRequisitions.AsNoTracking().SingleAsync()).Status);
    }

    [Fact]
    public async Task NoPersistedRowEverCarriesEmployeeIdZero()
    {
        var fx = await ReservedAsync();
        await fx.Svc.IssueAsync(fx.ReqId, null, null, A(Issuer));
        Assert.DoesNotContain(await fx.Db.MaterialReservations.AsNoTracking().ToListAsync(), r => r.ReservedById <= 0);
        Assert.DoesNotContain(await fx.Db.InventoryTransactions.AsNoTracking().Where(t => t.ReferenceType == "StoreRequisition").ToListAsync(), t => t.EmployeeId is null or <= 0);
        Assert.DoesNotContain(await fx.Db.StoreRequisitionStatusHistories.AsNoTracking().ToListAsync(), h => h.ChangedById is null or <= 0);
    }

    // ------------------------------------------------------------------ مجوز ریزدانه

    [Theory]
    [InlineData("Submitted", StoreRequisitionActions.Submit)]
    public async Task Submit_RequiresItsOwnPermission(string next, string action)
    {
        var fx = await NewAsync();
        fx.Actions.Grants["ONLY_VIEW"] = new() { "View" };
        Assert.False((await fx.Svc.AdvanceStatusAsync(fx.ReqId, next, null, A(Requester, "ONLY_VIEW"))).Success);
        fx.Actions.Grants["ONLY_VIEW"].Add(action);
        Assert.True((await fx.Svc.AdvanceStatusAsync(fx.ReqId, next, null, A(Requester, "ONLY_VIEW"))).Success);
    }

    [Fact]
    public async Task EachSensitiveAction_NeedsExactlyItsPermission_PermissionAloneDoesNotBypassSodOrLifecycle()
    {
        var fx = await NewAsync();
        Assert.True((await fx.Svc.AdvanceStatusAsync(fx.ReqId, "Submitted", null, A(Requester))).Success);

        // مجوز Approve ندارد
        fx.Actions.Grants["NO_APPROVE"] = new(StoreRequisitionActions.Sensitive.Where(a => a != StoreRequisitionActions.Approve));
        Assert.False((await fx.Svc.AdvanceStatusAsync(fx.ReqId, "Approved", null, A(Approver, "NO_APPROVE"))).Success);

        // مجوز دارد ولی خودِ درخواست‌دهنده است ⇒ SoD
        Assert.False((await fx.Svc.AdvanceStatusAsync(fx.ReqId, "Approved", null, A(Requester))).Success);
        // مجوز دارد ولی گذار غیرمجاز است ⇒ جدول گذار
        Assert.False((await fx.Svc.AdvanceStatusAsync(fx.ReqId, "Reserved", null, A(Approver))).Success);

        Assert.True((await fx.Svc.AdvanceStatusAsync(fx.ReqId, "Approved", null, A(Approver))).Success);
        Assert.True((await fx.Svc.AdvanceStatusAsync(fx.ReqId, "Reserved", null, A(Approver))).Success);

        fx.Actions.Grants["NO_ISSUE"] = new(StoreRequisitionActions.Sensitive.Where(a => a != StoreRequisitionActions.Issue));
        Assert.False((await fx.Svc.IssueAsync(fx.ReqId, null, null, A(Issuer, "NO_ISSUE"))).Success);
        Assert.False((await fx.Svc.AdvanceStatusAsync(fx.ReqId, "Issued", null, A(Issuer, "NO_ISSUE"))).Success);
        Assert.True((await fx.Svc.IssueAsync(fx.ReqId, null, null, A(Issuer))).Success);
    }

    [Fact]
    public async Task CloseShortAndNormalClose_UseDifferentPermissions()
    {
        var fx = await ReservedAsync(onHand: 100, qty: 10);
        await fx.Svc.IssueAsync(fx.ReqId, new Dictionary<int, decimal> { [fx.Line] = 4 }, null, A(Issuer));

        fx.Actions.Grants["CLOSE_ONLY"] = new() { StoreRequisitionActions.Close };
        Assert.False((await fx.Svc.AdvanceStatusAsync(fx.ReqId, "Closed", "short", A(Issuer, "CLOSE_ONLY"))).Success);   // نیاز به CloseShort
        fx.Actions.Grants["CLOSE_ONLY"].Add(StoreRequisitionActions.CloseShort);
        Assert.True((await fx.Svc.AdvanceStatusAsync(fx.ReqId, "Closed", "short", A(Issuer, "CLOSE_ONLY"))).Success);
        var history = await fx.Db.StoreRequisitionStatusHistories.AsNoTracking().OrderBy(h => h.StoreRequisitionStatusHistoryId).LastAsync();
        Assert.StartsWith("CloseShort:", history.Reason);
        Assert.Contains("issued 4", history.Reason);
    }

    [Fact]
    public async Task UserWithoutAnyRole_CannotDoAnything()
    {
        var fx = await NewAsync();
        var snapshot = await Snapshot(fx);
        Assert.False((await fx.Svc.AdvanceStatusAsync(fx.ReqId, "Submitted", null, new StoreRequisitionActor(Requester, Array.Empty<string>()))).Success);
        Assert.Equal(snapshot, await Snapshot(fx));
    }

    // ------------------------------------------------------------------ Idempotency

    [Fact]
    public async Task RetryWithSameRequestKey_DoesNotDuplicateIssue()
    {
        var fx = await ReservedAsync(onHand: 100, qty: 10);
        var key = Guid.NewGuid().ToString("N");
        Assert.True((await fx.Svc.IssueAsync(fx.ReqId, new Dictionary<int, decimal> { [fx.Line] = 4 }, null, A(Issuer), key)).Success);
        var snapshot = await Snapshot(fx);

        var retry = await fx.Svc.IssueAsync(fx.ReqId, new Dictionary<int, decimal> { [fx.Line] = 4 }, null, A(Issuer), key);
        Assert.True(retry.Success);                                       // پاسخ موفق، بدون اثر تکراری
        Assert.Equal(snapshot, await Snapshot(fx));
        Assert.Equal(1, await fx.Db.InventoryTransactions.CountAsync(t => t.TransactionType == "Issue"));
        Assert.Equal(4, (await fx.Db.StoreRequisitionLines.AsNoTracking().SingleAsync()).IssuedQuantity);

        // کلید جدید = تحویل واقعی دوم
        Assert.True((await fx.Svc.IssueAsync(fx.ReqId, new Dictionary<int, decimal> { [fx.Line] = 3 }, null, A(Issuer), Guid.NewGuid().ToString("N"))).Success);
        Assert.Equal(7, (await fx.Db.StoreRequisitionLines.AsNoTracking().SingleAsync()).IssuedQuantity);
    }

    [Fact]
    public async Task RetryWithSameRequestKey_DoesNotDuplicateReservationOrApproval()
    {
        var fx = await NewAsync();
        var k1 = "k-submit"; var k2 = "k-approve"; var k3 = "k-reserve";
        await fx.Svc.AdvanceStatusAsync(fx.ReqId, "Submitted", null, A(Requester), k1);
        await fx.Svc.AdvanceStatusAsync(fx.ReqId, "Approved", null, A(Approver), k2);
        await fx.Svc.AdvanceStatusAsync(fx.ReqId, "Reserved", null, A(Approver), k3);
        var snapshot = await Snapshot(fx);
        foreach (var (to, key, who) in new[] { ("Submitted", k1, Requester), ("Approved", k2, Approver), ("Reserved", k3, Approver) })
            Assert.True((await fx.Svc.AdvanceStatusAsync(fx.ReqId, to, null, A(who), key)).Success);
        Assert.Equal(snapshot, await Snapshot(fx));
        Assert.Equal(1, await fx.Db.MaterialReservations.CountAsync());
    }

    // ------------------------------------------------------------------ ردیابی دوطرفه

    [Fact]
    public async Task Trace_WorkOrder_To_Issue_And_Back()
    {
        var fx = await ReservedAsync(onHand: 100, qty: 10);
        await fx.Svc.IssueAsync(fx.ReqId, null, null, A(Issuer));
        var woId = (await fx.Db.WorkOrders.AsNoTracking().SingleAsync()).WorkOrderId;

        // WO → Requisition → Line → Reservation → Issue transaction
        var req = await fx.Db.StoreRequisitions.AsNoTracking().SingleAsync(r => r.WorkOrderId == woId);
        var line = await fx.Db.StoreRequisitionLines.AsNoTracking().SingleAsync(l => l.StoreRequisitionId == req.StoreRequisitionId);
        var reservation = await fx.Db.MaterialReservations.AsNoTracking().SingleAsync(r => r.StoreRequisitionLineId == line.StoreRequisitionLineId);
        var issue = await fx.Db.InventoryTransactions.AsNoTracking().SingleAsync(t => t.MaterialReservationId == reservation.MaterialReservationId && t.TransactionType == "Issue");
        Assert.Equal(10, issue.Quantity);

        // Issue → Line → Requisition → WO (فقط با کلیدهای خودِ تراکنش)
        var backLine = await fx.Db.StoreRequisitionLines.AsNoTracking().SingleAsync(l => l.StoreRequisitionLineId == issue.StoreRequisitionLineId);
        var backReq = await fx.Db.StoreRequisitions.AsNoTracking().SingleAsync(r => r.StoreRequisitionId == backLine.StoreRequisitionId);
        Assert.Equal(woId, backReq.WorkOrderId);
        Assert.Equal(woId, issue.WorkOrderId);
        Assert.Equal(req.StoreRequisitionId, issue.ReferenceId);
    }

    [Fact]
    public async Task EveryMovement_CarriesLineAndReservation()
    {
        var fx = await ReservedAsync(onHand: 100, qty: 10);
        await fx.Svc.IssueAsync(fx.ReqId, new Dictionary<int, decimal> { [fx.Line] = 4 }, null, A(Issuer));
        await fx.Svc.AdvanceStatusAsync(fx.ReqId, "Closed", "short", A(Issuer));
        foreach (var t in await fx.Db.InventoryTransactions.AsNoTracking().Where(t => t.ReferenceType == "StoreRequisition").ToListAsync())
        {
            Assert.Equal(fx.Line, t.StoreRequisitionLineId);
            Assert.NotNull(t.MaterialReservationId);
        }
        var types = await fx.Db.InventoryTransactions.AsNoTracking().Where(t => t.ReferenceType == "StoreRequisition").Select(t => t.TransactionType).ToListAsync();
        Assert.Contains("ReservationRelease", types);
    }

    // ------------------------------------------------------------------ ممیزی

    [Fact]
    public async Task AuditLog_RecordsStockQuantityChanges_Reservations_AndApprovedQuantity()
    {
        var fx = await NewAsync(onHand: 100, qty: 10);
        await fx.Svc.AdvanceStatusAsync(fx.ReqId, "Submitted", null, A(Requester));
        await fx.Svc.SetApprovedQuantitiesAsync(fx.ReqId, new Dictionary<int, decimal> { [fx.Line] = 8 }, A(Approver));
        await fx.Svc.AdvanceStatusAsync(fx.ReqId, "Approved", null, A(Approver));
        await fx.Svc.AdvanceStatusAsync(fx.ReqId, "Reserved", null, A(Approver));
        await fx.Svc.IssueAsync(fx.ReqId, new Dictionary<int, decimal> { [fx.Line] = 5 }, null, A(Issuer));

        var audit = await fx.Db.AuditLogs.AsNoTracking().ToListAsync();
        string Summary(string entity) => string.Join(" || ", audit.Where(a => a.EntityName == entity).Select(a => a.Action + ":" + a.ChangedFieldsSummary));
        Assert.Contains("ApprovedQuantity", Summary("StoreRequisitionLine"));
        Assert.Contains("ReservedQuantity: 0 -> 8", Summary("MaterialStock"));
        Assert.Contains("OnHandQuantity: 100 -> 95", Summary("MaterialStock"));
        Assert.Contains("Insert", Summary("MaterialReservation"));
        Assert.Contains("IssuedQuantity", Summary("MaterialReservation"));
        Assert.Contains("Status: Submitted -> Approved", Summary("StoreRequisition"));

        // ریشه‌ی کسب‌وکاری: تاریخچه‌ی وضعیت (+ دلیل/مقدار) و تراکنش‌ها
        var history = await fx.Db.StoreRequisitionStatusHistories.AsNoTracking().Select(h => h.ToStatus + "|" + h.Reason).ToListAsync();
        Assert.Contains(history, h => h.Contains("ApprovedQuantity: line"));
        Assert.Contains(history, h => h.StartsWith("PartiallyIssued|") && h.Contains("+5"));
    }

    // ------------------------------------------------------------------ Atomicity شکست

    [Fact]
    public async Task FailedOperations_LeaveNoPartialMutation()
    {
        var fx = await NewAsync(onHand: 5, qty: 10);            // کمبود موجودی
        await fx.Svc.AdvanceStatusAsync(fx.ReqId, "Submitted", null, A(Requester));
        await fx.Svc.AdvanceStatusAsync(fx.ReqId, "Approved", null, A(Approver));
        var snapshot = await Snapshot(fx);
        var history = await fx.Db.StoreRequisitionStatusHistories.CountAsync();

        Assert.False((await fx.Svc.AdvanceStatusAsync(fx.ReqId, "Reserved", null, A(Approver))).Success);   // موجودی کم
        Assert.Equal(snapshot, await Snapshot(fx));
        Assert.Equal(history, await fx.Db.StoreRequisitionStatusHistories.CountAsync());
        Assert.Equal("Approved", (await fx.Db.StoreRequisitions.AsNoTracking().SingleAsync()).Status);
    }

    [Fact]
    public async Task OverIssueInSecondLine_RollsBackTheFirstLineIssue()
    {
        var db = PemsDbContextFactory.CreateInMemory("srh2_" + Guid.NewGuid().ToString("N"));
        db.Warehouses.Add(new Warehouse { WarehouseId = Wh, Code = "W", Name = "W" });
        foreach (var id in new[] { Requester, Approver, Issuer }) db.Employees.Add(new Employee { EmployeeId = id, FirstName = "E", LastName = "T" });
        db.MaterialItems.AddRange(new MaterialItem { MaterialItemId = 10, MaterialCode = "A", Description = "A" }, new MaterialItem { MaterialItemId = 11, MaterialCode = "B", Description = "B" });
        db.MaterialStocks.AddRange(new MaterialStock { MaterialItemId = 10, WarehouseId = Wh, OnHandQuantity = 50 }, new MaterialStock { MaterialItemId = 11, WarehouseId = Wh, OnHandQuantity = 50 });
        var req = new StoreRequisition { RequisitionNumber = "SR-2L", WarehouseId = Wh, RequestedById = Requester };
        req.Lines.Add(new StoreRequisitionLine { MaterialItemId = 10, RequestedQuantity = 10 });
        req.Lines.Add(new StoreRequisitionLine { MaterialItemId = 11, RequestedQuantity = 10 });
        db.StoreRequisitions.Add(req);
        await db.SaveChangesAsync();
        var actions = new MapActions();
        var svc = new StoreRequisitionLifecycleService(db, actions);
        foreach (var (to, who) in new[] { ("Submitted", Requester), ("Approved", Approver), ("Reserved", Approver) })
            Assert.True((await svc.AdvanceStatusAsync(req.StoreRequisitionId, to, null, A(who))).Success);
        var lines = req.Lines.OrderBy(l => l.StoreRequisitionLineId).ToList();

        var before = (await db.MaterialStocks.AsNoTracking().SumAsync(s => s.OnHandQuantity), await db.InventoryTransactions.CountAsync());
        var r = await svc.IssueAsync(req.StoreRequisitionId, new Dictionary<int, decimal> { [lines[0].StoreRequisitionLineId] = 5, [lines[1].StoreRequisitionLineId] = 11 }, null, A(Issuer));
        Assert.False(r.Success);
        Assert.Equal(before, (await db.MaterialStocks.AsNoTracking().SumAsync(s => s.OnHandQuantity), await db.InventoryTransactions.CountAsync()));
        Assert.Equal(0, await db.StoreRequisitionLines.AsNoTracking().SumAsync(l => l.IssuedQuantity));
    }

    // ------------------------------------------------------------------ مسیرهای مستقیم جدول گذار (دستورالعمل §4)

    [Fact]
    public async Task DirectServiceCalls_CannotSkipOrRevive_AnyStatus()
    {
        var fx = await NewAsync();
        foreach (var bad in new[] { "Reserved", "Issued", "Closed", "Approved" })
            Assert.False((await fx.Svc.AdvanceStatusAsync(fx.ReqId, bad, null, A(Approver))).Success, "Draft→" + bad);

        await fx.Svc.AdvanceStatusAsync(fx.ReqId, "Submitted", null, A(Requester));
        Assert.False((await fx.Svc.AdvanceStatusAsync(fx.ReqId, "Issued", null, A(Issuer))).Success);

        await fx.Svc.AdvanceStatusAsync(fx.ReqId, "Approved", null, A(Approver));
        Assert.False((await fx.Svc.AdvanceStatusAsync(fx.ReqId, "Issued", null, A(Issuer))).Success);

        await fx.Svc.AdvanceStatusAsync(fx.ReqId, "Cancelled", "x", A(Approver));
        foreach (var bad in new[] { "Reserved", "Approved", "Submitted", "Draft", "Closed" })
            Assert.False((await fx.Svc.AdvanceStatusAsync(fx.ReqId, bad, null, A(Approver))).Success, "Cancelled→" + bad);
    }

    [Fact]
    public async Task ReservedToClosed_IsAFullShortClose_ReleasingEverything_AndNeedsCloseShortPermission()
    {
        var fx = await ReservedAsync(onHand: 100, qty: 10);
        fx.Actions.Grants["CLOSE_ONLY"] = new() { StoreRequisitionActions.Close };
        Assert.False((await fx.Svc.AdvanceStatusAsync(fx.ReqId, "Closed", "nothing needed", A(Issuer, "CLOSE_ONLY"))).Success);

        Assert.True((await fx.Svc.AdvanceStatusAsync(fx.ReqId, "Closed", "nothing needed", A(Issuer))).Success);
        var stock = await fx.Db.MaterialStocks.AsNoTracking().SingleAsync();
        Assert.Equal((100m, 0m), (stock.OnHandQuantity, stock.ReservedQuantity));
        Assert.Equal("Closed", (await fx.Db.StoreRequisitions.AsNoTracking().SingleAsync()).Status);
        Assert.StartsWith("CloseShort:", (await fx.Db.StoreRequisitionStatusHistories.AsNoTracking().OrderBy(h => h.StoreRequisitionStatusHistoryId).LastAsync()).Reason);
    }
}
