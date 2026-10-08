using Microsoft.EntityFrameworkCore;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Inventory;

/// <summary>
/// StoreRequisition (Indent داخلی): جدول گذار، رزرو Idempotent/خط‌به‌خط، مقدار تأییدشده، تحویل بخشی، SoD، آزادسازی و ناوردای موجودی.
/// هر تغییر موجودی باید یک InventoryTransaction داشته باشد.
/// </summary>
public class StoreRequisitionLifecycleServiceTests
{
    private const int Requester = 1, Approver = 2, Issuer = 3, Wh = 1, ItemA = 10, ItemB = 11;

    /// <summary>جدول مجوز تستی: نقش → عمل‌های StoreRequisition.</summary>
    internal sealed class MapActions : IRoleModuleActionService
    {
        public Dictionary<string, HashSet<string>> Grants { get; } = new() { ["SRADM"] = new(StoreRequisitionActions.Sensitive) };
        public Task<bool> HasActionAsync(IEnumerable<string> userRoles, string module, string action) =>
            Task.FromResult(module == StoreRequisitionActions.Module && userRoles.Any(r => Grants.TryGetValue(r, out var g) && g.Contains(action)));
        public Task SeedDefaultsAsync() => Task.CompletedTask;
    }

    internal static StoreRequisitionActor A(int? employeeId, params string[] roles) =>
        new(employeeId, roles.Length == 0 ? new[] { "SRADM" } : roles);

    private sealed class Fx
    {
        public required PEMSDbContext Db { get; init; }
        public required StoreRequisitionLifecycleService Svc { get; init; }
        public required MapActions Actions { get; init; }
        public int ReqId, Line1, Line2;

        public Task<(bool Success, string? Error)> Go(string to, int by = Approver, string? reason = null) => Svc.AdvanceStatusAsync(ReqId, to, reason, A(by));
        public async Task<MaterialStock> Stock(int item = ItemA) => await Db.MaterialStocks.AsNoTracking().SingleAsync(s => s.MaterialItemId == item);
        public async Task<StoreRequisition> Req() => await Db.StoreRequisitions.AsNoTracking().Include(r => r.Lines).SingleAsync(r => r.StoreRequisitionId == ReqId);

        /// ناوردا: Reserved ≤ OnHand، مقادیر ≥ 0، و موجودی = Σ تراکنش‌ها (Receipt/Adjustment − Issue)
        public async Task AssertInvariants()
        {
            foreach (var s in await Db.MaterialStocks.AsNoTracking().ToListAsync())
            {
                Assert.True(s.OnHandQuantity >= 0 && s.ReservedQuantity >= 0 && s.ReservedQuantity <= s.OnHandQuantity, $"stock invariant broken for {s.MaterialItemId}");
                var tx = await Db.InventoryTransactions.AsNoTracking().Where(t => t.MaterialItemId == s.MaterialItemId).ToListAsync();
                var reserved = tx.Where(t => t.TransactionType == "Reservation").Sum(t => t.Quantity)
                               - tx.Where(t => t.TransactionType == "ReservationRelease").Sum(t => t.Quantity)
                               - tx.Where(t => t.TransactionType == "Issue").Sum(t => t.Quantity);
                Assert.Equal(reserved, s.ReservedQuantity);
            }
            foreach (var l in (await Req()).Lines)
                Assert.True(l.IssuedQuantity <= (l.ApprovedQuantity ?? l.RequestedQuantity) && (l.ApprovedQuantity ?? 0) <= l.RequestedQuantity);
        }
    }

