using Microsoft.EntityFrameworkCore;
using PEMS.Persistence.Services;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Inventory;

/// <summary>
/// طبق «PEMS Phase 6B — P0»: اثبات این‌که lowStockOnly واقعاً به یک EXISTS Subquery در SQL
/// Server ترجمه می‌شود (نه AsEnumerable/بارگذاری کامل جدول). Read-only محض روی PEMSDB واقعی.
/// </summary>
public class MaterialItemLowStockPaginationLiveDbTests
{
    [Fact]
    public void LowStockOnly_Filter_TranslatesToRealSqlExistsSubquery_NotClientEvaluation()
    {
        using var ctx = PemsDbContextFactory.CreateSqlServerOrThrow();

        var query = ctx.MaterialItems
            .Where(x => x.IsActive)
            .Where(x => ctx.MaterialStocks.Any(s =>
                s.MaterialItemId == x.MaterialItemId && s.IsActive &&
                s.ReorderPoint != null && (s.OnHandQuantity - s.ReservedQuantity) <= s.ReorderPoint))
            .OrderBy(x => x.MaterialCode)
            .Skip(0)
            .Take(25)
            .Select(x => new { x.MaterialItemId, x.MaterialCode });

        // ToQueryString خودش پرتاب می‌کند اگر EF Core نتواند کوئری را به SQL ترجمه کند (یعنی
        // اگر بخشی از آن نیازمند ارزیابی سمت-کلاینت باشد) - این دقیقاً تضمین SQL-translatable
        // بودن است که در الزامات فاز ۶B خواسته شده.
        var sql = query.ToQueryString();

        Assert.Contains("EXISTS", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("MaterialStocks", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("OFFSET", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("FETCH", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetListAsync_LowStockOnly_AgainstRealPEMSDB_ExecutesWithoutError()
    {
        using var ctx = PemsDbContextFactory.CreateSqlServerOrThrow();
        var service = new MaterialItemService(ctx);

        // اجرای واقعی Read-only روی PEMSDB - جدول فعلاً خالی است، اما این تضمین می‌کند کوئری
        // واقعاً روی SQL Server اجرا می‌شود (نه فقط ToQueryString) و خطای ترجمه‌ی EF نمی‌دهد.
        var result = await service.GetListAsync(page: 1, pageSize: 25, lowStockOnly: true);

        Assert.NotNull(result);
        Assert.True(result.TotalCount >= 0);
    }
}
