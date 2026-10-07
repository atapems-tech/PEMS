using PEMS.Application.DTOs;
using PEMS.Application.PartCatalogue;
using PEMS.Web.Routing;

namespace PEMS.Tests.PartCatalogue;

/// <summary>قواعد خالص کاتالوگ قطعه + مسیریاب گردش‌کار (فلوچارت) + اعتبارسنج + ریدایرکت‌های قدیمی Warehouse.</summary>
public class PartCatalogueRulesAndRoutingTests
{
    // ------------------------------------------------------------------ Rules

    [Theory]
    [InlineData("A", 5, 7, 0, 0)]
    [InlineData("a", 5, 7, 0, 0)]
    [InlineData("B", 5, 7, 5, 7)]
    [InlineData(null, 5, 7, 5, 7)]
    public void PlanningMethodA_ZeroesMinStockAndConsumption(string? code, int min, int max, int expMin, int expMax)
    {
        var (m, x) = PartCatalogueRules.ApplyPlanningMethod(code, min, max);
        Assert.Equal(expMin, m);
        Assert.Equal(expMax, x);
    }

    [Fact]
    public void LocalManufacturer_ForcesNoPartNumber()
    {
        Assert.Equal("NO_PART_NR", PartCatalogueRules.ApplyManufacturerPartNumber(true, "ABC-1"));
        Assert.Equal("ABC-1", PartCatalogueRules.ApplyManufacturerPartNumber(false, " ABC-1 "));
        Assert.Null(PartCatalogueRules.ApplyManufacturerPartNumber(false, "  "));
    }

    [Theory]
    [InlineData("SIEMENS (wrong)", true)]
    [InlineData("SIEMENS (WRONG)  ", true)]
    [InlineData("SIEMENS", false)]
    public void WrongManufacturers_AreDetected(string name, bool wrong) =>
        Assert.Equal(wrong, PartCatalogueRules.IsWrongName(name));

    [Fact]
    public void FreeFormatText_IsSingleLine_AndNeverEmpty()
    {
        Assert.Equal("A B C", PartCatalogueRules.NormalizeFreeFormatText("A\r\nB\tC"));
        Assert.Equal(" ", PartCatalogueRules.NormalizeFreeFormatText(null));
        Assert.Equal(" ", PartCatalogueRules.NormalizeFreeFormatText("   \n "));
        Assert.True(PartCatalogueRules.ContainsLineBreak("a\nb"));
        Assert.False(PartCatalogueRules.ContainsLineBreak("a b"));
    }

    [Fact]
    public void ChangeVendorInfo_IsAppendedOnce()
    {
        var once = PartCatalogueRules.AppendOldVendorInfo("BALL VALVE", "OLD CO", "X1", true, true);
        Assert.Contains("VENDOR NAME: OLD CO", once);
        Assert.Contains("VENDOR CODE: X1", once);
        Assert.Equal(once, PartCatalogueRules.AppendOldVendorInfo(once, "OLD CO", "X1", true, true));
    }

    [Theory]
    [InlineData("SEAL KIT POS NO:54", true)]
    [InlineData("see item no 6100-015A", true)]
    [InlineData("BALL VALVE 2 IN", false)]
    public void PosOrItemReference_IsDetected(string text, bool expected) =>
        Assert.Equal(expected, PartCatalogueRules.ReferencesPosOrItem(text));

    [Theory]
    [InlineData("Critical Part", true, false)]
    [InlineData("Capital Part", false, true)]
    [InlineData("CR-CAP", true, true)]
    [InlineData("Normal", false, false)]
    public void TypeDesignation_DerivesCriticalAndCapital(string code, bool cr, bool cap) =>
        Assert.Equal((cr, cap), PartCatalogueRules.DeriveTypeFlags(code));

    [Fact]
    public void NewManufacturer_ExactAndSimilarMatchesAreFound()
    {
        var existing = new[] { ("SIEMENS", PartCatalogueRules.NormalizeName("SIEMENS")), ("SKF", "SKF"), ("ABB LTD", "ABBLTD") };
        var (exact, similar) = PartCatalogueRules.FindManufacturerMatches("Siemens", existing);
        Assert.Single(exact);
        Assert.Empty(similar);

        (exact, similar) = PartCatalogueRules.FindManufacturerMatches("SIEMENS AG", existing);
        Assert.Empty(exact);
        Assert.Contains("SIEMENS", similar);

        Assert.False(PartCatalogueRules.IsLatinManufacturerName("زیمنس"));
        Assert.Equal("ACME CO", PartCatalogueRules.NormalizeNewManufacturerName(" acme   co "));
    }