    private static async Task<Fx> NewAsync(decimal onHand = 100, decimal q1 = 5, decimal q2 = 7, bool sameMaterial = true)
    {
        var db = PemsDbContextFactory.CreateInMemory("sr_" + Guid.NewGuid().ToString("N"));
        db.Warehouses.Add(new Warehouse { WarehouseId = Wh, Code = "W1", Name = "Main" });
        foreach (var id in new[] { Requester, Approver, Issuer }) db.Employees.Add(new Employee { EmployeeId = id, FirstName = "E" + id, LastName = "T" });
        db.MaterialItems.AddRange(new MaterialItem { MaterialItemId = ItemA, MaterialCode = "A", Description = "A" }, new MaterialItem { MaterialItemId = ItemB, MaterialCode = "B", Description = "B" });
        db.MaterialStocks.AddRange(
            new MaterialStock { MaterialItemId = ItemA, WarehouseId = Wh, OnHandQuantity = onHand },
            new MaterialStock { MaterialItemId = ItemB, WarehouseId = Wh, OnHandQuantity = 50 });
        var req = new StoreRequisition { RequisitionNumber = "SR-1", WarehouseId = Wh, RequestedById = Requester, Status = StoreRequisitionStatuses.Draft };
        req.Lines.Add(new StoreRequisitionLine { MaterialItemId = ItemA, RequestedQuantity = q1 });
        req.Lines.Add(new StoreRequisitionLine { MaterialItemId = sameMaterial ? ItemA : ItemB, RequestedQuantity = q2 });
        db.StoreRequisitions.Add(req);
        await db.SaveChangesAsync();
        // موجودی اولیه تراکنش دارد (Receipt) تا معادله‌ی ناوردا برقرار باشد
        db.InventoryTransactions.Add(new InventoryTransaction { MaterialItemId = ItemA, WarehouseId = Wh, TransactionType = "Receipt", Quantity = onHand });
        await db.SaveChangesAsync();
        var lines = req.Lines.OrderBy(l => l.StoreRequisitionLineId).ToList();
        var actions = new MapActions();
        return new Fx { Db = db, Actions = actions, Svc = new StoreRequisitionLifecycleService(db, actions), ReqId = req.StoreRequisitionId, Line1 = lines[0].StoreRequisitionLineId, Line2 = lines[1].StoreRequisitionLineId };
    }

    private static async Task ToReserved(Fx fx)
    {
        Assert.True((await fx.Go("Submitted", Requester)).Success);
        Assert.True((await fx.Go("Approved")).Success);
        var r = await fx.Go("Reserved");
        Assert.True(r.Success, r.Error);
    }

    // ------------------------------------------------------------------ gate

    [Theory]
    [InlineData("Draft", "Issued")] [InlineData("Draft", "Approved")] [InlineData("Draft", "Reserved")]
    [InlineData("Submitted", "Reserved")] [InlineData("Submitted", "Closed")] [InlineData("Approved", "Issued")]
    [InlineData("Approved", "Submitted")] [InlineData("Reserved", "Approved")]
    [InlineData("Issued", "Reserved")] [InlineData("Closed", "Cancelled")] [InlineData("Rejected", "Submitted")] [InlineData("Cancelled", "Draft")]
    public void IllegalTransitions_AreRejectedByTheTable(string from, string to) =>
        Assert.False(StoreRequisitionStatuses.CanTransition(from, to));

    [Theory]
    [InlineData("Draft", "Submitted")] [InlineData("Draft", "Rejected")] [InlineData("Draft", "Cancelled")] [InlineData("Submitted", "Approved")]
    [InlineData("Submitted", "Rejected")] [InlineData("Approved", "Reserved")] [InlineData("Approved", "Rejected")] [InlineData("Approved", "Cancelled")]
    [InlineData("Reserved", "Issued")] [InlineData("Reserved", "PartiallyIssued")] [InlineData("PartiallyIssued", "Issued")] [InlineData("PartiallyIssued", "Closed")] [InlineData("Issued", "Closed")] [InlineData("Reserved", "Closed")]
    public void LegalTransitions_AreAllowed(string from, string to) => Assert.True(StoreRequisitionStatuses.CanTransition(from, to));

    [Fact]
    public async Task ServiceRefusesToSkipStatuses_EvenWhenCalledDirectly()
    {
        var fx = await NewAsync();
        var r = await fx.Go("Issued");
        Assert.False(r.Success);
        Assert.Equal("Draft", (await fx.Req()).Status);
        Assert.Empty(await fx.Db.MaterialReservations.ToListAsync());

        Assert.False((await fx.Go("Reserved")).Success);
        Assert.False((await fx.Go("PartiallyIssued")).Success);
    }

    // ------------------------------------------------------------------ happy path + invariants

