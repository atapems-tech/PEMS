using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common;
using PEMS.Domain.Entities;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Engineering;

/// <summary>
/// طبق سند «PEMS — Global Data Grid Standard» فاز ۲ (P0 - TechnicalQuery/MOC): این دو ماژول
/// هرگز از یک Service/Repository جدا برای فهرست استفاده نمی‌کردند (TechnicalQueryController/
/// MOCController مستقیم روی PEMSDbContext Query می‌زدند) - این تست‌ها همون الگو را دنبال
/// می‌کنند و مستقیم روی Query سمت Repository (که حالا در Controller زندگی می‌کند) اثبات را
/// از طریق فراخوانی مستقیم DbContext انجام می‌دهند، دقیقاً همون منطقی که Controller اجرا می‌کند.
/// </summary>
public class TechnicalQueryAndMOCPagedListTests
{
    private static async Task<(Project project, Discipline discipline)> SeedProjectAndDisciplineAsync(
        PEMS.Persistence.Context.PEMSDbContext ctx, string tag)
    {
        var project = new Project { ProjectName = $"TQMOC Test {tag}", ProjectCode = $"TQM{tag}{Guid.NewGuid():N}".Substring(0, 8) };
        var discipline = new Discipline { Name = $"TQMOC Disc {tag}", Code = $"TQD{tag}{Guid.NewGuid():N}".Substring(0, 6) };
        ctx.Projects.Add(project);
        ctx.Disciplines.Add(discipline);
        await ctx.SaveChangesAsync();
        return (project, discipline);
    }

    // ===================== TechnicalQuery =====================

    [Fact]
    public async Task TechnicalQuery_ReturnsStableDisjointPages_TotalCountIndependentOfPageSize()
    {
        using var ctx = PemsDbContextFactory.CreateSqlServerOrThrow();
        var (project, discipline) = await SeedProjectAndDisciplineAsync(ctx, "A");

        for (var i = 0; i < 25; i++)
            ctx.TechnicalQueries.Add(new TechnicalQuery
            {
                QueryNumber = $"TQ-A-{i:00}-{Guid.NewGuid():N}".Substring(0, 15),
                QueryType = "TQ",
                ProjectId = project.ProjectId,
                DisciplineId = discipline.DisciplineId,
                Subject = $"Subject {i}",
                RaisedDate = DateTime.Now.AddMinutes(-i),
                IsActive = true
            });
        await ctx.SaveChangesAsync();

        var baseQuery = ctx.TechnicalQueries.AsNoTracking().Where(x => x.IsActive && x.ProjectId == project.ProjectId);

        var totalCount = await baseQuery.CountAsync();
        var page1 = await baseQuery.OrderByDescending(x => x.RaisedDate).ThenByDescending(x => x.TechnicalQueryId).Skip(0).Take(20).Select(x => x.TechnicalQueryId).ToListAsync();
        var page2 = await baseQuery.OrderByDescending(x => x.RaisedDate).ThenByDescending(x => x.TechnicalQueryId).Skip(20).Take(20).Select(x => x.TechnicalQueryId).ToListAsync();

        Assert.Equal(25, totalCount);
        Assert.Equal(20, page1.Count);
        Assert.Equal(5, page2.Count);
        Assert.Equal(25, page1.Concat(page2).Distinct().Count());
    }

    [Fact]
    public async Task TechnicalQuery_StatusFilter_ExecutesServerSide()
    {
        using var ctx = PemsDbContextFactory.CreateSqlServerOrThrow();
        var (project, discipline) = await SeedProjectAndDisciplineAsync(ctx, "B");

        ctx.TechnicalQueries.Add(new TechnicalQuery { QueryNumber = $"TQ-B1-{Guid.NewGuid():N}".Substring(0, 15), QueryType = "TQ", ProjectId = project.ProjectId, DisciplineId = discipline.DisciplineId, Subject = "S1", Status = "Open", IsActive = true });
        ctx.TechnicalQueries.Add(new TechnicalQuery { QueryNumber = $"TQ-B2-{Guid.NewGuid():N}".Substring(0, 15), QueryType = "TQ", ProjectId = project.ProjectId, DisciplineId = discipline.DisciplineId, Subject = "S2", Status = "Closed", IsActive = true });
        await ctx.SaveChangesAsync();

        var result = await ctx.TechnicalQueries.AsNoTracking()
            .Where(x => x.IsActive && x.ProjectId == project.ProjectId && x.Status == "Open")
            .ToListAsync();

        Assert.Single(result);
        Assert.Equal("Open", result[0].Status);
    }

