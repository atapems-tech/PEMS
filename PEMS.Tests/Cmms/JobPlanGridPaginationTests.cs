using PEMS.Domain.Entities;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Cmms;

/// <summary>
/// طبق «PEMS Phase 6B Wave B1» بند P1-01: قبلاً کل JobPlanهای فعال + کل MaintenanceTasks + کل
/// MaintenancePlans به‌صورت جداگانه در حافظه بارگذاری می‌شدند (GroupBy→Dictionary→lookup در
/// foreach) - بدون Skip/Take. حالا TaskCount/PlanUsageCount با Correlated Subquery در همون
/// Select محاسبه می‌شوند.
/// </summary>
public class JobPlanGridPaginationTests
{
    private static JobPlanService CreateService(string dbName, out PEMS.Persistence.Context.PEMSDbContext ctx)
    {
        ctx = PemsDbContextFactory.CreateInMemory($"JobPlanGrid_{dbName}");
        return new JobPlanService(ctx, null!);
    }

    private static int _nextId = 1;

    private static JobPlan NewJobPlan(string number, bool isActive = true) => new()
    {
        JobPlanId = _nextId++,
        JobPlanNumber = number,
        Title = $"Job Plan {number}",
        IsActive = isActive
    };

    [Fact]
    public async Task GetListAsync_DefaultPageSize_Is50()
    {
        var service = CreateService(nameof(GetListAsync_DefaultPageSize_Is50), out var ctx);
        for (var i = 0; i < 60; i++)
            ctx.JobPlans.Add(NewJobPlan($"JP-{i:D3}"));
        await ctx.SaveChangesAsync();

        var result = await service.GetListAsync(1, 999);

        Assert.Equal(50, result.PageSize);
        Assert.Equal(50, result.Items.Count);
        Assert.Equal(60, result.TotalCount);
    }

    [Theory]
    [InlineData(20)]
    [InlineData(50)]
    [InlineData(100)]
    public async Task GetListAsync_AllowedPageSizes_ReturnExactlyThatCount(int pageSize)
    {
        var service = CreateService($"{nameof(GetListAsync_AllowedPageSizes_ReturnExactlyThatCount)}_{pageSize}", out var ctx);
        for (var i = 0; i < 120; i++)
            ctx.JobPlans.Add(NewJobPlan($"JP-{i:D3}"));
        await ctx.SaveChangesAsync();

        var result = await service.GetListAsync(1, pageSize);

        Assert.Equal(pageSize, result.PageSize);
        Assert.Equal(Math.Min(pageSize, 120), result.Items.Count);
    }

    [Fact]
    public async Task GetListAsync_InvalidPageSize_FallsBackTo50()
    {
        var service = CreateService(nameof(GetListAsync_InvalidPageSize_FallsBackTo50), out var ctx);
        ctx.JobPlans.Add(NewJobPlan("JP-001"));
        await ctx.SaveChangesAsync();

        var result = await service.GetListAsync(1, 13);

        Assert.Equal(50, result.PageSize);
    }

    [Fact]
    public async Task GetListAsync_InvalidPage_FallsBackToPage1()
    {
        var service = CreateService(nameof(GetListAsync_InvalidPage_FallsBackToPage1), out var ctx);
        ctx.JobPlans.Add(NewJobPlan("JP-001"));
        await ctx.SaveChangesAsync();

        var result = await service.GetListAsync(0, 20);

        Assert.Equal(1, result.Page);
        Assert.Single(result.Items);
    }

    [Fact]
    public async Task GetListAsync_Page1AndPage2_AreStableAndDisjoint()
    {
        var service = CreateService(nameof(GetListAsync_Page1AndPage2_AreStableAndDisjoint), out var ctx);
        for (var i = 0; i < 25; i++)
            ctx.JobPlans.Add(NewJobPlan($"JP-{i:D3}"));
        await ctx.SaveChangesAsync();

        var page1 = await service.GetListAsync(1, 20);
        var page2 = await service.GetListAsync(2, 20);

        Assert.Equal(25, page1.TotalCount);
        Assert.Equal(25, page2.TotalCount);
        Assert.Equal(20, page1.Items.Count);
        Assert.Equal(5, page2.Items.Count);
        Assert.Equal(2, page1.TotalPages);

        var page1Ids = page1.Items.Select(x => x.JobPlanId).ToList();
        var page2Ids = page2.Items.Select(x => x.JobPlanId).ToList();
        Assert.Empty(page1Ids.Intersect(page2Ids));
        Assert.NotEqual(page1Ids, page2Ids);
    }

    [Fact]
    public async Task GetListAsync_DeterministicOrdering_ByJobPlanNumber()
    {
        var service = CreateService(nameof(GetListAsync_DeterministicOrdering_ByJobPlanNumber), out var ctx);
        ctx.JobPlans.Add(NewJobPlan("JP-003"));
        ctx.JobPlans.Add(NewJobPlan("JP-001"));
        ctx.JobPlans.Add(NewJobPlan("JP-002"));
        await ctx.SaveChangesAsync();

        var result = await service.GetListAsync(1, 20);

        Assert.Equal(new[] { "JP-001", "JP-002", "JP-003" }, result.Items.Select(x => x.JobPlanNumber));
    }