    [Fact]
    public async Task FullPath_DraftToClosed_MovesStockOnlyThroughTransactions()
    {
        var fx = await NewAsync(sameMaterial: false);
        await ToReserved(fx);
        var afterReserve = await fx.Stock();
        Assert.Equal((100, 5), (afterReserve.OnHandQuantity, afterReserve.ReservedQuantity));
        await fx.AssertInvariants();

        var issue = await fx.Go("Issued", Issuer);
        Assert.True(issue.Success, issue.Error);
        var s = await fx.Stock();
        Assert.Equal((95, 0), (s.OnHandQuantity, s.ReservedQuantity));
        Assert.Equal("Issued", (await fx.Req()).Status);
        Assert.True((await fx.Go("Closed", Issuer)).Success);
        await fx.AssertInvariants();
        Assert.All(await fx.Db.InventoryTransactions.Where(t => t.TransactionType == "Issue" || t.TransactionType == "Reservation").ToListAsync(),
            t => Assert.Equal(fx.ReqId, t.ReferenceId));
    }

    [Fact]
    public async Task IssueTransaction_CarriesTheWorkOrderForTraceability()
    {
        var fx = await NewAsync(sameMaterial: false);
        var wo = new WorkOrder { WorkOrderNumber = "WO-1", WorkOrderType = "Corrective", EquipmentId = 1 };
        fx.Db.WorkOrders.Add(wo);
        await fx.Db.SaveChangesAsync();
        var r = await fx.Db.StoreRequisitions.SingleAsync(); r.WorkOrderId = wo.WorkOrderId;
        await fx.Db.SaveChangesAsync();
        await ToReserved(fx);
        await fx.Go("Issued", Issuer);
        Assert.All(await fx.Db.InventoryTransactions.Where(t => t.ReferenceType == "StoreRequisition").ToListAsync(), t => Assert.Equal(wo.WorkOrderId, t.WorkOrderId));
    }

    // ------------------------------------------------------------------ reservation

    [Fact]
    public async Task Reservation_IsPerLine_EvenForDuplicateMaterialLines()
    {
        var fx = await NewAsync();                       // دو خط هر دو ItemA: 5 و 7
        await ToReserved(fx);
        var res = await fx.Db.MaterialReservations.AsNoTracking().OrderBy(r => r.StoreRequisitionLineId).ToListAsync();
        Assert.Equal(2, res.Count);
        Assert.Equal((fx.Line1, 5m), (res[0].StoreRequisitionLineId!.Value, res[0].Quantity));
        Assert.Equal((fx.Line2, 7m), (res[1].StoreRequisitionLineId!.Value, res[1].Quantity));
        Assert.Equal(12, (await fx.Stock()).ReservedQuantity);
    }

    [Fact]
    public async Task Reserve_WhenStockIsInsufficient_FailsAtomically()
    {
        var fx = await NewAsync(onHand: 10);             // نیاز 12
        Assert.True((await fx.Go("Submitted", Requester)).Success);
        Assert.True((await fx.Go("Approved")).Success);
        var r = await fx.Go("Reserved");
        Assert.False(r.Success);
        Assert.Empty(await fx.Db.MaterialReservations.ToListAsync());
        Assert.Equal(0, (await fx.Stock()).ReservedQuantity);
        Assert.Equal("Approved", (await fx.Req()).Status);
        Assert.DoesNotContain(await fx.Db.InventoryTransactions.ToListAsync(), t => t.TransactionType == "Reservation");
    }

    [Fact]
    public async Task Reserve_CalledTwice_DoesNotDoubleReserve()
    {
        var fx = await NewAsync();
        await ToReserved(fx);
        Assert.False((await fx.Go("Reserved")).Success);                 // گذار Reserved→Reserved مجاز نیست
        Assert.Equal(2, await fx.Db.MaterialReservations.CountAsync());
        Assert.Equal(12, (await fx.Stock()).ReservedQuantity);
    }

    // ------------------------------------------------------------------ approved quantity

