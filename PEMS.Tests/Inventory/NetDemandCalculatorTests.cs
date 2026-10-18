using System.Text.Json;
using PEMS.Application.Inventory;

namespace PEMS.Tests.Inventory;

/// <summary>
/// Phase 2 / P2 - تست‌های قطعی و خالص NetDemandCalculator (بدون DB). قرارداد: UsableStock = OnHand - Reserved؛ تقاضای رزروشده از «تقاضا» کم می‌شود
/// نه دوباره از موجودی؛ ورودی نامعلوم صفر فرض نمی‌شود؛ top-up نقطه‌ی سفارش جدا از کمبود است.
/// </summary>
public class NetDemandCalculatorTests
{
    private const int Item = 7, Wh = 3;

    private static DemandOrigin Sr(int line, int sr = 100) => new(DemandSourceType.StoreRequisitionLine, line, sr, "SR-" + sr);
    private static DemandOrigin Wo(int id, int wo = 200) => new(DemandSourceType.WorkOrderMaterial, id, wo, "WO-" + wo);

    private static DemandLine Line(DemandOrigin o, decimal gross, decimal covered = 0, DemandUnitStatus unit = DemandUnitStatus.Compatible, int? wh = Wh)
        => new(o, Item, wh, gross, covered, unit);

    private static UsableStockResult Stock(decimal onHand, decimal reserved, ReorderPolicy? policy = null, StockState state = StockState.Ok, params NetDemandIssue[] issues)
        => new(Item, Wh, state, onHand, reserved, onHand - reserved, 1, policy, issues);

    private static NetDemandResult Calc(UsableStockResult? stock, IEnumerable<DemandLine> demand, IEnumerable<ExcludedDemand>? excluded = null,
        IEnumerable<NetDemandIssue>? itemIssues = null, decimal? incoming = null, int? wh = Wh)
        => NetDemandCalculator.Calculate(new NetDemandInput(Item, wh, stock, demand.ToList(), (excluded ?? Array.Empty<ExcludedDemand>()).ToList(),
            itemIssues?.ToList(), incoming));

    private static bool Has(NetDemandResult r, NetDemandIssueCode code) => r.Issues.Any(i => i.Code == code);

    // ---------------------------------------------------------------- 1
    [Fact]
    public void NoDemand_WithPositiveStock_HasZeroShortage_AndIsAStockOnlyEstimate()
    {
        var r = Calc(Stock(10, 0), Array.Empty<DemandLine>());

        Assert.Equal(NetDemandResultKind.StockOnlyShortageEstimate, r.Kind);
        Assert.Equal(0m, r.GrossEligibleDemand);
        Assert.Equal(0m, r.StockOnlyShortage);
        Assert.Equal(10m, r.UsableStockForNetting);
        Assert.True(r.IsDefinitive);
        Assert.Null(r.ProjectedBalance);
        Assert.Null(r.CommittedShortage);
    }

    // ---------------------------------------------------------------- 2
    [Fact]
    public void DemandFullyCoveredByUsableStock_HasNoShortage()
    {
        var r = Calc(Stock(10, 0), new[] { Line(Sr(1), 6) });

        Assert.Equal(6m, r.UnreservedEligibleDemand);
        Assert.Equal(0m, r.StockOnlyShortage);
    }

    // ---------------------------------------------------------------- 3 (E1)
    [Fact]
    public void DemandExceedingUsableStock_ReportsTheStockOnlyShortage_NotACompleteForecast()
    {
        var r = Calc(Stock(4, 0), new[] { Line(Sr(1), 10) });

        Assert.Equal(6m, r.StockOnlyShortage);
        Assert.Equal(NetDemandResultKind.StockOnlyShortageEstimate, r.Kind);
        Assert.Null(r.CommittedShortage);                       // «کمبود متعهد» فقط با ورودی معلوم
        Assert.Equal(IncomingSupplyStatus.NotModeled, r.IncomingSupply);
        Assert.True(r.IsDefinitive);                            // برای آنچه مدل شده قطعی است، ولی کامل نیست
    }

