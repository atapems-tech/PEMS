using Microsoft.EntityFrameworkCore;
using PEMS.Application.Inventory;
using PEMS.Application.Replenishment;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;
using static PEMS.Tests.Inventory.ReplenishmentTestKit;

namespace PEMS.Tests.Inventory;

/// <summary>
/// Phase 2 / P5 (InMemory) - چرخه‌ی عمر توصیه، Idempotency، قاعده‌ی رد/بازگشایی، قطعی‌نبودن، مجوزها، پوشاندن منبع و «فقط‌خواندنیِ موجودی/تقاضا».
/// جعلی‌کردن P3 فقط برای ساخت سناریوهاست؛ نتایج با NetDemandCalculator واقعی ساخته می‌شوند.
/// </summary>
public class ReplenishmentRecommendationServiceTests
{
    private sealed class Env
    {
        public required PEMSDbContext Db { get; init; }
        public required FakeNetDemand Demand { get; init; }
        public required Perms Perms { get; init; }
        public required ReplenishmentRecommendationService Svc { get; init; }
    }

    private static async Task<Env> NewAsync(Perms? perms = null)
    {
        var db = PemsDbContextFactory.CreateInMemory("repl_" + Guid.NewGuid().ToString("N"));
        db.Warehouses.AddRange(new Warehouse { Code = "WH-1", Name = "Main" }, new Warehouse { Code = "WH-2", Name = "Second" });
        db.MaterialItems.AddRange(new MaterialItem { MaterialCode = "M-1", Description = "Bearing" }, new MaterialItem { MaterialCode = "M-2", Description = "Seal" });
        await db.SaveChangesAsync();
        var demand = new FakeNetDemand();
        perms ??= StandardPerms();
        return new Env { Db = db, Demand = demand, Perms = perms, Svc = new ReplenishmentRecommendationService(db, demand, perms) };
    }

    private static Task<ReplenishmentRunResult> RunAsync(Env e, string? key = null, IReadOnlyCollection<int>? items = null)
    {
        e.Db.ChangeTracker.Clear();
        return e.Svc.RunAsync(new ReplenishmentRunRequest(items, null, key), Full);
    }

    private static Task<ReplenishmentRecommendation> LiveAsync(Env e, int item = 1, int wh = 1) =>
        e.Db.ReplenishmentRecommendations.AsNoTracking().SingleAsync(r => r.MaterialItemId == item && r.WarehouseId == wh && (r.Status == "Open" || r.Status == "Dismissed"));

    // ───────── ایجاد / Idempotency / Revision ─────────

    [Fact]
    public async Task Run_CreatesAnOpenRecommendation_WithSourcesHistoryAndRunCounters()
    {
        var e = await NewAsync();
        e.Demand.Results = new() { Result(1, 1, Stock(1, 1, 5, 2), SrLine(10, 1, 1, 8), SrLine(11, 1, 1, 2)) };   // OnHand 5, Reserved 2 ⇒ Usable 3

        var run = await RunAsync(e);

        Assert.True(run.Success, run.Error);
        Assert.Equal((1, 1, 0, 0), (run.ResultsEvaluated, run.Created, run.Updated, run.Unchanged));
        var rec = await LiveAsync(e);
        Assert.Equal((5m, 2m, 3m), (rec.OnHandQuantity, rec.ReservedQuantity, rec.UsableQuantity));   // Snapshot موجودی ردیابی می‌شود
        Assert.Equal(("Open", 1, 7m, 7m, true), (rec.Status, rec.Revision, rec.ShortageQuantity, rec.RecommendedQuantity, rec.IsCurrentlyDefinitive));
        Assert.Equal(64, rec.InputFingerprint.Length);
        Assert.Equal(2, await e.Db.ReplenishmentRecommendationSources.CountAsync(s => s.Revision == 1));
        var h = await e.Db.ReplenishmentRecommendationHistories.SingleAsync();
        Assert.Equal(("Created", "Open"), (h.Action, h.ToStatus));
        Assert.Null(h.FromStatus);
        var stored = await e.Db.NetDemandRuns.SingleAsync();
        Assert.Equal(("Completed", 1), (stored.Status, stored.Created));
    }