    // ------------------------------------------------------------------ Routing planner

    private static readonly RoutingStepDef[] Defs =
    {
        new("DirectManager", 10, "Approval", "DirectManager", "Always"),
        new("SuperiorManager", 20, "Approval", "SuperiorManager", "Always"),
        new("MaintenanceSupervisor", 30, "Review", "AssignedPool", "CriticalOrCapital"),
        new("SparePartsSpecialist", 40, "Review", "AssignedPool", "SectionNeedsSpareReview"),
        new("SourcingUnit", 50, "Approval", "AssignedPool", "NewManufacturer"),
        new("CommodityGroupReview", 60, "Review", "AssignedPool", "Always"),
        new("PartCataloguer", 70, "Registration", "AssignedPool", "Always"),
        new("InventoryControl", 80, "Review", "AssignedPool", "PlanningMethodNotA"),
    };

    private static RoutingContext Ctx(Dictionary<int, int> managers, IEnumerable<int>? ceo = null, int requester = 1,
        bool critical = false, bool capital = false, bool spare = false, bool newMfr = false, string? planning = "B") =>
        new(requester, critical, capital, spare, newMfr, planning,
            e => managers.TryGetValue(e, out var m) ? m : null,
            (ceo ?? Array.Empty<int>()).ToHashSet(),
            _ => new List<int> { 90 });

    private static PlannedStep Step(RoutingResult r, string key) => r.Steps.Single(s => s.StepKey == key);

    [Fact]
    public void FullRoute_WhenAllConditionsHold()
    {
        var r = PartCatalogueRoutingPlanner.Plan(Defs, Ctx(new() { [1] = 2, [2] = 3 }, critical: true, spare: true, newMfr: true));
        Assert.True(r.IsValid);
        Assert.All(r.Steps, s => Assert.False(s.Skipped));
        Assert.Equal(2, Step(r, "DirectManager").AssignedEmployeeId);
        Assert.Equal(3, Step(r, "SuperiorManager").AssignedEmployeeId);
    }

    [Fact]
    public void DirectManagerIsCeo_SkipsBothManagerSteps()
    {
        var r = PartCatalogueRoutingPlanner.Plan(Defs, Ctx(new() { [1] = 2 }, ceo: new[] { 2 }));
        Assert.True(r.IsValid);
        Assert.Equal("DirectManagerIsCEO", Step(r, "DirectManager").SkipReason);
        Assert.Equal("DirectManagerIsCEO", Step(r, "SuperiorManager").SkipReason);
    }

    [Fact]
    public void SuperiorIsCeo_SkipsOnlySuperiorStep()
    {
        var r = PartCatalogueRoutingPlanner.Plan(Defs, Ctx(new() { [1] = 2, [2] = 3 }, ceo: new[] { 3 }));
        Assert.False(Step(r, "DirectManager").Skipped);
        Assert.Equal("SuperiorIsCEO", Step(r, "SuperiorManager").SkipReason);
    }

    [Fact]
    public void RequesterIsCeo_SkipsManagerSteps()
    {
        var r = PartCatalogueRoutingPlanner.Plan(Defs, Ctx(new(), ceo: new[] { 1 }));
        Assert.True(r.IsValid);
        Assert.Equal("RequesterIsCEO", Step(r, "DirectManager").SkipReason);
        Assert.Equal("RequesterIsCEO", Step(r, "SuperiorManager").SkipReason);
    }

    [Fact]
    public void MissingDirectManager_BlocksSubmit()
    {
        var r = PartCatalogueRoutingPlanner.Plan(Defs, Ctx(new()));
        Assert.False(r.IsValid);
        Assert.Contains(r.Errors, e => e.Key == PartCatalogueRoutingPlanner.ErrNoDirectManager);
    }

    [Fact]
    public void NoSuperior_SkipsSuperiorWithoutError()
    {
        var r = PartCatalogueRoutingPlanner.Plan(Defs, Ctx(new() { [1] = 2 }));
        Assert.True(r.IsValid);
        Assert.Equal("NoSuperiorManager", Step(r, "SuperiorManager").SkipReason);
    }