    // ---------------------------------------------------------------- 4 (E3)
    [Fact]
    public void Reservations_AreDeductedExactlyOnce_FromDemandNotFromStockAgain()
    {
        // OnHand 10 با Reserved 6 (برای SR-A که کاملاً رزرو دارد)؛ SR-B تأییدشده 5 بدون رزرو
        var r = Calc(Stock(10, 6), new[] { Line(Sr(1, 100), 6, covered: 6), Line(Sr(2, 101), 5) });

        Assert.Equal(11m, r.GrossEligibleDemand);
        Assert.Equal(6m, r.CoveredByReservations);
        Assert.Equal(5m, r.UnreservedEligibleDemand);
        Assert.Equal(4m, r.UsableStockForNetting);
        Assert.Equal(1m, r.StockOnlyShortage);

        // اگر پوشش رزرو نادیده گرفته شود (تقاضای 11 در برابر Usable 4) ⇒ 7: یعنی همان رزرو دوبار اثر کرده بود
        var doubleCounted = Calc(Stock(10, 6), new[] { Line(Sr(1, 100), 6), Line(Sr(2, 101), 5) });
        Assert.Equal(7m, doubleCounted.StockOnlyShortage);
        Assert.NotEqual(r.StockOnlyShortage, doubleCounted.StockOnlyShortage);
    }

    // ---------------------------------------------------------------- 5
    [Fact]
    public void NegativeOnHand_IsReportedExplicitly_NotSilentlyClamped_AndMakesTheResultNonDefinitive()
    {
        var r = Calc(Stock(-3, 0), new[] { Line(Sr(1), 2) });

        Assert.Equal(-3m, r.RawUsableStock);
        Assert.Equal(0m, r.UsableStockForNetting);              // Clamp فقط در محاسبه، و صریح گزارش شده
        Assert.Equal(2m, r.StockOnlyShortage);
        Assert.True(Has(r, NetDemandIssueCode.StockNegativeOnHand));
        Assert.False(r.IsDefinitive);
    }

    [Fact]
    public void ReservedExceedingOnHand_IsFlagged_EvenIfTheStockProducerDidNotFlagIt()
    {
        var r = Calc(Stock(5, 8), new[] { Line(Sr(1), 1) });

        Assert.Equal(-3m, r.RawUsableStock);
        Assert.True(Has(r, NetDemandIssueCode.StockReservedExceedsOnHand));
        Assert.False(r.IsDefinitive);
    }

    // ---------------------------------------------------------------- 6
    [Fact]
    public void MissingStockData_IsNotCalculable_NotAFabricatedZero()
    {
        var r = Calc(null, new[] { Line(Sr(1), 10) });

        Assert.Equal(NetDemandResultKind.NotCalculable, r.Kind);
        Assert.Null(r.StockOnlyShortage);
        Assert.Null(r.UsableStockForNetting);
        Assert.True(Has(r, NetDemandIssueCode.StockMissing));
        Assert.False(r.IsDefinitive);
    }

    [Theory]
    [InlineData(StockState.AmbiguousRows)]
    [InlineData(StockState.NotCalculable)]
    public void AmbiguousOrUnusableStockRows_AreNotCalculable(StockState state)
    {
        var s = new UsableStockResult(Item, Wh, state, null, null, null, 2, null, new[] { new NetDemandIssue(NetDemandIssueCode.StockAmbiguousRows, "2 rows") });

        var r = Calc(s, new[] { Line(Sr(1), 3) });

        Assert.Equal(NetDemandResultKind.NotCalculable, r.Kind);
        Assert.Null(r.StockOnlyShortage);
        Assert.False(r.IsDefinitive);
    }

    [Fact]
    public void NoStockRecord_IsZeroUsableStock_AndRemainsDefinitive()
    {
        var s = new UsableStockResult(Item, Wh, StockState.NoStockRecord, 0, 0, 0, 0, null, new[] { new NetDemandIssue(NetDemandIssueCode.StockNoRecord) });

        var r = Calc(s, new[] { Line(Sr(1), 4) });

        Assert.Equal(4m, r.StockOnlyShortage);
        Assert.True(r.IsDefinitive);
    }

    // ---------------------------------------------------------------- 7
    [Theory]
    [InlineData(DemandUnitStatus.Unknown, NetDemandIssueCode.UnitUnknown)]
    [InlineData(DemandUnitStatus.Incompatible, NetDemandIssueCode.UnitIncompatible)]
    public void UnknownOrIncompatibleUnits_AreExcludedFromTheNumbers_AndReported(DemandUnitStatus unit, NetDemandIssueCode expected)
    {
        var r = Calc(Stock(10, 0), new[] { Line(Sr(1), 4), Line(Sr(2), 100, unit: unit) });

        Assert.Equal(4m, r.GrossEligibleDemand);                // هیچ تبدیل واحدی: خط ناسازگار اصلاً شمرده نمی‌شود
        var ex = Assert.Single(r.Excluded);
        Assert.Equal(expected, ex.Reason);
        Assert.False(r.IsDefinitive);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(-1, 0)]
    [InlineData(5, 6)]      // پوشش بیش از مقدار
    [InlineData(5, -1)]
    public void InvalidQuantities_AreExcluded(double gross, double covered)
    {
        var r = Calc(Stock(10, 0), new[] { Line(Sr(1), (decimal)gross, (decimal)covered) });

        Assert.Equal(0m, r.GrossEligibleDemand);
        Assert.Equal(NetDemandIssueCode.InvalidQuantity, Assert.Single(r.Excluded).Reason);
        Assert.False(r.IsDefinitive);
    }