    [Fact]
    public async Task SecondRunWithUnchangedInputs_InsertsNothingAndWritesNoHistory()
    {
        var e = await NewAsync();
        e.Demand.Results = new() { Result(1, 1, Stock(1, 1, 3), SrLine(10, 1, 1, 8)) };
        await RunAsync(e);

        var again = await RunAsync(e);

        Assert.Equal((0, 0, 1), (again.Created, again.Updated, again.Unchanged));
        Assert.Equal(1, await e.Db.ReplenishmentRecommendations.CountAsync());
        Assert.Equal(1, await e.Db.ReplenishmentRecommendationHistories.CountAsync());
        Assert.Equal(1, await e.Db.ReplenishmentRecommendationSources.CountAsync());
        Assert.Equal(1, (await LiveAsync(e)).Revision);
        Assert.Equal(2, (await LiveAsync(e)).LastRunId);               // فقط ارزیابی ثبت شد
    }

    [Fact]
    public async Task MaterialChange_CreatesANewRevision_KeepingEarlierSourcesAndHistory()
    {
        var e = await NewAsync();
        e.Demand.Results = new() { Result(1, 1, Stock(1, 1, 3), SrLine(10, 1, 1, 8)) };
        await RunAsync(e);
        e.Demand.Results = new() { Result(1, 1, Stock(1, 1, 3), SrLine(10, 1, 1, 8), SrLine(11, 1, 1, 4)) };

        var run = await RunAsync(e);

        Assert.Equal((0, 1, 0), (run.Created, run.Updated, run.Unchanged));
        var rec = await LiveAsync(e);
        Assert.Equal((2, 9m), (rec.Revision, rec.ShortageQuantity));
        Assert.Equal(1, await e.Db.ReplenishmentRecommendationSources.CountAsync(s => s.Revision == 1));
        Assert.Equal(2, await e.Db.ReplenishmentRecommendationSources.CountAsync(s => s.Revision == 2));
        Assert.Equal(new[] { "Created", "Updated" }, (await e.Db.ReplenishmentRecommendationHistories.OrderBy(h => h.ReplenishmentRecommendationHistoryId).ToListAsync()).Select(h => h.Action).ToArray());
    }

    [Fact]
    public async Task RequestKey_MakesARepeatedRunAnExactReplay_NoSecondRun()
    {
        var e = await NewAsync();
        e.Demand.Results = new() { Result(1, 1, Stock(1, 1, 3), SrLine(10, 1, 1, 8)) };

        var first = await RunAsync(e, key: "abc");
        var second = await RunAsync(e, key: "abc");

        Assert.False(first.Replayed);
        Assert.True(second.Replayed);
        Assert.Equal(first.RunId, second.RunId);
        Assert.Equal(first.Created, second.Created);
        Assert.Equal(1, await e.Db.NetDemandRuns.CountAsync());
        Assert.Equal(1, e.Demand.Calls);                                // دومی اصلاً محاسبه نکرد
    }

    [Fact]
    public async Task OverlongRequestKey_IsRejected_WithoutARun()
    {
        var e = await NewAsync();

        var run = await RunAsync(e, key: new string('k', 65));

        Assert.False(run.Success);
        Assert.Empty(await e.Db.NetDemandRuns.ToListAsync());
    }

    // ───────── بسته شدن ─────────

    [Fact]
    public async Task ShortageResolved_ClosesTheRecommendation_AndALaterShortageCreatesANewOne()
    {
        var e = await NewAsync();
        e.Demand.Results = new() { Result(1, 1, Stock(1, 1, 3), SrLine(10, 1, 1, 8)) };
        await RunAsync(e);
        e.Demand.Results = new() { Result(1, 1, Stock(1, 1, 20), SrLine(10, 1, 1, 8)) };

        var closing = await RunAsync(e);

        Assert.Equal(1, closing.Closed);
        var closed = await e.Db.ReplenishmentRecommendations.SingleAsync();
        Assert.Equal(("Closed", ReplenishmentCloseReasons.ShortageResolved), (closed.Status, closed.ClosedReason));

        e.Demand.Results = new() { Result(1, 1, Stock(1, 1, 1), SrLine(10, 1, 1, 8)) };
        await RunAsync(e);
        Assert.Equal(new[] { "Closed", "Open" }, (await e.Db.ReplenishmentRecommendations.OrderBy(r => r.ReplenishmentRecommendationId).ToListAsync()).Select(r => r.Status).ToArray());
    }

