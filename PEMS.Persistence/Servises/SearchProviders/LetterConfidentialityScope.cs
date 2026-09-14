using Microsoft.EntityFrameworkCore;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services.SearchProviders;

/// <summary>
/// طبق «PEMS Phase 2A — Global Search Security»: قانون محرمانگی نامه‌نگاری (همون قرارداد
/// LetterController.Index/LetterService.SearchAsync، منبع حقیقت واقعی) توسط دو Provider جدا
/// نیاز است - LetterSearchProvider و AttachmentSearchProvider (وقتی پیوست به یک نامه وصل
/// است). به‌جای کپی همون منطق دوبار، این‌جا یک‌بار تعریف می‌شود.
/// </summary>
internal static class LetterConfidentialityScope
{
    public static async Task<bool> CanViewConfidentialAsync(
        IPermissionService permissionService, ICurrentUserContext currentUser) =>
        currentUser.Roles.Contains(PEMS.Persistence.Identity.SystemRoles.SystemAdmin)
        || await permissionService.HasPermissionAsync(currentUser.Roles, "Correspondence", "Approve");

    public static async Task<IQueryable<Letter>> ApplyAsync(
        IQueryable<Letter> query, bool canViewConfidential, PEMSDbContext context, ICurrentUserContext currentUser)
    {
        if (canViewConfidential) return query;

        var referredLetterIds = currentUser.EmployeeId.HasValue
            ? await context.LetterReferrals.Where(x => x.ReferredToEmployeeId == currentUser.EmployeeId.Value)
                .Select(x => x.LetterId).ToListAsync()
            : new List<int>();

        return query.Where(x =>
            x.ConfidentialityLevel == "Public" || x.ConfidentialityLevel == "Internal" ||
            (currentUser.UserId.HasValue && x.RegisteredById == currentUser.UserId.Value) ||
            referredLetterIds.Contains(x.LetterId));
    }
}