    [Fact]
    public async Task ApprovedQuantity_DrivesReservationAndCannotExceedRequested()
    {
        var fx = await NewAsync(q1: 100, q2: 1);
        Assert.True((await fx.Go("Submitted", Requester)).Success);

        Assert.False((await fx.Svc.SetApprovedQuantitiesAsync(fx.ReqId, new Dictionary<int, decimal> { [fx.Line1] = 101 }, A(Approver))).Success);
        Assert.False((await fx.Svc.SetApprovedQuantitiesAsync(fx.ReqId, new Dictionary<int, decimal> { [fx.Line1] = -1 }, A(Approver))).Success);
        Assert.False((await fx.Svc.SetApprovedQuantitiesAsync(fx.ReqId, new Dictionary<int, decimal> { [9999] = 1 }, A(Approver))).Success);
        Assert.False((await fx.Svc.SetApprovedQuantitiesAsync(fx.ReqId, new Dictionary<int, decimal> { [fx.Line1] = 70 }, A(Requester))).Success);   // SoD

        Assert.True((await fx.Svc.SetApprovedQuantitiesAsync(fx.ReqId, new Dictionary<int, decimal> { [fx.Line1] = 70, [fx.Line2] = 0 }, A(Approver))).Success);
        Assert.True((await fx.Go("Approved")).Success);
        Assert.True((await fx.Go("Reserved")).Success);

        Assert.Equal(70, (await fx.Stock()).ReservedQuantity);
        Assert.Equal(70, (await fx.Db.MaterialReservations.SingleAsync()).Quantity);
    }

    [Fact]
    public async Task ApprovedQuantity_DefaultsToRequested_AndMustBePositiveSomewhere()
    {
        var fx = await NewAsync();
        Assert.True((await fx.Go("Submitted", Requester)).Success);
        Assert.True((await fx.Svc.SetApprovedQuantitiesAsync(fx.ReqId, new Dictionary<int, decimal> { [fx.Line1] = 0, [fx.Line2] = 0 }, A(Approver))).Success);
        Assert.False((await fx.Go("Approved")).Success);                 // همه صفر
        Assert.True((await fx.Svc.SetApprovedQuantitiesAsync(fx.ReqId, new Dictionary<int, decimal> { [fx.Line1] = 5 }, A(Approver))).Success);
        Assert.True((await fx.Go("Approved")).Success);
        Assert.All((await fx.Req()).Lines.Where(l => l.StoreRequisitionLineId == fx.Line2), l => Assert.Equal(0, l.ApprovedQuantity));
    }

    // ------------------------------------------------------------------ partial issue

    [Fact]
    public async Task PartialIssue_Requested100_Approved70_Issued50_Remaining20()
    {
        var fx = await NewAsync(q1: 100, q2: 1, sameMaterial: false);
        Assert.True((await fx.Go("Submitted", Requester)).Success);
        Assert.True((await fx.Svc.SetApprovedQuantitiesAsync(fx.ReqId, new Dictionary<int, decimal> { [fx.Line1] = 70, [fx.Line2] = 0 }, A(Approver))).Success);
        Assert.True((await fx.Go("Approved")).Success);
        Assert.True((await fx.Go("Reserved")).Success);

        var first = await fx.Svc.IssueAsync(fx.ReqId, new Dictionary<int, decimal> { [fx.Line1] = 50 }, "first", A(Issuer));
        Assert.True(first.Success, first.Error);
        var req = await fx.Req();
        Assert.Equal("PartiallyIssued", req.Status);
        Assert.Equal(50, req.Lines.Single(l => l.StoreRequisitionLineId == fx.Line1).IssuedQuantity);
        var s = await fx.Stock();
        Assert.Equal((50, 20), (s.OnHandQuantity, s.ReservedQuantity));
        Assert.Equal("PartiallyIssued", (await fx.Db.MaterialReservations.SingleAsync()).Status);
        await fx.AssertInvariants();

        // بیش از باقی‌مانده‌ی رزرو (20) ممکن نیست؛ تغییری ذخیره نمی‌شود
        Assert.False((await fx.Svc.IssueAsync(fx.ReqId, new Dictionary<int, decimal> { [fx.Line1] = 21 }, null, A(Issuer))).Success);
        Assert.Equal(50, (await fx.Req()).Lines.Single(l => l.StoreRequisitionLineId == fx.Line1).IssuedQuantity);

        var rest = await fx.Svc.IssueAsync(fx.ReqId, null, null, A(Issuer));
        Assert.True(rest.Success, rest.Error);
        Assert.Equal("Issued", (await fx.Req()).Status);
        var done = await fx.Stock();
        Assert.Equal((30, 0), (done.OnHandQuantity, done.ReservedQuantity));
        await fx.AssertInvariants();
    }