    [Fact]
    public async Task KeyWithNoRemainingDemand_IsClosed_ButOnlyWithinTheRunScope()
    {
        var e = await NewAsync();
        e.Demand.Results = new() { Result(1, 1, Stock(1, 1, 3), SrLine(10, 1, 1, 8)), Result(2, 1, Stock(2, 1, 0), SrLine(20, 2, 1, 5)) };
        await RunAsync(e);
        e.Demand.Results = new();

        await RunAsync(e, items: new[] { 1 });                           // فقط متریال 1 در محدوده است

        var byItem = await e.Db.ReplenishmentRecommendations.OrderBy(r => r.MaterialItemId).ToListAsync();
        Assert.Equal(("Closed", ReplenishmentCloseReasons.NoRemainingDemand), (byItem[0].Status, byItem[0].ClosedReason));
        Assert.Equal("Open", byItem[1].Status);                          // خارج از محدوده دست‌نخورده
    }

    // ───────── قطعی‌نبودن / استثناها ─────────

    [Fact]
    public async Task NotDefinitiveResults_CreateNoRecommendation_AndAreReported()
    {
        var e = await NewAsync();
        e.Demand.Results = new()
        {
            AmbiguousStock(1, 1, SrLine(10, 1, 1, 8)),
            Result(2, 1, Stock(2, 1, 0, 0, NetDemandIssueCode.StockMultipleLocationRows), SrLine(20, 2, 1, 5)),
            NoWarehouse(1, 4)
        };

        var run = await RunAsync(e);

        Assert.True(run.Success);
        Assert.Equal((2, 2, 1, 0), (run.ResultsEvaluated, run.NotDefinitive, run.NotComparableNoWarehouse, run.Created));
        Assert.Empty(await e.Db.ReplenishmentRecommendations.ToListAsync());
        Assert.Contains(run.NotDefinitiveSummary, s => s.Contains("StockAmbiguousRows"));
        Assert.Contains(run.NotDefinitiveSummary, s => s.Contains("StockMultipleLocationRows"));
        Assert.Contains(run.NotDefinitiveSummary, s => s.Contains("RequiresWarehouseAttribution"));
    }

    [Fact]
    public async Task ExistingRecommendation_BecomingNotDefinitive_IsFlaggedOnce_ThenRestoredWithHistory()
    {
        var e = await NewAsync();
        e.Demand.Results = new() { Result(1, 1, Stock(1, 1, 3), SrLine(10, 1, 1, 8)) };
        await RunAsync(e);

        e.Demand.Results = new() { AmbiguousStock(1, 1, SrLine(10, 1, 1, 8)) };
        await RunAsync(e);
        await RunAsync(e);                                               // دوباره غیرقطعی: تاریخچه‌ی تکراری نباید ساخته شود
        var flagged = await LiveAsync(e);
        Assert.False(flagged.IsCurrentlyDefinitive);
        Assert.Contains("StockAmbiguousRows", flagged.BlockingIssueCodes);
        Assert.Equal(1, await e.Db.ReplenishmentRecommendationHistories.CountAsync(h => h.Action == "NotDefinitive"));
        Assert.Equal("Open", flagged.Status);                            // وضعیت دست‌نخورده؛ فقط علامت قطعی‌نبودن

        e.Demand.Results = new() { Result(1, 1, Stock(1, 1, 3), SrLine(10, 1, 1, 8)) };
        await RunAsync(e);
        var restored = await LiveAsync(e);
        Assert.True(restored.IsCurrentlyDefinitive);
        Assert.Null(restored.BlockingIssueCodes);
        Assert.Equal(1, await e.Db.ReplenishmentRecommendationHistories.CountAsync(h => h.Action == "DefinitiveAgain"));
    }

    // ───────── رد / بازگشایی ─────────

    private static async Task<int> CreateOpenAsync(Env e)
    {
        e.Demand.Results = new() { Result(1, 1, Stock(1, 1, 3), SrLine(10, 1, 1, 8)) };   // کمبود 5
        await RunAsync(e);
        return (await LiveAsync(e)).ReplenishmentRecommendationId;
    }