    [Fact]
    public async Task TechnicalQuery_OverdueComputation_MatchesEntityLogic()
    {
        using var ctx = PemsDbContextFactory.CreateSqlServerOrThrow();
        var (project, discipline) = await SeedProjectAndDisciplineAsync(ctx, "C");

        var overdue = new TechnicalQuery
        {
            QueryNumber = $"TQ-C1-{Guid.NewGuid():N}".Substring(0, 15), QueryType = "TQ",
            ProjectId = project.ProjectId, DisciplineId = discipline.DisciplineId, Subject = "Overdue",
            Status = "Open", RequiredResponseDate = DateTime.Now.AddDays(-1), IsActive = true
        };
        var notOverdue = new TechnicalQuery
        {
            QueryNumber = $"TQ-C2-{Guid.NewGuid():N}".Substring(0, 15), QueryType = "TQ",
            ProjectId = project.ProjectId, DisciplineId = discipline.DisciplineId, Subject = "Future",
            Status = "Open", RequiredResponseDate = DateTime.Now.AddDays(1), IsActive = true
        };
        ctx.TechnicalQueries.AddRange(overdue, notOverdue);
        await ctx.SaveChangesAsync();

        // طبق سند فاز ۲ بند ۹: همون Expression دقیقاً که TechnicalQueryController.Index حالا
        // مستقیم در Select() استفاده می‌کند - باید با TechnicalQuery.IsOverdue (Property واقعی
        // موجودیت) کاملاً یکسان باشد
        var overdueCount = await ctx.TechnicalQueries.AsNoTracking()
            .Where(x => x.IsActive && x.ProjectId == project.ProjectId)
            .CountAsync(x => (x.Status == "Open" || x.Status == "Reopened")
                && x.RequiredResponseDate.HasValue
                && x.RequiredResponseDate.Value < DateTime.Now);

        Assert.Equal(1, overdueCount);
        Assert.True(overdue.IsOverdue);
        Assert.False(notOverdue.IsOverdue);
    }

    // ===================== MOC =====================

    [Fact]
    public async Task MOC_ReturnsStableDisjointPages_TotalCountIndependentOfPageSize()
    {
        using var ctx = PemsDbContextFactory.CreateSqlServerOrThrow();
        var (project, discipline) = await SeedProjectAndDisciplineAsync(ctx, "D");

        for (var i = 0; i < 25; i++)
            ctx.MOCs.Add(new MOC
            {
                MOCNumber = $"MOC-D-{i:00}-{Guid.NewGuid():N}".Substring(0, 15),
                ProjectId = project.ProjectId,
                DisciplineId = discipline.DisciplineId,
                Title = $"Change {i}",
                RequestedDate = DateTime.Now.AddMinutes(-i),
                IsActive = true
            });
        await ctx.SaveChangesAsync();

        var baseQuery = ctx.MOCs.AsNoTracking().Where(x => x.IsActive && x.ProjectId == project.ProjectId);

        var totalCount = await baseQuery.CountAsync();
        var page1 = await baseQuery.OrderByDescending(x => x.RequestedDate).ThenByDescending(x => x.MOCId).Skip(0).Take(20).Select(x => x.MOCId).ToListAsync();
        var page2 = await baseQuery.OrderByDescending(x => x.RequestedDate).ThenByDescending(x => x.MOCId).Skip(20).Take(20).Select(x => x.MOCId).ToListAsync();

        Assert.Equal(25, totalCount);
        Assert.Equal(20, page1.Count);
        Assert.Equal(5, page2.Count);
        Assert.Equal(25, page1.Concat(page2).Distinct().Count());
    }

    [Fact]
    public async Task MOC_RiskLevelFilter_ExecutesServerSide()
    {
        using var ctx = PemsDbContextFactory.CreateSqlServerOrThrow();
        var (project, discipline) = await SeedProjectAndDisciplineAsync(ctx, "E");

        ctx.MOCs.Add(new MOC { MOCNumber = $"MOC-E1-{Guid.NewGuid():N}".Substring(0, 15), ProjectId = project.ProjectId, DisciplineId = discipline.DisciplineId, Title = "High risk", RiskLevel = "High", IsActive = true });
        ctx.MOCs.Add(new MOC { MOCNumber = $"MOC-E2-{Guid.NewGuid():N}".Substring(0, 15), ProjectId = project.ProjectId, DisciplineId = discipline.DisciplineId, Title = "Low risk", RiskLevel = "Low", IsActive = true });
        await ctx.SaveChangesAsync();

        var result = await ctx.MOCs.AsNoTracking()
            .Where(x => x.IsActive && x.ProjectId == project.ProjectId && x.RiskLevel == "High")
            .ToListAsync();

        Assert.Single(result);
        Assert.Equal("High", result[0].RiskLevel);
    }

    [Fact]
    public async Task PageSize_InvalidValue_NormalizesToDefault()
    {
        Assert.Equal(DataGridPageSize.Default, DataGridPageSize.Normalize(999999));
        Assert.Equal(DataGridPageSize.Default, DataGridPageSize.Normalize(10));
        Assert.Equal(20, DataGridPageSize.Normalize(20));
    }
}
