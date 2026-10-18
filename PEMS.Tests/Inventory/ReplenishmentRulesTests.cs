using PEMS.Application.Inventory;
using PEMS.Application.Replenishment;
using static PEMS.Tests.Inventory.ReplenishmentTestKit;

namespace PEMS.Tests.Inventory;

/// <summary>Phase 2 / P5 - قواعد خالص: قطعیت، مسدودسازی چندمحلی، اثر انگشت و قاعده‌ی تغییر مادی (بدون EF).</summary>
public class ReplenishmentRulesTests
{
    [Fact]
    public void DefinitiveShortage_IsRecommendedExactlyAsCalculated_WithNoticesOnly()
    {
        var r = Result(1, 1, Stock(1, 1, onHand: 3), SrLine(10, 1, 1, 8));

        var e = ReplenishmentRules.Evaluate(r);

        Assert.True(e.IsDefinitive);
        Assert.Equal(5m, e.Shortage);                                   // 8 - 3: بدون Reorder/Buffer/گرد کردن
        Assert.Empty(e.BlockingCodes);
        Assert.Contains("IncomingSupplyNotModeled", e.NoticeCodes);
    }

    [Fact]
    public void StockCoveringDemand_IsDefinitiveWithZeroShortage()
    {
        var e = ReplenishmentRules.Evaluate(Result(1, 1, Stock(1, 1, onHand: 10), SrLine(10, 1, 1, 8)));

        Assert.True(e.IsDefinitive);
        Assert.Equal(0m, e.Shortage);
    }

    [Fact]
    public void MultiLocationStock_IsBlocking_ForADefinitiveRecommendation()
    {
        // در P1 این هشدار غیرمسدودکننده است؛ طبق تصمیم مالک (Option C) برای توصیه‌ی قطعی مسدودکننده است
        var r = Result(1, 1, Stock(1, 1, onHand: 3, reserved: 0, NetDemandIssueCode.StockMultipleLocationRows), SrLine(10, 1, 1, 8));
        Assert.True(r.IsDefinitive);                                    // خودِ P3 دست‌نخورده می‌ماند

        var e = ReplenishmentRules.Evaluate(r);

        Assert.False(e.IsDefinitive);
        Assert.Contains("StockMultipleLocationRows", e.BlockingCodes);
    }

    [Fact]
    public void AmbiguousRowsAndNoWarehouseDemand_NeverProduceADefinitiveRecommendation()
    {
        var ambiguous = ReplenishmentRules.Evaluate(AmbiguousStock(1, 1, SrLine(10, 1, 1, 8)));
        var noWarehouse = ReplenishmentRules.Evaluate(NoWarehouse(1, 5));

        Assert.False(ambiguous.IsDefinitive);
        Assert.False(noWarehouse.IsDefinitive);
        Assert.Contains("NotComparableNoWarehouse", noWarehouse.BlockingCodes);
    }

    [Fact]
    public void Fingerprint_IsDeterministic_AndChangesOnlyWithMaterialInputs()
    {
        var a = Result(1, 1, Stock(1, 1, 3), SrLine(10, 1, 1, 8), SrLine(11, 1, 1, 2));
        var sameOrderSwapped = Result(1, 1, Stock(1, 1, 3), SrLine(11, 1, 1, 2), SrLine(10, 1, 1, 8));
        var moreStock = Result(1, 1, Stock(1, 1, 4), SrLine(10, 1, 1, 8), SrLine(11, 1, 1, 2));
        var newSource = Result(1, 1, Stock(1, 1, 3), SrLine(10, 1, 1, 8), SrLine(11, 1, 1, 2), SrLine(12, 1, 1, 1));

        string F(NetDemandResult r) => ReplenishmentRules.Fingerprint(r, ReplenishmentRules.Evaluate(r));

        Assert.Equal(F(a), F(a));
        Assert.Equal(F(a), F(sameOrderSwapped));                        // مستقل از ترتیب ورودی
        Assert.NotEqual(F(a), F(moreStock));
        Assert.NotEqual(F(a), F(newSource));
        Assert.Equal(64, F(a).Length);
    }

    [Theory]
    [InlineData(5, "S:1,S:2", 5, "S:1,S:2", false)]    // برابر ⇒ باز نمی‌شود
    [InlineData(5, "S:1,S:2", 4, "S:1,S:2", false)]    // کمتر ⇒ باز نمی‌شود
    [InlineData(5, "S:1,S:2", 5.001, "S:1,S:2", true)]  // اکیداً بیشتر (حتی کوچک) ⇒ باز می‌شود؛ آستانه‌ی ساختگی نداریم
    [InlineData(5, "S:1,S:2", 3, "S:1,S:2,S:3", true)] // منبع جدید ⇒ باز می‌شود حتی با کمبود کمتر
    [InlineData(5, "S:1,S:2", 3, "S:2", false)]        // منبع کمتر ⇒ باز نمی‌شود
    public void MaterialChangeRule_IsStrictAndDeterministic(double dismissed, string dismissedKeys, double current, string currentKeys, bool expected)
    {
        var result = ReplenishmentRules.IsMaterialChange((decimal)dismissed, dismissedKeys, (decimal)current, currentKeys.Split(','));

        Assert.Equal(expected, result);
    }

    [Fact]
    public void WorkOrderDemandWithoutAWarehouse_IsExplicitlyMarkedAsRequiringWarehouseAttribution()
    {
        var noWarehouse = ReplenishmentRules.Evaluate(NoWarehouse(1, 5));
        var withItemLevelIssue = ReplenishmentRules.Evaluate(NetDemandCalculator.Calculate(new NetDemandInput(1, 1, Stock(1, 1, 3),
            new[] { SrLine(10, 1, 1, 8) }, Array.Empty<ExcludedDemand>(), new[] { new NetDemandIssue(NetDemandIssueCode.WorkOrderDemandNotAttributableToWarehouse) })));

        Assert.False(noWarehouse.IsDefinitive);
        Assert.Contains(ReplenishmentRules.RequiresWarehouseAttribution, noWarehouse.BlockingCodes);
        Assert.False(withItemLevelIssue.IsDefinitive);                   // نتیجه‌ی انبار-دار همان متریال هم قطعی نیست و کد صریح دارد
        Assert.Contains(ReplenishmentRules.RequiresWarehouseAttribution, withItemLevelIssue.BlockingCodes);
    }
}