    [Fact]
    public async Task Dismiss_RequiresAReason_AndOnlyAnOpenRecommendation_AndIsRecordedInHistory()
    {
        var e = await NewAsync();
        var id = await CreateOpenAsync(e);

        Assert.False((await e.Svc.DismissAsync(id, null, "  ", Full)).Success);
        var ok = await e.Svc.DismissAsync(id, null, "covered by a transfer", Full);
        Assert.True(ok.Success, ok.Error);
        Assert.False((await e.Svc.DismissAsync(id, null, "again", Full)).Success);   // دیگر Open نیست

        var rec = await LiveAsync(e);
        Assert.Equal(("Dismissed", 5m, "S:10"), (rec.Status, rec.DismissedShortageQuantity, rec.DismissedSourceKeys));
        var h = await e.Db.ReplenishmentRecommendationHistories.OrderBy(x => x.ReplenishmentRecommendationHistoryId).LastAsync();
        Assert.Equal(("Dismissed", "covered by a transfer", 1), (h.Action, h.Note, h.ActorEmployeeId));
    }

    [Fact]
    public async Task DismissedRecommendation_DoesNotReopenBecauseTheCalculationRunsAgain_OrBecauseShortageShrinks()
    {
        var e = await NewAsync();
        var id = await CreateOpenAsync(e);
        await e.Svc.DismissAsync(id, null, "ignore", Full);

        await RunAsync(e);                                               // ورودی بدون تغییر
        e.Demand.Results = new() { Result(1, 1, Stock(1, 1, 5), SrLine(10, 1, 1, 8)) };   // کمبود 3 (کمتر)
        var run = await RunAsync(e);

        Assert.Equal(0, run.AutoReopened);
        var rec = await LiveAsync(e);
        Assert.Equal(("Dismissed", 3m, 2), (rec.Status, rec.ShortageQuantity, rec.Revision));   // Snapshot به‌روز، وضعیت دست‌نخورده
        Assert.Equal(5m, rec.DismissedShortageQuantity);                  // مبنای مقایسه ثابت می‌ماند
        Assert.DoesNotContain(await e.Db.ReplenishmentRecommendationHistories.ToListAsync(), h => h.Action == "AutoReopened");
    }

    [Fact]
    public async Task DismissedRecommendation_ReopensAutomatically_WhenTheShortageGrows()
    {
        var e = await NewAsync();
        var id = await CreateOpenAsync(e);
        await e.Svc.DismissAsync(id, null, "ignore", Full);
        e.Demand.Results = new() { Result(1, 1, Stock(1, 1, 3), SrLine(10, 1, 1, 9)) };   // کمبود 6 > 5

        var run = await RunAsync(e);

        Assert.Equal(1, run.AutoReopened);
        var rec = await LiveAsync(e);
        Assert.Equal(("Open", 6m), (rec.Status, rec.ShortageQuantity));
        Assert.Null(rec.DismissedAt);
        var h = await e.Db.ReplenishmentRecommendationHistories.OrderBy(x => x.ReplenishmentRecommendationHistoryId).LastAsync();
        Assert.Equal("AutoReopened", h.Action);
        Assert.Null(h.ActorEmployeeId);                                  // اقدام سیستمی
        Assert.Contains("increased", h.Note);
    }

    [Fact]
    public async Task DismissedRecommendation_ReopensAutomatically_WhenANewDemandSourceAppears_EvenWithLowerShortage()
    {
        var e = await NewAsync();
        var id = await CreateOpenAsync(e);
        await e.Svc.DismissAsync(id, null, "ignore", Full);
        e.Demand.Results = new() { Result(1, 1, Stock(1, 1, 7), SrLine(10, 1, 1, 8), SrLine(12, 1, 1, 2)) };   // کمبود 3 ولی منبع S:12 تازه است

        var run = await RunAsync(e);

        Assert.Equal(1, run.AutoReopened);
        Assert.Equal("Open", (await LiveAsync(e)).Status);
    }

    [Fact]
    public async Task ManualReopen_WorksOnlyForDismissed_AndRecordsTheActor()
    {
        var e = await NewAsync();
        var id = await CreateOpenAsync(e);

        Assert.False((await e.Svc.ReopenAsync(id, null, null, Full)).Success);   // هنوز Open است
        await e.Svc.DismissAsync(id, null, "ignore", Full);
        var reopened = await e.Svc.ReopenAsync(id, null, "needed after all", Full);

        Assert.True(reopened.Success, reopened.Error);
        var rec = await LiveAsync(e);
        Assert.Equal(("Open", (DateTime?)null), (rec.Status, rec.DismissedAt));
        var h = await e.Db.ReplenishmentRecommendationHistories.OrderBy(x => x.ReplenishmentRecommendationHistoryId).LastAsync();
        Assert.Equal(("Reopened", "needed after all", 1), (h.Action, h.Note, h.ActorEmployeeId));
    }

