using Microsoft.EntityFrameworkCore;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Cmms;

/// <summary>
/// طبق سند «PEMS — Global Data Grid Standard» بند A-10/A-11: شواهد SQL واقعی (ToQueryString)
/// روی PEMSDB واقعی توسعه. Read-only محض.
/// </summary>
public class MaintenanceStrategyServiceLiveDbTests
{
    [Fact]
    public async Task GetListAsync_AgainstRealPEMSDB_GeneratesOffsetFetchSql()
    {
        using var ctx = PemsDbContextFactory.CreateSqlServerOrThrow();

        var query = ctx.MaintenanceStrategies
            .Where(x => x.IsActive)
            .OrderBy(x => x.Equipment!.TagNumber)
            .ThenBy(x => x.MaintenanceStrategyId)
            .Skip(0)
            .Take(20)
            .Select(x => new { x.MaintenanceStrategyId, x.StrategyType });

        var sql = query.ToQueryString();

        Assert.Contains("OFFSET", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("FETCH", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ORDER BY", sql, StringComparison.OrdinalIgnoreCase);

        var totalInDb = await ctx.MaintenanceStrategies.CountAsync(x => x.IsActive);

        var service = new MaintenanceStrategyService(ctx);
        var result = await service.GetListAsync(page: 1, pageSize: 20);

        Assert.Equal(totalInDb, result.TotalCount);
        Assert.True(result.Items.Count <= 20);
    }
}