    [Theory]
    [InlineData(false, false, "Condition:CriticalOrCapital")]
    [InlineData(true, false, null)]
    [InlineData(false, true, null)]
    public void CriticalOrCapital_DrivesMaintenanceSupervisor(bool cr, bool ca, string? skip)
    {
        var r = PartCatalogueRoutingPlanner.Plan(Defs, Ctx(new() { [1] = 2, [2] = 3 }, critical: cr, capital: ca));
        Assert.Equal(skip, Step(r, "MaintenanceSupervisor").SkipReason);
    }

    [Fact]
    public void SectionAndNewManufacturerBranches()
    {
        var off = PartCatalogueRoutingPlanner.Plan(Defs, Ctx(new() { [1] = 2, [2] = 3 }));
        Assert.True(Step(off, "SparePartsSpecialist").Skipped);
        Assert.True(Step(off, "SourcingUnit").Skipped);

        var on = PartCatalogueRoutingPlanner.Plan(Defs, Ctx(new() { [1] = 2, [2] = 3 }, spare: true, newMfr: true));
        Assert.False(Step(on, "SparePartsSpecialist").Skipped);
        Assert.False(Step(on, "SourcingUnit").Skipped);
    }

    [Fact]
    public void PlanningMethodA_RemovesInventoryControl()
    {
        var a = PartCatalogueRoutingPlanner.Plan(Defs, Ctx(new() { [1] = 2, [2] = 3 }, planning: "A"));
        Assert.True(Step(a, "InventoryControl").Skipped);
        var b = PartCatalogueRoutingPlanner.Plan(Defs, Ctx(new() { [1] = 2, [2] = 3 }, planning: "B"));
        Assert.False(Step(b, "InventoryControl").Skipped);
    }

    [Fact]
    public void RequesterIsNeverInTheApproverPool_AndEmptyPoolBlocks()
    {
        var ctx = new RoutingContext(1, false, false, false, false, "B", e => e == 1 ? 2 : (e == 2 ? 3 : null),
            new HashSet<int>(), _ => new List<int> { 1 });
        var r = PartCatalogueRoutingPlanner.Plan(Defs, ctx);
        Assert.False(r.IsValid);
        Assert.Contains(r.Errors, e => e.Key == PartCatalogueRoutingPlanner.ErrNoApprover);
    }

    // ------------------------------------------------------------------ Validator

    private static PartCatalogueFormDto ValidForm() => new()
    {
        RequestType = "Add", SectionId = 1, TechnicalClassId = 1, CommodityGroup1Id = 1, CommodityGroup2Id = 2,
        ProductFamilyId = 1, UnitOfMeasureId = 1, ManufacturerId = 1, ManufacturerPartNumber = "P1",
        MinimumStock = 1, MaxConsumptionRate = 1, StockGradeId = 1,
        Attributes = { new PartCatalogueAttributeDto { Name = "FREE FORMAT TEXT", Value = "BALL VALVE" } }
    };

    private static PartCatalogueValidationContext Vc() => new() { HasRequestNumber = true, PlanningMethodCode = "B" };

    [Fact]
    public void ValidAddRequest_PassesSubmit() =>
        Assert.True(PartCatalogueValidator.Validate(ValidForm(), Vc(), forSubmit: true).Success);

    [Fact]
    public void Submit_RequiresNumber_StockGradeAndFreeFormat()
    {
        var f = ValidForm();
        f.StockGradeId = null;
        f.Attributes.Clear();
        var ctx = Vc(); ctx.HasRequestNumber = false;
        var r = PartCatalogueValidator.Validate(f, ctx, true);
        Assert.Contains(r.Errors, e => e.Key == "Pc_Err_NumberRequired");
        Assert.Contains(r.Errors, e => e.Field == nameof(f.StockGradeId));
        Assert.Contains(r.Errors, e => e.Key == "Pc_Err_FreeFormatRequired");
    }

    [Fact]
    public void PlanningA_DoesNotRequireStockQuantities()
    {
        var f = ValidForm(); f.MinimumStock = null; f.MaxConsumptionRate = null;
        var ctx = Vc(); ctx.PlanningMethodCode = "A";
        Assert.True(PartCatalogueValidator.Validate(f, ctx, true).Success);
    }

    [Fact]
    public void FreeFormatText_WithLineBreak_IsRejected()
    {
        var f = ValidForm(); f.Attributes[0].Value = "A\nB";
        Assert.Contains(PartCatalogueValidator.Validate(f, Vc(), true).Errors, e => e.Key == "Pc_Err_FreeFormatSingleLine");
    }