    [Fact]
    public async Task ClosedRecommendation_CannotBeDismissedOrReopened_AndMissingIdIsNotFound()
    {
        var e = await NewAsync();
        var id = await CreateOpenAsync(e);
        e.Demand.Results = new();
        await RunAsync(e);                                               // بسته شد

        Assert.False((await e.Svc.DismissAsync(id, null, "x", Full)).Success);
        Assert.False((await e.Svc.ReopenAsync(id, null, null, Full)).Success);
        Assert.True((await e.Svc.DismissAsync(999, null, "x", Full)).NotFound);
    }

    // ───────── مجوز ─────────

    [Fact]
    public async Task WithoutPermissions_NothingRunsAndNothingIsWritten()
    {
        var e = await NewAsync(new Perms().Allow("REPL", Module, "View"));
        e.Demand.Results = new() { Result(1, 1, Stock(1, 1, 3), SrLine(10, 1, 1, 8)) };

        var run = await e.Svc.RunAsync(new ReplenishmentRunRequest(), Full);

        Assert.True(run.Forbidden);
        Assert.Empty(await e.Db.NetDemandRuns.ToListAsync());
        Assert.Equal(0, e.Demand.Calls);

        var none = new ReplenishmentActor(2, 2, new[] { "NOBODY" });
        Assert.False((await e.Svc.GetPagedAsync(new ReplenishmentFilter(), 1, 25, none)).Authorized);
        Assert.False((await e.Svc.GetDetailsAsync(1, none)).Authorized);
        Assert.True((await e.Svc.DismissAsync(1, null, "x", Full)).Forbidden);            // View بدون Edit
        Assert.True((await e.Svc.ReopenAsync(1, null, null, Full)).Forbidden);
    }

    // ───────── خواندن / پوشاندن منبع ─────────

    [Fact]
    public async Task Details_MaskSourceIdentityWithoutSourceViewPermission_ButKeepQuantities()
    {
        var e = await NewAsync();
        var sr = new StoreRequisition { RequisitionNumber = "SR-77", Status = "Approved", WarehouseId = 1, RequestedById = 1 };
        sr.Lines.Add(new StoreRequisitionLine { MaterialItemId = 1, RequestedQuantity = 8, ApprovedQuantity = 8 });
        e.Db.StoreRequisitions.Add(sr);
        await e.Db.SaveChangesAsync();
        var lineId = sr.Lines.Single().StoreRequisitionLineId;
        e.Demand.Results = new() { Result(1, 1, Stock(1, 1, 3), SrLine(lineId, 1, 1, 8, parent: sr.StoreRequisitionId)) };
        await RunAsync(e);
        var id = (await LiveAsync(e)).ReplenishmentRecommendationId;

        var full = (await e.Svc.GetDetailsAsync(id, Full)).Data!;
        var viewOnly = new Perms().Allow("VIEWONLY", Module, "View");
        var svc2 = new ReplenishmentRecommendationService(e.Db, e.Demand, viewOnly);
        var masked = (await svc2.GetDetailsAsync(id, new ReplenishmentActor(3, 3, new[] { "VIEWONLY" }))).Data!;

        Assert.Equal(("SR-77", false), (full.Sources.Single().ParentNumber, full.Sources.Single().IsRestricted));
        Assert.Equal((0, null, true, 8m), (masked.Sources.Single().SourceLineId, masked.Sources.Single().ParentNumber, masked.Sources.Single().IsRestricted, masked.Sources.Single().GrossRemainingQuantity));
        Assert.Equal(5m, masked.Summary.ShortageQuantity);                // مقدار پنهان نمی‌شود
    }