    [Fact]
    public async Task GetListAsync_TieBreaker_SameJobPlanNumber_OrderedByJobPlanId()
    {
        var service = CreateService(nameof(GetListAsync_TieBreaker_SameJobPlanNumber_OrderedByJobPlanId), out var ctx);
        var second = NewJobPlan("JP-SAME");
        ctx.JobPlans.Add(second);
        var first = NewJobPlan("JP-SAME");
        ctx.JobPlans.Add(first);
        await ctx.SaveChangesAsync();

        var run1 = await service.GetListAsync(1, 20);
        var run2 = await service.GetListAsync(1, 20);

        Assert.Equal(run1.Items.Select(x => x.JobPlanId), run2.Items.Select(x => x.JobPlanId));
        // ترتیب باید بر اساس JobPlanId صعودی باشد (tie-breaker) - هر دو JobPlanNumber یکسان‌اند
        var orderedIds = new[] { second.JobPlanId, first.JobPlanId }.OrderBy(id => id).ToList();
        Assert.Equal(orderedIds, run1.Items.Select(x => x.JobPlanId).ToList());
    }

    [Fact]
    public async Task GetListAsync_InactiveJobPlans_AreExcluded()
    {
        var service = CreateService(nameof(GetListAsync_InactiveJobPlans_AreExcluded), out var ctx);
        var active = NewJobPlan("JP-ACTIVE");
        var inactive = NewJobPlan("JP-INACTIVE", isActive: false);
        ctx.JobPlans.Add(active);
        ctx.JobPlans.Add(inactive);
        await ctx.SaveChangesAsync();

        var result = await service.GetListAsync(1, 20);

        Assert.Single(result.Items);
        Assert.Equal(active.JobPlanId, result.Items[0].JobPlanId);
    }

    [Fact]
    public async Task GetListAsync_EmptyDataset_ReturnsEmptyPagedResult()
    {
        var service = CreateService(nameof(GetListAsync_EmptyDataset_ReturnsEmptyPagedResult), out _);

        var result = await service.GetListAsync(1, 20);

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
        Assert.Equal(0, result.TotalPages);
    }

    [Fact]
    public async Task GetListAsync_TaskCount_CountsOnlyActiveTasksForThatJobPlan()
    {
        var service = CreateService(nameof(GetListAsync_TaskCount_CountsOnlyActiveTasksForThatJobPlan), out var ctx);
        var jp1 = NewJobPlan("JP-001");
        var jp2 = NewJobPlan("JP-002");
        ctx.JobPlans.Add(jp1);
        ctx.JobPlans.Add(jp2);
        ctx.MaintenanceTasks.Add(new MaintenanceTask { JobPlanId = jp1.JobPlanId, TaskNumber = 1, Title = "T1", IsActive = true });
        ctx.MaintenanceTasks.Add(new MaintenanceTask { JobPlanId = jp1.JobPlanId, TaskNumber = 2, Title = "T2", IsActive = true });
        ctx.MaintenanceTasks.Add(new MaintenanceTask { JobPlanId = jp1.JobPlanId, TaskNumber = 3, Title = "T3 inactive", IsActive = false });
        ctx.MaintenanceTasks.Add(new MaintenanceTask { JobPlanId = jp2.JobPlanId, TaskNumber = 1, Title = "Other JobPlan's task", IsActive = true });
        await ctx.SaveChangesAsync();

        var result = await service.GetListAsync(1, 20);

        Assert.Equal(2, result.Items.Single(x => x.JobPlanId == jp1.JobPlanId).TaskCount);
        Assert.Equal(1, result.Items.Single(x => x.JobPlanId == jp2.JobPlanId).TaskCount);
    }

    [Fact]
    public async Task GetListAsync_PlanUsageCount_CountsOnlyActiveMaintenancePlansForThatJobPlan()
    {
        var service = CreateService(nameof(GetListAsync_PlanUsageCount_CountsOnlyActiveMaintenancePlansForThatJobPlan), out var ctx);
        ctx.Equipments.Add(new Equipment { EquipmentId = 1, TagNumber = "EQ-001", Name = "EQ-001", EquipmentType = "Pump", IsActive = true });
        var jp1 = NewJobPlan("JP-001");
        var jp2 = NewJobPlan("JP-002");
        ctx.JobPlans.Add(jp1);
        ctx.JobPlans.Add(jp2);
        ctx.MaintenancePlans.Add(new MaintenancePlan { EquipmentId = 1, JobPlanId = jp1.JobPlanId, Title = "P1", TriggerType = "Calendar", IsActive = true });
        ctx.MaintenancePlans.Add(new MaintenancePlan { EquipmentId = 1, JobPlanId = jp1.JobPlanId, Title = "P2", TriggerType = "Calendar", IsActive = true });
        ctx.MaintenancePlans.Add(new MaintenancePlan { EquipmentId = 1, JobPlanId = jp1.JobPlanId, Title = "P3 inactive", TriggerType = "Calendar", IsActive = false });
        ctx.MaintenancePlans.Add(new MaintenancePlan { EquipmentId = 1, JobPlanId = null, Title = "No JobPlan", TriggerType = "Calendar", IsActive = true });
        await ctx.SaveChangesAsync();

        var result = await service.GetListAsync(1, 20);

        Assert.Equal(2, result.Items.Single(x => x.JobPlanId == jp1.JobPlanId).PlanUsageCount);
        Assert.Equal(0, result.Items.Single(x => x.JobPlanId == jp2.JobPlanId).PlanUsageCount);
    }
}
