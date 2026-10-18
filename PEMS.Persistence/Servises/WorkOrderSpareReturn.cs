using Microsoft.EntityFrameworkCore;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

/// <summary>انبار/محلی که یک مصرف قطعه‌ی Work Order از آن خارج شده است (از دفتر کل، نه از حدس).</summary>
public sealed record SpareIssueOrigin(int WarehouseId, int? StorageLocationId);

/// <summary>
/// Phase 2 / P4 (Audit B) - برگشت قطعه‌ی مصرف‌شده‌ی Work Order به همان انبار/محلی که از آن خارج شده بود.
///
/// منشأ (Provenance): MaintenanceSpareUsage ستون انبار/محل ندارد، ولی AddSpareUsage همواره یک InventoryTransaction از نوع Issue با
/// ReferenceType="WorkOrder"، WorkOrderId، MaterialItemId، WarehouseId و StorageLocationId ثبت می‌کند. اگر همه‌ی Issueهای این
/// (WorkOrder، متریال) از یک کلید (انبار، محل) باشند، مقصد برگشت بدون ابهام همان است. اگر هیچ Issue ای نباشد (داده‌ی قدیمی) یا از
/// چند کلید مختلف باشد (و ثبت مصرف به تراکنش خاص لینک نیست)، حدس زدن (مثلاً «بیشترین موجودی») ممنوع است و عملیات با پیام قابل‌اقدام
/// متوقف می‌شود. تغییر اسکیما (لینک مستقیم Usage→Transaction) نیازمند تصمیم طراحی جداست و این‌جا انجام نشده است.
///
/// اتمیک بودن: ReturnedQuantity/TotalCost همان Context روی Usage ست می‌شود و یک SaveChanges (داخل MaterialStockService.ReturnAsync) هم
/// Usage و هم ردیف موجودی و هم تراکنش Return را با هم ذخیره می‌کند؛ دو بار Return هم‌زمان یک Usage با RowVersion رد می‌شود و هیچ‌چیز ثبت نمی‌شود.
/// </summary>
public static class WorkOrderSpareReturn
{
    public static async Task<(SpareIssueOrigin? Origin, string? Error)> ResolveOriginAsync(
        PEMSDbContext ctx, int workOrderId, int materialItemId, CancellationToken ct = default)
    {
        var keys = await ctx.InventoryTransactions.AsNoTracking()
            .Where(t => t.WorkOrderId == workOrderId && t.MaterialItemId == materialItemId
                        && t.TransactionType == "Issue" && t.ReferenceType == "WorkOrder")
            .Select(t => new { t.WarehouseId, t.StorageLocationId })
            .Distinct()
            .OrderBy(k => k.WarehouseId).ThenBy(k => k.StorageLocationId)
            .Take(5)
            .ToListAsync(ct);

        if (keys.Count == 0)
            return (null, $"تراکنش خروج (Issue) این مصرف برای Work Order {workOrderId} و متریال {materialItemId} پیدا نشد؛ انبار مبدأ مشخص نیست و برگشت خودکار انجام نمی‌شود. " +
                          "ورود دستی برگشت را از صفحه‌ی موجودی انجام دهید. / No Issue transaction found for this usage, so the origin warehouse cannot be determined; return it manually from the stock page.");

        if (keys.Count > 1)
            return (null, $"مصرف‌های Work Order {workOrderId} برای متریال {materialItemId} از چند انبار/محل خارج شده‌اند ({string.Join("، ", keys.Select(k => $"انبار {k.WarehouseId}/" + (k.StorageLocationId.HasValue ? $"محل {k.StorageLocationId}" : "بدون محل")))}) " +
                          "و ثبت مصرف به یک تراکنش مشخص لینک نیست؛ مقصد برگشت حدس زده نمی‌شود. برگشت را دستی در انبار درست ثبت کنید. / " +
                          "The issues came from several warehouses/locations and the usage is not linked to a specific one; the return destination is not guessed.");

        return (new SpareIssueOrigin(keys[0].WarehouseId, keys[0].StorageLocationId), null);
    }

    /// <summary>NotFound = مصرف وجود ندارد یا متعلق به این Work Order نیست (جلوگیری از برگشت با شناسه‌ی WO دیگر).</summary>
    public static async Task<(bool Success, string? Error, bool NotFound)> ReturnAsync(
        PEMSDbContext ctx, int maintenanceSpareUsageId, int workOrderId, decimal returnQuantity,
        string? reason, int? employeeId, CancellationToken ct = default)
    {
        if (returnQuantity <= 0) return (false, "مقدار برگشتی باید بزرگ‌تر از صفر باشه.", false);

        var usage = await ctx.MaintenanceSpareUsages.FirstOrDefaultAsync(x => x.MaintenanceSpareUsageId == maintenanceSpareUsageId, ct);
        if (usage == null || usage.WorkOrderId != workOrderId) return (false, null, true);

        var alreadyReturned = usage.ReturnedQuantity ?? 0;
        var outstanding = usage.Quantity - alreadyReturned;
        if (returnQuantity > outstanding) return (false, $"حداکثر مقدار قابل‌برگشت {outstanding} است.", false);

        var (origin, originError) = await ResolveOriginAsync(ctx, usage.WorkOrderId, usage.MaterialItemId, ct);
        if (origin == null) return (false, originError, false);

        // ردیف مقصد قبل از دست‌زدن به Usage پیدا/ساخته می‌شود (ساخت ردیف SaveChanges جدا دارد و نباید تغییر نیمه‌کاره‌ی Usage را ذخیره کند)
        var (row, rowError) = await StockRowResolver.GetOrCreateAsync(ctx, usage.MaterialItemId, origin.WarehouseId, origin.StorageLocationId, ct);
        if (row == null) return (false, rowError, false);

        usage.ReturnedQuantity = alreadyReturned + returnQuantity;
        if (usage.UnitCost.HasValue)
            usage.TotalCost = usage.UnitCost.Value * (usage.Quantity - usage.ReturnedQuantity.Value);

        var (success, error) = await new MaterialStockService(ctx).ReturnAsync(
            usage.MaterialItemId, origin.WarehouseId, origin.StorageLocationId, returnQuantity,
            reason ?? $"برگشت از WorkOrder #{workOrderId}", employeeId);

        if (!success)
        {
            // هیچ‌چیز ذخیره نشد؛ تغییر درون‌حافظه‌ی Usage هم برگردانده می‌شود تا SaveChanges بعدیِ همین Context آن را ننویسد
            await ctx.Entry(usage).ReloadAsync(ct);
            return (false, error, false);
        }

        return (true, null, false);
    }
}