    [Fact]
    public async Task List_FiltersByStatusSearchAndDefinitive_AndPages()
    {
        var e = await NewAsync();
        e.Demand.Results = new() { Result(1, 1, Stock(1, 1, 3), SrLine(10, 1, 1, 8)), Result(2, 2, Stock(2, 2, 0), SrLine(20, 2, 2, 4)) };
        await RunAsync(e);
        var id1 = (await LiveAsync(e, 1, 1)).ReplenishmentRecommendationId;
        await e.Svc.DismissAsync(id1, null, "x", Full);

        var all = (await e.Svc.GetPagedAsync(new ReplenishmentFilter(), 1, 25, Full)).Data!;
        var open = (await e.Svc.GetPagedAsync(new ReplenishmentFilter("Open"), 1, 25, Full)).Data!;
        var search = (await e.Svc.GetPagedAsync(new ReplenishmentFilter(null, "Seal"), 1, 25, Full)).Data!;
        var paged = (await e.Svc.GetPagedAsync(new ReplenishmentFilter(), 1, 1, Full)).Data!;

        Assert.Equal(2, all.TotalCount);
        Assert.Equal("M-2", Assert.Single(open.Items).MaterialCode);
        Assert.Equal("M-2", Assert.Single(search.Items).MaterialCode);
        Assert.Equal((1, 2), (paged.Items.Count, paged.TotalPages));
        Assert.Equal("Open", paged.Items[0].Status);                      // Open اول
    }

    // ───────── مرز فقط‌خواندنی ─────────

    [Fact]
    public async Task RunAndLifecycle_NeverTouchStockLedgerReservationsRequisitionsOrOrders()
    {
        var e = await NewAsync();
        e.Db.MaterialStocks.Add(new MaterialStock { MaterialItemId = 1, WarehouseId = 1, OnHandQuantity = 3, ReservedQuantity = 1 });
        e.Db.InventoryTransactions.Add(new InventoryTransaction { MaterialItemId = 1, WarehouseId = 1, TransactionType = "Receipt", Quantity = 3 });
        e.Db.MaterialReservations.Add(new MaterialReservation { MaterialItemId = 1, WarehouseId = 1, Quantity = 1, Status = "Active", ReservedById = 1 });
        await e.Db.SaveChangesAsync();
        string Snapshot() => string.Join("|",
            e.Db.MaterialStocks.AsNoTracking().Select(s => $"{s.OnHandQuantity}/{s.ReservedQuantity}/{(s.RowVersion == null ? 0 : s.RowVersion.Length)}"),
            e.Db.InventoryTransactions.AsNoTracking().Count(),
            e.Db.MaterialReservations.AsNoTracking().Select(r => r.Quantity),
            e.Db.StoreRequisitions.AsNoTracking().Count(), e.Db.PurchaseOrders.AsNoTracking().Count(), e.Db.MaterialRequisitions.AsNoTracking().Count());
        var before = Snapshot();

        e.Demand.Results = new() { Result(1, 1, Stock(1, 1, 3, 1), SrLine(10, 1, 1, 8)) };
        await RunAsync(e);
        var id = (await LiveAsync(e)).ReplenishmentRecommendationId;
        await e.Svc.DismissAsync(id, null, "x", Full);
        await e.Svc.ReopenAsync(id, null, null, Full);
        e.Demand.Results = new();
        await RunAsync(e);

        Assert.Equal(before, Snapshot());
    }

    private static async Task<(PEMSDbContext Db, ReplenishmentRecommendationService Svc, MaterialItem Item, Warehouse Wh)> RealStackAsync(bool withWorkOrderDemand)
    {
        var db = PemsDbContextFactory.CreateInMemory("repl_real_" + Guid.NewGuid().ToString("N"));
        var uom = new MaterialUOM { Code = "EA", Name = "Each" };
        var wh = new Warehouse { Code = "WH-1", Name = "Main" };
        db.AddRange(uom, wh);
        await db.SaveChangesAsync();
        var item = new MaterialItem { MaterialCode = "M-1", Description = "Bearing", UomId = uom.MaterialUOMId, IsStockItem = true };
        db.MaterialItems.Add(item);
        await db.SaveChangesAsync();
        db.MaterialStocks.Add(new MaterialStock { MaterialItemId = item.MaterialItemId, WarehouseId = wh.WarehouseId, OnHandQuantity = 3 });
        var sr = new StoreRequisition { RequisitionNumber = "SR-1", Status = "Approved", WarehouseId = wh.WarehouseId, RequestedById = 1 };
        sr.Lines.Add(new StoreRequisitionLine { MaterialItemId = item.MaterialItemId, RequestedQuantity = 8, ApprovedQuantity = 8 });
        db.StoreRequisitions.Add(sr);
        await db.SaveChangesAsync();
        if (withWorkOrderDemand)
        {
            var wo = new WorkOrder { WorkOrderNumber = "WO-1", EquipmentId = 1, Status = "InProgress" };
            db.WorkOrders.Add(wo);
            await db.SaveChangesAsync();
            db.WorkOrderMaterials.Add(new WorkOrderMaterial { WorkOrderId = wo.WorkOrderId, MaterialItemId = item.MaterialItemId, RequiredQuantity = 5 });
            await db.SaveChangesAsync();
        }
        var perms = StandardPerms();
        return (db, new ReplenishmentRecommendationService(db, new NetDemandService(new NetDemandCollector(db), new UsableStockService(db), perms), perms), item, wh);
    }

