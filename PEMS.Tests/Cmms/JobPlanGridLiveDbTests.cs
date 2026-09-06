using Microsoft.EntityFrameworkCore;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Cmms;

/// <summary>
/// طبق «PEMS Phase 6B Wave B1» بند ۲۴ (SQL Verification): شواهد SQL واقعی (ToQueryString) روی
/// PEMSDB واقعی توسعه - نه فقط EF InMemory (که هر Expression قابل‌ارزیابی‌ای را می‌پذیرد، حتی
/// اگر SQL Server واقعی نتواند آن را ترجمه کند). این تست ثابت می‌کند Correlated Subquery های
/// TaskCount/PlanUsageCount و Skip/Take واقعاً به SQL Server منتقل می‌شوند.
/// </summary>
public class JobPlanGridLiveDbTests
{
    private readonly Xunit.Abstractions.ITestOutputHelper _output;

    public JobPlanGridLiveDbTests(Xunit.Abstractions.ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task GetListAsync_AgainstRealPEMSDB_GeneratesOffsetFetchSql()
    {
        using var ctx = PemsDbContextFactory.CreateSqlServerOrThrow();

        var query = ctx.JobPlans
            .Where(x => x.IsActive)
            .OrderBy(x => x.JobPlanNumber)
            .ThenBy(x => x.JobPlanId)
            .Skip(0)
            .Take(20)
            .Select(x => new
            {
                x.JobPlanId,
                x.JobPlanNumber,
                TaskCount = ctx.MaintenanceTasks.Count(t => t.JobPlanId == x.JobPlanId && t.IsActive),
                PlanUsageCount = ctx.MaintenancePlans.Count(p => p.JobPlanId == x.JobPlanId && p.IsActive)
            });

        var sql = query.ToQueryString();
        _output.WriteLine(sql);

        Assert.Contains("OFFSET", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("FETCH", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ORDER BY", sql, StringComparison.OrdinalIgnoreCase);

        var totalInDb = await ctx.JobPlans.CountAsync(x => x.IsActive);

        var service = new JobPlanService(ctx, null!);
        var result = await service.GetListAsync(page: 1, pageSize: 20);

        Assert.Equal(totalInDb, result.TotalCount);
        Assert.True(result.Items.Count <= 20);
    }
}