    // ---------------------------------------------------------------- 8
    [Fact]
    public void CancelledAndNotYetCommittedDemand_NeverAffectTheNumbers_AndDoNotBreakDefiniteness()
    {
        var excluded = new[]
        {
            new ExcludedDemand(Sr(9), Item, Wh, NetDemandIssueCode.CancelledOrIneligibleLifecycle, "cancelled", 50),
            new ExcludedDemand(Wo(9), Item, Wh, NetDemandIssueCode.PotentialDemandNotCommitted, "Open work order", 70)
        };

        var r = Calc(Stock(5, 0), new[] { Line(Sr(1), 8) }, excluded);

        Assert.Equal(8m, r.GrossEligibleDemand);
        Assert.Equal(3m, r.StockOnlyShortage);
        Assert.Equal(2, r.Excluded.Count);
        Assert.True(r.IsDefinitive);
    }

    // ---------------------------------------------------------------- 9
    [Fact]
    public void ConclusivelyIdenticalDuplicateSourceLine_IsCountedOnce()
    {
        var r = Calc(Stock(0, 0), new[] { Line(Sr(1), 5), Line(Sr(1), 5) });

        Assert.Equal(5m, r.GrossEligibleDemand);
        Assert.Single(r.Contributions);
        Assert.True(Has(r, NetDemandIssueCode.DuplicateSourceLineIgnored));
        Assert.True(r.IsDefinitive);
    }

    [Fact]
    public void ConflictingDuplicateSourceLine_IsExcludedEntirely_AndNonDefinitive()
    {
        var r = Calc(Stock(0, 0), new[] { Line(Sr(1), 5), Line(Sr(1), 7) });

        Assert.Equal(0m, r.GrossEligibleDemand);
        Assert.Equal(2, r.Excluded.Count(e => e.Reason == NetDemandIssueCode.ConflictingDuplicateSourceLine));
        Assert.False(r.IsDefinitive);
    }

    // ---------------------------------------------------------------- 10
    [Fact]
    public void AmbiguousWorkOrderStoreRequisitionOverlap_IsNotCounted_AndMakesTheResultNonDefinitive()
    {
        // SR تأییدشده 5 شمرده می‌شود؛ نیاز WO (8) به‌خاطر ابهام در مجموع قطعی نیست
        var overlap = new NetDemandIssue(NetDemandIssueCode.AmbiguousOverlapWithStoreRequisition, "work-order requirement not counted");

        var r = Calc(Stock(2, 0), new[] { Line(Sr(1), 5) }, null, new[] { overlap });

        Assert.Equal(5m, r.GrossEligibleDemand);
        Assert.Equal(3m, r.StockOnlyShortage);
        Assert.True(Has(r, NetDemandIssueCode.AmbiguousOverlapWithStoreRequisition));
        Assert.False(r.IsDefinitive);
    }

    [Fact]
    public void WorkOrderDemandWithoutWarehouse_IsReportedButNeverNettedAgainstAnyStock()
    {
        var r = Calc(null, new[] { Line(Wo(1), 8, wh: null) }, wh: null);

        Assert.Equal(NetDemandResultKind.NotComparableNoWarehouse, r.Kind);
        Assert.Equal(8m, r.GrossEligibleDemand);
        Assert.Null(r.StockOnlyShortage);
        Assert.Null(r.UsableStockForNetting);
        Assert.True(Has(r, NetDemandIssueCode.WorkOrderDemandNotAttributableToWarehouse));
        Assert.False(r.IsDefinitive);
    }

    // ---------------------------------------------------------------- 11
    [Fact]
    public void UnknownIncomingSupply_IsNeverTreatedAsZero_AndNoProjectionIsProduced()
    {
        var r = Calc(Stock(4, 0), new[] { Line(Sr(1), 10) });

        Assert.Equal(IncomingSupplyStatus.NotModeled, r.IncomingSupply);
        Assert.Null(r.ProjectedBalance);
        Assert.Null(r.CommittedShortage);
        Assert.True(Has(r, NetDemandIssueCode.IncomingSupplyNotModeled));
    }

