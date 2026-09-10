using Microsoft.EntityFrameworkCore;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Cmms;

/// <summary>
/// طبق «PEMS Phase 7.5» بند ۱۵: پیش از این فاز، JobPlan.EquipmentClassId/PermitRequired/
/// LOTORequired (از فاز ۴، مصرف‌شده توسط WorkOrderReleaseGateService فاز ۶) هیچ مسیر UI ای
/// برای تنظیم نداشتند. این تست‌ها اثبات می‌کنند حالا این سه فیلد واقعاً از طریق
/// JobPlanService.CreateAsync/UpdateAsync قابل تنظیم و بازیابی هستند.
/// </summary>
public class JobPlanSafetyFieldsTests
{
    private class FakeNumberingService : INumberingService
    {
        public Task<string> GetNextNumberAsync(string documentType, string? scopeKey = null) =>
            Task.FromResult($"{documentType}-0001");
    }

    [Fact]
    public async Task CreateAsync_WithSafetyFields_PersistsEquipmentClassAndPermitAndLoto()
    {
        var ctx = PemsDbContextFactory.CreateInMemory($"Phase75_{nameof(CreateAsync_WithSafetyFields_PersistsEquipmentClassAndPermitAndLoto)}");
        var equipmentClass = new PEMS.Domain.Entities.EquipmentClass { Code = "TEST", Name = "Test", IsActive = true };
        ctx.EquipmentClasses.Add(equipmentClass);
        await ctx.SaveChangesAsync();

        var service = new JobPlanService(ctx, new FakeNumberingService());
        var (success, error, id) = await service.CreateAsync("Overhaul", "scope", 4, "safety notes", "tester",
            equipmentClass.EquipmentClassId, permitRequired: true, lotoRequired: true);

        Assert.True(success);
        Assert.Null(error);
        var jobPlan = await ctx.JobPlans.AsNoTracking().SingleAsync(x => x.JobPlanId == id);
        Assert.Equal(equipmentClass.EquipmentClassId, jobPlan.EquipmentClassId);
        Assert.True(jobPlan.PermitRequired);
        Assert.True(jobPlan.LOTORequired);
    }

    [Fact]
    public async Task CreateAsync_WithoutSafetyFields_DefaultsToFalseAndUnlinked()
    {
        var ctx = PemsDbContextFactory.CreateInMemory($"Phase75_{nameof(CreateAsync_WithoutSafetyFields_DefaultsToFalseAndUnlinked)}");
        var service = new JobPlanService(ctx, new FakeNumberingService());

        var (success, _, id) = await service.CreateAsync("Routine Check", null, null, null, "tester");

        Assert.True(success);
        var jobPlan = await ctx.JobPlans.AsNoTracking().SingleAsync(x => x.JobPlanId == id);
        Assert.Null(jobPlan.EquipmentClassId);
        Assert.False(jobPlan.PermitRequired);
        Assert.False(jobPlan.LOTORequired);
    }

    [Fact]
    public async Task UpdateAsync_ChangesSafetyFields()
    {
        var ctx = PemsDbContextFactory.CreateInMemory($"Phase75_{nameof(UpdateAsync_ChangesSafetyFields)}");
        var service = new JobPlanService(ctx, new FakeNumberingService());
        var (_, _, id) = await service.CreateAsync("Overhaul", null, null, null, "tester");

        var (success, error) = await service.UpdateAsync(id!.Value, "Overhaul", null, null, null, "tester2",
            permitRequired: true, lotoRequired: false);

        Assert.True(success);
        Assert.Null(error);
        var jobPlan = await ctx.JobPlans.AsNoTracking().SingleAsync(x => x.JobPlanId == id);
        Assert.True(jobPlan.PermitRequired);
        Assert.False(jobPlan.LOTORequired);
    }
}
