using Microsoft.EntityFrameworkCore;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Cmms;

/// <summary>
/// طبق سند «PEMS — Global Data Grid Standard» بند A-10/A-11 (فاز ۵C): شواهد SQL واقعی
/// (ToQueryString) روی PEMSDB واقعی توسعه. Read-only محض - جدول Equipments در این محیط ۰ ردیف
/// دارد (تأیید‌شده مستقیم با sqlcmd)، پس این تست فقط شکل SQL را اثبات می‌کند، نه مقیاس واقعی.
/// </summary>
public class EquipmentServiceGridLiveDbTests
{
    [Fact]
    public async Task GetPagedAsync_AgainstRealPEMSDB_GeneratesOffsetFetchSql()
    {
        using var ctx = PemsDbContextFactory.CreateSqlServerOrThrow();

        var query = ctx.Equipments
            .Where(x => x.IsActive)
            .OrderBy(x => x.TagNumber)
            .ThenBy(x => x.EquipmentId)
            .Skip(0)
            .Take(20)
            .Select(x => new { x.EquipmentId, x.TagNumber });

        var sql = query.ToQueryString();

        Assert.Contains("OFFSET", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("FETCH", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ORDER BY", sql, StringComparison.OrdinalIgnoreCase);

        var totalInDb = await ctx.Equipments.CountAsync(x => x.IsActive);

        var service = new EquipmentService(ctx, new RelationshipService(ctx), new FunctionalLocationService(ctx));
        var result = await service.GetPagedAsync(page: 1, pageSize: 20);

        Assert.Equal(totalInDb, result.TotalCount);
        Assert.True(result.Items.Count <= 20);
    }
}
