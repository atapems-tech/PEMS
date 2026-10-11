using Microsoft.EntityFrameworkCore;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

/// <summary>
/// Phase 2 / P4 (Audit A) - قفل کاربردی (sp_getapplock) به‌ازای یک سند برای عملیات «ثبت در موجودی» که ایدمپوتنسی‌اش به یک فیلد لینک
/// (مثل InventoryTransactionId) وابسته است و خود سند RowVersion ندارد (ProductionInput/ProductionOutput).
///
/// چرا: MaterialStockService.PostWithRetryAsync تعارض RowVersion ردیف موجودی را با خواندن دوباره حل می‌کند؛ این برای دو دریافت
/// «مستقل» درست است، ولی برای دو کلیک هم‌زمان روی «همان سند» تعارض ردیف موجودی ثبت دوباره را متوقف نمی‌کند. با این قفل، دومی تا
/// پایان اولی منتظر می‌ماند، سند را دوباره (از دیتابیس) می‌خواند، لینک را می‌بیند و ثبت دوباره نمی‌کند.
///
/// نکته‌ی استقرار: برنامه با EnableRetryOnFailure اجرا می‌شود و آن استراتژی تراکنش کاربر را فقط داخل strategy.ExecuteAsync می‌پذیرد؛
/// پس کل عملیات (تراکنش + قفل + بدنه) داخل Execution Strategy اجرا می‌شود. بدنه ممکن است روی خطای گذرا دوباره اجرا شود (بعد از Rollback کامل)
/// و باید دوباره‌اجراپذیر باشد: سند را از دیتابیس می‌خواند و ثبت قبلی را از دفتر کل تشخیص می‌دهد. روی InMemory/سایر Providerها یا وقتی
/// تراکنش بیرونی هست، فقط بدنه اجرا می‌شود.
/// </summary>
public static class PostingScope
{
    /// <summary>بدنه: (Success، Error، Commit). Commit=false یعنی تغییرات تراکنش Rollback شود (مثلاً رد اعتبارسنجی بعد از قفل).</summary>
    public static async Task<(bool Success, string? Error)> RunAsync(
        PEMSDbContext ctx, string resource, Func<Task<(bool Success, string? Error, bool Commit)>> body, int lockTimeoutMs = 15000)
    {
        if (!ctx.Database.IsSqlServer() || ctx.Database.CurrentTransaction != null)
        {
            var direct = await body();
            return (direct.Success, direct.Error);
        }

        var attempt = 0;
        var strategy = ctx.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            // تلاش مجدد بعد از خطای گذرا: وضعیت ردیابی‌شده‌ی تلاش قبلی (که Rollback شده) نباید نشت کند
            if (++attempt > 1) ctx.ChangeTracker.Clear();

            await using var tx = await ctx.Database.BeginTransactionAsync();
            var granted = await ctx.Database.SqlQueryRaw<int>(
                "DECLARE @r int; EXEC @r = sp_getapplock @Resource = {0}, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = {1}; SELECT @r AS Value;",
                resource, lockTimeoutMs).ToListAsync();
            if (granted.Count != 1 || granted[0] < 0)
                return (false, "این سند هم‌اکنون توسط عملیات دیگری در حال ثبت در موجودی است؛ چند لحظه بعد دوباره تلاش کنید. / The document is being posted by another operation; retry shortly.");

            var result = await body();
            if (result.Commit) await tx.CommitAsync();
            return (result.Success, result.Error);
        });
    }
}
