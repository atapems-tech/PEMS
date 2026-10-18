using PEMS.Application.DTOs;
using PEMS.Application.Replenishment;

namespace PEMS.Application.Interfaces;

/// <summary>
/// Phase 2 / P5 - توصیه‌های تأمین (اجرای دستی). مجوزها روی ماژول «ReplenishmentRecommendation» با اکشن‌های استاندارد ماتریس مجوز:
/// View = مشاهده، Create = اجرای محاسبه، Edit = بازبینی (رد/بازگشایی). هیچ مجوزی به هیچ نقشی خودکار داده نمی‌شود.
/// این سرویس هرگز MR/PO/رزرو/موجودی/دفتر کل نمی‌نویسد و تبدیل (P6) و زمان‌بندی (P8) ندارد.
/// </summary>
public interface IReplenishmentRecommendationService
{
    public const string Module = "ReplenishmentRecommendation";

    Task<ReplenishmentRunResult> RunAsync(ReplenishmentRunRequest request, ReplenishmentActor actor, CancellationToken ct = default);

    Task<ReplenishmentCommandResult> DismissAsync(int recommendationId, byte[]? rowVersion, string reason, ReplenishmentActor actor, CancellationToken ct = default);

    Task<ReplenishmentCommandResult> ReopenAsync(int recommendationId, byte[]? rowVersion, string? note, ReplenishmentActor actor, CancellationToken ct = default);

    Task<ReplenishmentQuery<PagedResult<ReplenishmentListItem>>> GetPagedAsync(ReplenishmentFilter filter, int page, int pageSize, ReplenishmentActor actor, CancellationToken ct = default);

    Task<ReplenishmentQuery<ReplenishmentDetails>> GetDetailsAsync(int recommendationId, ReplenishmentActor actor, CancellationToken ct = default);

    Task<ReplenishmentQuery<ReplenishmentRunSummary>> GetLastRunAsync(ReplenishmentActor actor, CancellationToken ct = default);
}