    [Fact]
    public void WrongManufacturer_AndNonLatinNewManufacturer_AreRejected()
    {
        var f = ValidForm();
        var ctx = Vc(); ctx.ManufacturerIsWrong = true;
        Assert.Contains(PartCatalogueValidator.Validate(f, ctx, false).Errors, e => e.Key == "Pc_Err_ManufacturerWrong");

        var n = ValidForm(); n.IsNewManufacturer = true; n.ManufacturerId = null; n.NewManufacturerName = "زیمنس";
        Assert.Contains(PartCatalogueValidator.Validate(n, Vc(), false).Errors, e => e.Key == "Pc_Err_NewManufacturerLatin");
    }

    [Fact]
    public void LowValueAsset_AboveConfiguredThreshold_IsRejected_OnlyForRial()
    {
        var f = ValidForm(); f.AssetType = "LowValueAsset"; f.PartPrice = 300_000; f.CurrencyId = 1;
        var ctx = Vc(); ctx.CurrencyCode = "IRR"; ctx.LowValueThresholdIrr = 250_000;
        Assert.Contains(PartCatalogueValidator.Validate(f, ctx, false).Errors, e => e.Key == "Pc_Err_LowValueThreshold");

        ctx.LowValueThresholdIrr = 500_000;
        Assert.True(PartCatalogueValidator.Validate(f, ctx, false).Success);

        ctx.CurrencyCode = "USD";
        var r = PartCatalogueValidator.Validate(f, ctx, false);
        Assert.True(r.Success);
        Assert.Contains(r.Warnings, w => w.Key == "Pc_Warn_LowValueNotComparable");
    }

    [Fact]
    public void PosItem_WithoutPartNumber_NeedsAttachment()
    {
        var f = ValidForm(); f.ManufacturerPartNumber = null; f.Attributes[0].Value = "SEAL POS NO:54";
        Assert.Contains(PartCatalogueValidator.Validate(f, Vc(), true).Errors, e => e.Key == "Pc_Err_PosItemNeedsAttachment");
        var ctx = Vc(); ctx.DrawingOrDocumentAttachmentCount = 1;
        Assert.True(PartCatalogueValidator.Validate(f, ctx, true).Success);
    }

    [Fact]
    public void Change_RequiresExistingPart_LocksUom_AndRequiresSupportDocs()
    {
        var f = ValidForm(); f.RequestType = "Change"; f.ExistingPartNumber = "PN-1"; f.ChangesMatchInventory = false;
        var ctx = Vc(); ctx.ExistingPartResolved = true; ctx.ExistingPartUnitOfMeasureId = 9;
        var r = PartCatalogueValidator.Validate(f, ctx, true);
        Assert.Contains(r.Errors, e => e.Key == "Pc_Err_ChangeUomLocked");
        Assert.Contains(r.Errors, e => e.Key == "Pc_Err_ChangeSupportDocs");

        var missing = PartCatalogueValidator.Validate(f, Vc(), true);
        Assert.Contains(missing.Errors, e => e.Key == "Pc_Err_ChangePartNotFound");
    }

    [Fact]
    public void Section_RestrictsAllowedCommodityGroups()
    {
        var f = ValidForm();
        var ctx = Vc(); ctx.AllowedGroup1Ids = new HashSet<int> { 7 }; ctx.AllowedGroup2Ids = new HashSet<int> { 2 };
        var r = PartCatalogueValidator.Validate(f, ctx, false);
        Assert.Contains(r.Errors, e => e.Key == "Pc_Err_Group1NotAllowed");
        Assert.DoesNotContain(r.Errors, e => e.Key == "Pc_Err_Group2NotAllowed");
    }

    // ------------------------------------------------------------------ Legacy redirects

    [Theory]
    [InlineData("StoreRequisition", null, null, "", "/Warehouse/StoreRequisition")]
    [InlineData("StoreRequisition", "Details", "5", "?x=1", "/Warehouse/StoreRequisition/Details/5?x=1")]
    [InlineData("MaterialItem", "Edit", "a b", "", "/Warehouse/MaterialItem/Edit/a%20b")]
    public void LegacyWarehouseUrls_MapToTheWarehouseArea(string controller, string? action, string? id, string query, string expected) =>
        Assert.Equal(expected, WarehouseLegacyRedirects.BuildTarget(controller, action, id, query));

    [Fact]
    public void LegacyRedirects_CoverAllMovedControllers_AndNotPartCatalogue()
    {
        Assert.Equal(7, WarehouseLegacyRedirects.Controllers.Length);
        Assert.DoesNotContain("PartCatalogueRequest", WarehouseLegacyRedirects.Controllers);
    }
}