    [Fact]
    public async Task Issue_PerLine_DoesNotMixDuplicateMaterialLines()
    {
        var fx = await NewAsync();                       // Line1=5, Line2=7 (هر دو ItemA)
        await ToReserved(fx);
        Assert.True((await fx.Svc.IssueAsync(fx.ReqId, new Dictionary<int, decimal> { [fx.Line2] = 7 }, null, A(Issuer))).Success);
        var lines = (await fx.Req()).Lines.OrderBy(l => l.StoreRequisitionLineId).ToList();
        Assert.Equal((0m, 7m), (lines[0].IssuedQuantity, lines[1].IssuedQuantity));
        Assert.Equal("PartiallyIssued", (await fx.Req()).Status);
        await fx.AssertInvariants();
    }

    [Fact]
    public async Task Issue_RejectsNegativeUnknownLineAndNothingToIssue()
    {
        var fx = await NewAsync();
        await ToReserved(fx);
        Assert.False((await fx.Svc.IssueAsync(fx.ReqId, new Dictionary<int, decimal> { [fx.Line1] = -1 }, null, A(Issuer))).Success);
        Assert.False((await fx.Svc.IssueAsync(fx.ReqId, new Dictionary<int, decimal> { [424242] = 1 }, null, A(Issuer))).Success);
        Assert.False((await fx.Svc.IssueAsync(fx.ReqId, new Dictionary<int, decimal> { [fx.Line1] = 0 }, null, A(Issuer))).Success);
        await fx.AssertInvariants();
    }

    [Fact]
    public async Task Issue_RequiresReservedState()
    {
        var fx = await NewAsync();
        Assert.False((await fx.Svc.IssueAsync(fx.ReqId, null, null, A(Issuer))).Success);
    }

    // ------------------------------------------------------------------ SoD

    [Fact]
    public async Task Requester_CannotApproveOrIssueOwnRequisition()
    {
        var fx = await NewAsync(sameMaterial: false);
        Assert.True((await fx.Go("Submitted", Requester)).Success);
        Assert.False((await fx.Go("Approved", Requester)).Success);
        Assert.True((await fx.Go("Approved", Approver)).Success);
        Assert.True((await fx.Go("Reserved", Approver)).Success);
        Assert.False((await fx.Go("Issued", Requester)).Success);
        Assert.False((await fx.Svc.IssueAsync(fx.ReqId, null, null, A(Requester))).Success);
        Assert.True((await fx.Svc.IssueAsync(fx.ReqId, null, null, A(Issuer))).Success);
    }

    // ------------------------------------------------------------------ release paths

    [Fact]
    public async Task CancelFromReserved_ReleasesTheReservation()
    {
        var fx = await NewAsync();
        await ToReserved(fx);
        Assert.True((await fx.Go("Cancelled", Approver, "no longer needed")).Success);
        Assert.Equal(0, (await fx.Stock()).ReservedQuantity);
        Assert.All(await fx.Db.MaterialReservations.ToListAsync(), r => Assert.Equal("Released", r.Status));
        Assert.Contains(await fx.Db.InventoryTransactions.ToListAsync(), t => t.TransactionType == "ReservationRelease");
        await fx.AssertInvariants();
    }

    [Theory]
    [InlineData("Submitted")] [InlineData("Approved")] [InlineData("Draft")]
    public async Task RejectAndCancelBeforeReservation_NeedNoStockChange(string upTo)
    {
        var fx = await NewAsync();
        if (upTo != "Draft") Assert.True((await fx.Go("Submitted", Requester)).Success);
        if (upTo == "Approved") Assert.True((await fx.Go("Approved")).Success);
        Assert.True((await fx.Go("Rejected", Approver, "no")).Success);
        Assert.Equal(0, (await fx.Stock()).ReservedQuantity);
        Assert.Empty(await fx.Db.MaterialReservations.ToListAsync());
    }

