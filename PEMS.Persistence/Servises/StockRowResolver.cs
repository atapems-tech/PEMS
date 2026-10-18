using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

/// <summary>نتیجه‌ی جستجوی ردیف موجودی: صفر، دقیقاً یک، یا چندین ردیف (مبهم). هیچ‌گاه ردیف دلخواه برنمی‌گرداند.</summary>
public sealed record StockRowLookup(MaterialStock? Row, IReadOnlyList<int> CandidateIds)
{
    public bool IsAmbiguous => CandidateIds.Count > 1;
}

/// <summary>
/// Phase 2 / P4 - تنها نقطه‌ی مجاز برای پیدا کردن/ساختن ردیف MaterialStock.
///
/// کلید کسب‌وکار ردیف موجودی (اثبات‌شده از مدل و همه‌ی مسیرهای نوشتن): (MaterialItemId، WarehouseId، StorageLocationId) که
/// StorageLocationId = NULL یعنی «موجودی بدون محل» و یک هویت مستقل است. دلیل‌ها: ایندکس یکتای موجود روی همین سه ستون (فقط برای
/// محل غیر NULL - زیرا EF برای ستون Nullable خودکار Filter «IS NOT NULL» می‌گذارد)، GetOrCreateStockAsync، IRN، MRS Import و Production
/// همگی با همین کلید جستجو می‌کنند؛ MaterialStock ستون Batch/Status ندارد (Batch فقط روی دفتر کل است) و TenantId بی‌استفاده است.
///
/// قواعد: دقیقاً یک ردیف ⇒ همان؛ صفر ⇒ ساخت (مسیر امن موجود)؛ چند ردیف ⇒ خطای صریح و قابل‌اقدام (ردیفی به‌دلخواه انتخاب یا جمع نمی‌شود،
/// چون قاعده‌ی تجمیع/ادغام تأیید نشده است). ساخت هم‌زمان ردیف اول با ایندکس یکتای فیلترشده‌ی NULL-location (Migration P4) رد می‌شود و
/// این‌جا با «خواندن دوباره‌ی ردیف برنده» (حداکثر یک بار) جبران می‌شود؛ بدون ایندکس (Schema بدون Migration) ردیف تکراری ممکن است
/// ساخته شود ولی در دسترسی بعدی به‌صورت AmbiguousRows شناسایی و متوقف می‌شود.
/// </summary>
public static class StockRowResolver
{
    private const int MaxCandidates = 5;

    /// <summary>جستجوی دقیق با کلید کامل (محل NULL فقط با NULL برابر است).</summary>
    public static async Task<StockRowLookup> FindExactAsync(
        PEMSDbContext ctx, int materialItemId, int warehouseId, int? storageLocationId, bool activeOnly, CancellationToken ct = default)
    {
        var query = ctx.MaterialStocks.Where(x =>
            x.MaterialItemId == materialItemId && x.WarehouseId == warehouseId && x.StorageLocationId == storageLocationId);
        if (activeOnly) query = query.Where(x => x.IsActive);

        var rows = await query.OrderBy(x => x.MaterialStockId).Take(MaxCandidates).ToListAsync(ct);
        return new StockRowLookup(rows.Count == 1 ? rows[0] : null, rows.Select(r => r.MaterialStockId).ToList());
    }

    /// <summary>
    /// جستجو در سطح انبار (بدون توجه به محل) - برای مسیرهایی مثل رزرو/تحویل SR که در سطح انبار کار می‌کنند و محل ندارند؛ بیش از یک ردیف فعال
    /// (چه تکراری، چه چند محل) مبهم است و انتخاب دلخواه ممنوع.
    /// </summary>
    public static async Task<StockRowLookup> FindForWarehouseAsync(
        PEMSDbContext ctx, int materialItemId, int warehouseId, bool activeOnly, CancellationToken ct = default)
    {
        var query = ctx.MaterialStocks.Where(x => x.MaterialItemId == materialItemId && x.WarehouseId == warehouseId);
        if (activeOnly) query = query.Where(x => x.IsActive);

        var rows = await query.OrderBy(x => x.MaterialStockId).Take(MaxCandidates).ToListAsync(ct);
        return new StockRowLookup(rows.Count == 1 ? rows[0] : null, rows.Select(r => r.MaterialStockId).ToList());
    }