    [Fact]
    public async Task RealP3Stack_ProducesARecommendationFromAnApprovedRequisition_WithRealSourceIds()
    {
        var (db, svc, item, wh) = await RealStackAsync(withWorkOrderDemand: false);

        var run = await svc.RunAsync(new ReplenishmentRunRequest(), Full);

        Assert.True(run.Success, run.Error);
        Assert.Equal(1, run.Created);
        var rec = await db.ReplenishmentRecommendations.SingleAsync();
        Assert.Equal((item.MaterialItemId, wh.WarehouseId, "Open", 5m), (rec.MaterialItemId, rec.WarehouseId, rec.Status, rec.ShortageQuantity));
        var src = await db.ReplenishmentRecommendationSources.SingleAsync();
        Assert.Equal(1, src.SourceType);
        Assert.Equal((await db.StoreRequisitionLines.SingleAsync()).StoreRequisitionLineId, src.StoreRequisitionLineId);
        Assert.Null(src.WorkOrderMaterialId);
    }

    [Fact]
    public async Task RealP3Stack_WorkOrderDemandWithoutAWarehouse_IsNeverGuessed_TheMaterialIsReportedNotRecommended()
    {
        var (db, svc, _, _) = await RealStackAsync(withWorkOrderDemand: true);
        var run = await svc.RunAsync(new ReplenishmentRunRequest(), Full);
        Assert.True(run.Success, run.Error);
        Assert.Equal(0, run.Created);
        Assert.True(run.NotComparableNoWarehouse >= 1);
        Assert.True(run.NotDefinitive >= 1);
        Assert.Empty(await db.ReplenishmentRecommendations.ToListAsync());
    }

    // ───────── سیاست‌های تأییدشده: انبار Work Order و تازگی ورودی‌ها ─────────

    [Fact]
    public async Task WorkOrderDemandWithoutWarehouse_IsReportedWithTheMaterialCode_AndNeverAssignedToAWarehouse()
    {
        var e = await NewAsync();
        e.Demand.Results = new()
        {
            NoWarehouse(1, 4),
            NetDemandCalculator.Calculate(new NetDemandInput(1, 1, Stock(1, 1, 3), new[] { SrLine(10, 1, 1, 8) }, Array.Empty<ExcludedDemand>(),
                new[] { new NetDemandIssue(NetDemandIssueCode.WorkOrderDemandNotAttributableToWarehouse) }))
        };

        var run = await RunAsync(e);

        Assert.Equal(0, run.Created);
        Assert.Empty(await e.Db.ReplenishmentRecommendations.ToListAsync());                 // هیچ توصیه‌ای با استنتاج ساخته نمی‌شود
        Assert.Equal(2, run.NotDefinitiveSummary.Count(x => x.Contains(ReplenishmentRules.RequiresWarehouseAttribution)));
        Assert.Contains(run.NotDefinitiveSummary, x => x.Contains("M-1") && x.Contains("work-order demand 4"));
        Assert.Contains(run.NotDefinitiveSummary, x => x.Contains("M-1") && x.Contains("WH-1"));
        var stored = await e.Db.NetDemandRuns.SingleAsync();
        Assert.Contains(ReplenishmentRules.RequiresWarehouseAttribution, stored.NotDefinitiveSummary);   // در خودِ اجرا هم ثبت می‌شود
    }

