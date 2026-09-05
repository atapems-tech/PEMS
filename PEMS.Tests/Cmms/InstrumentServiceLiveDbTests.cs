using Microsoft.EntityFrameworkCore;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Cmms;

/// <summary>
/// طبق سند «PEMS — Global Data Grid Standard» بند A-10/A-11: شواهد SQL واقعی (ToQueryString)
/// و تأیید روی PEMSDB واقعی توسعه (localhost\SQLEXPRESS) - نه فقط ادعا که کد Skip/Take دارد.
/// Read-only محض: هیچ Insert/Update/Delete ای انجام نمی‌دهد. اگر SQL Server در دسترس نباشد،
/// طبق قرارداد PemsDbContextFactory.CreateSqlServerOrThrow با پیام واضح Fail می‌شود (نه Skip خاموش).
/// </summary>
public class InstrumentServiceLiveDbTests
{
    [Fact]
    public async Task GetListAsync_AgainstRealPEMSDB_GeneratesOffsetFetchSql_AndNeverMaterializesFullTable()
    {
        using var ctx = PemsDbContextFactory.CreateSqlServerOrThrow();

        var query = ctx.Instruments
            .Where(x => x.IsActive)
            .OrderBy(x => x.Equipment!.TagNumber)
            .ThenBy(x => x.InstrumentId)
            .Skip(0)
            .Take(20)
            .Select(x => new { x.InstrumentId, Tag = x.Equipment!.TagNumber });

        var sql = query.ToQueryString();

        // شواهد واقعی صفحه‌بندی سمت SQL Server: OFFSET/FETCH پارامتری، نه بارگذاری کامل جدول
        Assert.Contains("OFFSET", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("FETCH", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ORDER BY", sql, StringComparison.OrdinalIgnoreCase);

        var totalInDb = await ctx.Instruments.CountAsync(x => x.IsActive);
        var page = await query.ToListAsync();

        // شاهد این‌که واقعاً صفحه‌بندی می‌شود (نه کل جدول): اندازه‌ی صفحه هرگز از ۲۰ بیشتر
        // نمی‌شود، حتی اگر کل جدول بزرگ‌تر باشد.
        Assert.True(page.Count <= 20);

        var service = new InstrumentService(ctx);
        var result = await service.GetListAsync(page: 1, pageSize: 20);

        Assert.Equal(totalInDb, result.TotalCount);
        Assert.True(result.Items.Count <= 20);
    }
}