    [Fact]
    public void KnownIncomingSupply_UsesTheAuditedFormula_WhenInputsAreComplete()
    {
        // Usable 4 + Incoming 3 - Unreserved 10 = -3 ⇒ CommittedShortage 3
        var short_ = Calc(Stock(4, 0), new[] { Line(Sr(1), 10) }, incoming: 3m);
        Assert.Equal(NetDemandResultKind.CompleteNetDemand, short_.Kind);
        Assert.Equal(IncomingSupplyStatus.Known, short_.IncomingSupply);
        Assert.Equal(-3m, short_.ProjectedBalance);
        Assert.Equal(3m, short_.CommittedShortage);

        // Usable 4 + Incoming 8 - Unreserved 10 = 2 ⇒ no shortage
        var ok = Calc(Stock(4, 0), new[] { Line(Sr(1), 10) }, incoming: 8m);
        Assert.Equal(2m, ok.ProjectedBalance);
        Assert.Equal(0m, ok.CommittedShortage);
    }

    // ---------------------------------------------------------------- 12
    [Fact]
    public void RepeatedCalculation_IsDeterministic_RegardlessOfInputOrder()
    {
        var demand = new[] { Line(Sr(3, 102), 2), Line(Sr(1, 100), 6, covered: 6), Line(Sr(2, 101), 5), Line(Sr(2, 101), 5) };
        var excluded = new[]
        {
            new ExcludedDemand(Wo(2), Item, Wh, NetDemandIssueCode.PotentialDemandNotCommitted, "b", 1),
            new ExcludedDemand(Wo(1), Item, Wh, NetDemandIssueCode.PotentialDemandNotCommitted, "a", 1)
        };

        var a = JsonSerializer.Serialize(Calc(Stock(10, 6), demand, excluded));
        var b = JsonSerializer.Serialize(Calc(Stock(10, 6), demand.Reverse(), excluded.Reverse()));
        var c = JsonSerializer.Serialize(Calc(Stock(10, 6), demand, excluded));

        Assert.Equal(a, b);
        Assert.Equal(a, c);
    }

    // ---------------------------------------------------------------- 13
    [Fact]
    public void ReorderPolicy_NeverChangesTheShortage_AndNoTopUpQuantityIsInvented()
    {
        var policy = new ReorderPolicy(Minimum: 2, Maximum: 50, ReorderPoint: 5, ReorderQuantity: 20);
        var demand = new[] { Line(Sr(1), 10) };

        var with = Calc(Stock(4, 0, policy), demand);
        var without = Calc(Stock(4, 0), demand);

        Assert.Equal(without.StockOnlyShortage, with.StockOnlyShortage);
        Assert.Equal(6m, with.StockOnlyShortage);
        Assert.Equal(ReorderTopUpStatus.PolicyPresentNotEvaluated, with.ReorderTopUp.Status);
        Assert.Null(with.ReorderTopUp.SuggestedQuantity);
        Assert.Equal(policy, with.ReorderTopUp.Policy);
        Assert.Equal(ReorderTopUpStatus.NotConfigured, without.ReorderTopUp.Status);
        Assert.True(with.IsDefinitive);                         // top-up ارزیابی‌نشده، قطعی‌بودن کمبود را تغییر نمی‌دهد
    }

    [Fact]
    public void ReorderTopUp_IsNotCalculable_WhenStockIsNotCalculable()
    {
        var r = Calc(null, new[] { Line(Sr(1), 3) });

        Assert.Equal(ReorderTopUpStatus.NotCalculable, r.ReorderTopUp.Status);
    }

    [Fact]
    public void LinesOrStockOfAnotherMaterialOrWarehouse_AreRejectedAsAProgrammingError()
    {
        var foreign = new DemandLine(Sr(1), Item + 1, Wh, 1, 0);
        Assert.Throws<ArgumentException>(() => Calc(Stock(1, 0), new[] { foreign }));

        var foreignStock = new UsableStockResult(Item, Wh + 1, StockState.Ok, 1, 0, 1, 1, null, Array.Empty<NetDemandIssue>());
        Assert.Throws<ArgumentException>(() => Calc(foreignStock, Array.Empty<DemandLine>()));
    }

    [Fact]
    public void Arithmetic_UsesDecimal_WithoutFloatingPointDrift()
    {
        var r = Calc(Stock(0.3m, 0), new[] { Line(Sr(1), 0.1m), Line(Sr(2, 101), 0.2m), Line(Sr(3, 102), 0.3m) });

        Assert.Equal(0.6m, r.GrossEligibleDemand);
        Assert.Equal(0.3m, r.StockOnlyShortage);
    }
}