    [Fact]
    public async Task ExistingRecommendation_TurnedNonDefinitiveByWorkOrderDemand_ShowsTheAttributionFlag()
    {
        var e = await NewAsync();
        e.Demand.Results = new() { Result(1, 1, Stock(1, 1, 3), SrLine(10, 1, 1, 8)) };
        await RunAsync(e);
        e.Demand.Results = new()
        {
            NetDemandCalculator.Calculate(new NetDemandInput(1, 1, Stock(1, 1, 3), new[] { SrLine(10, 1, 1, 8) }, Array.Empty<ExcludedDemand>(),
                new[] { new NetDemandIssue(NetDemandIssueCode.WorkOrderDemandNotAttributableToWarehouse) }))
        };
        await RunAsync(e);
        var id = (await LiveAsync(e)).ReplenishmentRecommendationId;

        var details = (await e.Svc.GetDetailsAsync(id, Full)).Data!;

        Assert.False(details.Summary.IsCurrentlyDefinitive);
        Assert.True(details.RequiresWarehouseAttribution);
        Assert.Contains(ReplenishmentRules.RequiresWarehouseAttribution, details.BlockingIssueCodes);
    }

    [Fact]
    public async Task Freshness_IsRecordedOnlyWhenEveryInputHasATimestamp_OtherwiseUnknown_AndNeverClaimedCurrent()
    {
        var e = await NewAsync();
        var t1 = new DateTime(2026, 10, 1, 8, 0, 0);
        var t2 = new DateTime(2026, 10, 2, 9, 30, 0);
        var sr = new StoreRequisition { RequisitionNumber = "SR-9", Status = "Approved", WarehouseId = 1, RequestedById = 1 };
        sr.Lines.Add(new StoreRequisitionLine { MaterialItemId = 1, RequestedQuantity = 8, ApprovedQuantity = 8 });
        e.Db.StoreRequisitions.Add(sr);
        await e.Db.SaveChangesAsync();
        e.Db.StoreRequisitionStatusHistories.AddRange(
            new StoreRequisitionStatusHistory { StoreRequisitionId = sr.StoreRequisitionId, FromStatus = "Draft", ToStatus = "Submitted", ChangedDate = t1 },
            new StoreRequisitionStatusHistory { StoreRequisitionId = sr.StoreRequisitionId, FromStatus = "Submitted", ToStatus = "Approved", ChangedDate = t2 });
        e.Db.MaterialStocks.Add(new MaterialStock { MaterialItemId = 1, WarehouseId = 1, OnHandQuantity = 3, IsActive = true, LastUpdated = t1 });
        await e.Db.SaveChangesAsync();
        var lineId = sr.Lines.Single().StoreRequisitionLineId;
        e.Demand.Results = new() { Result(1, 1, Stock(1, 1, 3), SrLine(lineId, 1, 1, 8, parent: sr.StoreRequisitionId)) };

        await RunAsync(e);

        var known = await LiveAsync(e);
        Assert.Equal((t1, t2), (known.StockLastUpdatedAt, known.DemandLastChangedAt));          // ثبت‌شده چون همه‌ی ورودی‌ها زمان دارند
        var run = await e.Db.NetDemandRuns.SingleAsync();
        Assert.NotNull(run.CompletedAt);                                                          // زمان محاسبه ثبت می‌شود
        var details = (await e.Svc.GetDetailsAsync(known.ReplenishmentRecommendationId, Full)).Data!;
        Assert.Equal((run.StartedAt, run.CompletedAt), (details.LastRunStartedAt, details.LastRunCompletedAt));

        // یک ردیف موجودی بدون LastUpdated ⇒ تازگی موجودی نامعلوم (نه بیشینه‌ی بقیه)؛ منبع Work Order ⇒ تازگی تقاضا نامعلوم
        e.Db.MaterialStocks.Add(new MaterialStock { MaterialItemId = 1, WarehouseId = 1, StorageLocationId = null, OnHandQuantity = 0, IsActive = true, LastUpdated = null });
        await e.Db.SaveChangesAsync();
        e.Demand.Results = new() { Result(1, 1, Stock(1, 1, 3), SrLine(lineId, 1, 1, 8, parent: sr.StoreRequisitionId),
            new DemandLine(new DemandOrigin(DemandSourceType.WorkOrderMaterial, 77, 5, "WO-5"), 1, 1, 1, 0m)) };
        await RunAsync(e);

        var unknown = await LiveAsync(e);
        Assert.Null(unknown.StockLastUpdatedAt);
        Assert.Null(unknown.DemandLastChangedAt);
    }
}
