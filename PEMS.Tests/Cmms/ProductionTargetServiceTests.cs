using PEMS.Domain.Entities;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Cmms;

/// <summary>
/// طبق «PEMS Operations Phase 2A — Foundational Hardening» بخش ۶: ProductionTarget فقط هدف را
/// نگه می‌دارد - Actual/Variance این‌جا تست نمی‌شود (در OperationsReportServiceTests).
/// </summary>
public class ProductionTargetServiceTests
{
    private static PEMS.Persistence.Context.PEMSDbContext CreateContext(string dbName) =>
        PemsDbContextFactory.CreateInMemory($"ProductionTarget_{dbName}");

    private static async Task<(MaterialItem material, ProductionWorkCenter workCenter)> SeedBaselineAsync(PEMS.Persistence.Context.PEMSDbContext ctx)
    {
        var fl = new FunctionalLocation { Code = "FL-1", Name = "Area 100", IsActive = true };
        ctx.FunctionalLocations.Add(fl);
        await ctx.SaveChangesAsync();

        var material = new MaterialItem { MaterialCode = "MAT-1", Description = "Test Material", IsActive = true };
        ctx.MaterialItems.Add(material);

        var workCenter = new ProductionWorkCenter { Code = "WC-1", Name = "Line 1", FunctionalLocationId = fl.FunctionalLocationId, IsActive = true };
        ctx.ProductionWorkCenters.Add(workCenter);

        await ctx.SaveChangesAsync();
        return (material, workCenter);
    }

    [Fact]
    public async Task CreateAsync_ValidInput_Succeeds()
    {
        var ctx = CreateContext(nameof(CreateAsync_ValidInput_Succeeds));
        var (material, wc) = await SeedBaselineAsync(ctx);
        var service = new ProductionTargetService(ctx);

        var (success, error, targetId) = await service.CreateAsync(
            DateTime.Today, null, material.MaterialItemId, wc.ProductionWorkCenterId, null, 100m, null, null);

        Assert.True(success, error);
        Assert.NotNull(targetId);
    }

    [Fact]
    public async Task CreateAsync_ZeroOrNegativeQuantity_Fails()
    {
        var ctx = CreateContext(nameof(CreateAsync_ZeroOrNegativeQuantity_Fails));
        var (material, wc) = await SeedBaselineAsync(ctx);
        var service = new ProductionTargetService(ctx);

        var (success, _, _) = await service.CreateAsync(
            DateTime.Today, null, material.MaterialItemId, wc.ProductionWorkCenterId, null, 0m, null, null);

        Assert.False(success);
    }

    [Fact]
    public async Task CreateAsync_InvalidWorkCenter_Fails()
    {
        var ctx = CreateContext(nameof(CreateAsync_InvalidWorkCenter_Fails));
        var (material, _) = await SeedBaselineAsync(ctx);
        var service = new ProductionTargetService(ctx);

        var (success, _, _) = await service.CreateAsync(DateTime.Today, null, material.MaterialItemId, 99999, null, 10m, null, null);

        Assert.False(success);
    }

    [Fact]
    public async Task CreateAsync_DuplicateGrain_SameDateWorkCenterMaterialShift_IsRejected()
    {
        var ctx = CreateContext(nameof(CreateAsync_DuplicateGrain_SameDateWorkCenterMaterialShift_IsRejected));
        var (material, wc) = await SeedBaselineAsync(ctx);
        var service = new ProductionTargetService(ctx);

        var first = await service.CreateAsync(DateTime.Today, null, material.MaterialItemId, wc.ProductionWorkCenterId, null, 100m, null, null);
        Assert.True(first.Success, first.Error);

        // طبق بخش ۶ سند فاز - جلوگیری از هدف تکراری برای همون گرین (تاریخ/مرکزکاری/متریال/شیفت)
        var duplicate = await service.CreateAsync(DateTime.Today, null, material.MaterialItemId, wc.ProductionWorkCenterId, null, 150m, null, null);

        Assert.False(duplicate.Success);
    }

    [Fact]
    public async Task CreateAsync_DifferentShift_SameDateWorkCenterMaterial_Succeeds()
    {
        var ctx = CreateContext(nameof(CreateAsync_DifferentShift_SameDateWorkCenterMaterial_Succeeds));
        var (material, wc) = await SeedBaselineAsync(ctx);

        var shiftA = new ShiftDefinition { Code = "A", Name = "Morning", StartTime = TimeSpan.FromHours(6), EndTime = TimeSpan.FromHours(14), IsActive = true };
        var shiftB = new ShiftDefinition { Code = "B", Name = "Evening", StartTime = TimeSpan.FromHours(14), EndTime = TimeSpan.FromHours(22), IsActive = true };
        ctx.ShiftDefinitions.AddRange(shiftA, shiftB);
        await ctx.SaveChangesAsync();

        var service = new ProductionTargetService(ctx);

        var first = await service.CreateAsync(DateTime.Today, null, material.MaterialItemId, wc.ProductionWorkCenterId, shiftA.ShiftDefinitionId, 100m, null, null);
        var second = await service.CreateAsync(DateTime.Today, null, material.MaterialItemId, wc.ProductionWorkCenterId, shiftB.ShiftDefinitionId, 80m, null, null);

        Assert.True(first.Success, first.Error);
        Assert.True(second.Success, second.Error);
    }

    [Fact]
    public async Task ProductionTarget_NeverStoresActualOrVariance()
    {
        // طبق بخش ۳۶ سند فاز («Architectural Acceptance Criteria»): اثبات ساختاری - نوع
        // ProductionTarget هیچ فیلدی برای مقدار واقعی/Variance ندارد.
        var properties = typeof(ProductionTarget).GetProperties().Select(p => p.Name).ToList();

        Assert.DoesNotContain("ActualQuantity", properties);
        Assert.DoesNotContain("Variance", properties);
        Assert.DoesNotContain("AchievementPercentage", properties);
    }

    [Fact]
    public async Task DeactivateAsync_ExcludesTargetFromList()
    {
        var ctx = CreateContext(nameof(DeactivateAsync_ExcludesTargetFromList));
        var (material, wc) = await SeedBaselineAsync(ctx);
        var service = new ProductionTargetService(ctx);

        var (_, _, targetId) = await service.CreateAsync(DateTime.Today, null, material.MaterialItemId, wc.ProductionWorkCenterId, null, 100m, null, null);
        var deactivateResult = await service.DeactivateAsync(targetId!.Value);
        Assert.True(deactivateResult.Success, deactivateResult.Error);

        var list = await service.GetListAsync();
        Assert.Empty(list);
    }
}