    /// <summary>
    /// یک ردیف یا ساخت ردیف صفر (با ذخیره‌ی فوری، همان رفتار قبلی). خطای تعارض یکتایی هم‌زمان ⇒ ردیف ناقص Detach و ردیف برنده دوباره خوانده
    /// می‌شود (حداکثر ۲ تلاش). هیچ تراکنش/مقدار/رزروی در این مرحله نوشته نمی‌شود، پس تلاش مجدد هیچ اثر تکراری ندارد.
    /// </summary>
    public static async Task<(MaterialStock? Stock, string? Error)> GetOrCreateAsync(
        PEMSDbContext ctx, int materialItemId, int warehouseId, int? storageLocationId, CancellationToken ct = default)
    {
        for (var attempt = 1; attempt <= 2; attempt++)
        {
            var lookup = await FindExactAsync(ctx, materialItemId, warehouseId, storageLocationId, activeOnly: false, ct);
            if (lookup.IsAmbiguous) return (null, AmbiguousMessage(materialItemId, warehouseId, storageLocationId, lookup.CandidateIds));
            if (lookup.Row != null) return (lookup.Row, null);

            var stock = new MaterialStock
            {
                MaterialItemId = materialItemId,
                WarehouseId = warehouseId,
                StorageLocationId = storageLocationId,
                OnHandQuantity = 0,
                ReservedQuantity = 0,
                IsActive = true,
                CreatedDate = DateTime.Now
            };
            ctx.MaterialStocks.Add(stock);

            try
            {
                await ctx.SaveChangesAsync(ct);
                return (stock, null);
            }
            catch (DbUpdateException ex) when (IsUniqueViolation(ex))
            {
                // عملیات هم‌زمان دیگری همین ردیف اول را ساخته است: ردیف ناقص را رها کن و ردیف برنده را دوباره بخوان
                ctx.Entry(stock).State = EntityState.Detached;
            }
        }

        return (null, $"ساخت ردیف موجودی متریال {materialItemId} در انبار {warehouseId} به‌دلیل تعارض هم‌زمان ممکن نشد؛ دوباره تلاش کنید. / Could not create the stock row because of a concurrent creation; retry.");
    }

    public static bool IsUniqueViolation(DbUpdateException ex)
    {
        for (Exception? e = ex; e != null; e = e.InnerException)
            if (e is SqlException sql && sql.Errors.Cast<SqlError>().Any(x => x.Number is 2601 or 2627)) return true;
        return false;
    }

    /// <summary>پیام قابل‌اقدام برای ردیف‌های مبهم (دو زبانه؛ شناسه‌ی ردیف‌ها برای بازبینی دستی).</summary>
    public static string AmbiguousMessage(int materialItemId, int warehouseId, int? storageLocationId, IReadOnlyList<int> ids)
    {
        var location = storageLocationId.HasValue ? $" / محل {storageLocationId}" : " / بدون محل";
        return $"موجودی متریال {materialItemId} در انبار {warehouseId}{location} چند ردیف دارد (MaterialStockId: {string.Join(", ", ids)}) و انتخاب یا جمع خودکار آن‌ها مجاز نیست؛ عملیات متوقف شد و هیچ موجودی/تراکنشی تغییر نکرد. " +
               $"ابتدا ردیف‌ها را بازبینی و با تأیید مسئول انبار یکی کنید. / Ambiguous stock rows for item {materialItemId}, warehouse {warehouseId}{location} (ids {string.Join(", ", ids)}); " +
               "the operation was stopped and nothing was changed. Reconcile the rows first.";
    }
}