    [Fact]
    public async Task IssuedRequisition_CannotBeCancelled_OnlyClosed()
    {
        var fx = await NewAsync(sameMaterial: false);
        await ToReserved(fx);
        Assert.True((await fx.Go("Issued", Issuer)).Success);
        Assert.False((await fx.Go("Cancelled", Approver)).Success);
        Assert.True((await fx.Go("Closed", Approver)).Success);
    }

    [Fact]
    public async Task CloseShort_FromPartiallyIssued_ReleasesTheRemainder()
    {
        var fx = await NewAsync(q1: 10, q2: 1, sameMaterial: false);
        await ToReserved(fx);
        Assert.True((await fx.Svc.IssueAsync(fx.ReqId, new Dictionary<int, decimal> { [fx.Line1] = 4 }, null, A(Issuer))).Success);
        Assert.True((await fx.Go("Closed", Issuer, "short close")).Success);

        var s = await fx.Stock();
        Assert.Equal((96, 0), (s.OnHandQuantity, s.ReservedQuantity));
        Assert.Equal("Closed", (await fx.Req()).Status);
        await fx.AssertInvariants();
    }

    [Fact]
    public async Task StatusHistory_IsWrittenForEveryChange_IncludingPartialIssues()
    {
        var fx = await NewAsync(sameMaterial: false);
        await ToReserved(fx);
        await fx.Svc.IssueAsync(fx.ReqId, new Dictionary<int, decimal> { [fx.Line1] = 2 }, null, A(Issuer));
        await fx.Svc.IssueAsync(fx.ReqId, null, null, A(Issuer));
        var history = await fx.Db.StoreRequisitionStatusHistories.AsNoTracking().OrderBy(h => h.StoreRequisitionStatusHistoryId).Select(h => h.FromStatus + ">" + h.ToStatus).ToListAsync();
        Assert.Equal(new[] { "Draft>Submitted", "Submitted>Approved", "Approved>Reserved", "Reserved>PartiallyIssued", "PartiallyIssued>Issued" }, history);
    }

    [Fact]
    public async Task LegacyReservationWithoutLine_IsAttachedWhenUnambiguous_AndRejectedWhenAmbiguous()
    {
        var fx = await NewAsync(sameMaterial: false, q1: 5, q2: 7);       // خط‌ها: ItemA، ItemB (کالاها یکتا)
        Assert.True((await fx.Go("Submitted", Requester)).Success);
        Assert.True((await fx.Go("Approved")).Success);
        // شبیه‌سازی رزرو قبل از مهاجرت: بدون LineId
        fx.Db.MaterialReservations.Add(new MaterialReservation { MaterialItemId = ItemA, WarehouseId = Wh, Quantity = 5, Status = "Active", StoreRequisitionId = fx.ReqId, ReservedById = 1 });
        (await fx.Db.MaterialStocks.SingleAsync(s => s.MaterialItemId == ItemA)).ReservedQuantity = 5;
        var req = await fx.Db.StoreRequisitions.SingleAsync(); req.Status = "Reserved";
        await fx.Db.SaveChangesAsync();

        var issue = await fx.Svc.IssueAsync(fx.ReqId, new Dictionary<int, decimal> { [fx.Line1] = 5 }, null, A(Issuer));
        Assert.True(issue.Success, issue.Error);
        Assert.Equal(fx.Line1, (await fx.Db.MaterialReservations.AsNoTracking().SingleAsync()).StoreRequisitionLineId);

        // مبهم: دو خط ItemA
        var amb = await NewAsync();
        Assert.True((await amb.Go("Submitted", Requester)).Success);
        Assert.True((await amb.Go("Approved")).Success);
        amb.Db.MaterialReservations.Add(new MaterialReservation { MaterialItemId = ItemA, WarehouseId = Wh, Quantity = 5, Status = "Active", StoreRequisitionId = amb.ReqId, ReservedById = 1 });
        (await amb.Db.MaterialStocks.SingleAsync(s => s.MaterialItemId == ItemA)).ReservedQuantity = 5;
        (await amb.Db.StoreRequisitions.SingleAsync()).Status = "Reserved";
        await amb.Db.SaveChangesAsync();
        Assert.False((await amb.Svc.IssueAsync(amb.ReqId, null, null, A(Issuer))).Success);
    }
}
