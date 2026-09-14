using Microsoft.EntityFrameworkCore;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services.SearchProviders;

/// <summary>
/// طبق «PEMS Phase 2A — Global Search Security»: قانون محدودیت دیسیپلین مدارک مهندسی
/// (EngineeringDocumentsController.Index/ScopeToOwnDisciplineIfRestrictedAsync، منبع حقیقت
/// واقعی) توسط سه Provider جدا نیاز است - EngineeringDocumentSearchProvider،
/// DocumentRevisionSearchProvider (از طریق مدرک والد)، و AttachmentSearchProvider (وقتی به
/// یک مدرک مهندسی وصل است). به‌جای کپی همون منطق سه‌بار، این‌جا یک‌بار تعریف می‌شود.
/// </summary>
internal static class EngineeringDocumentDisciplineScope
{
    public static async Task<(bool IsRestricted, int? DisciplineId)> ResolveAsync(
        PEMSDbContext context, IAccessControlService accessControlService, ICurrentUserContext currentUser)
    {
        var canViewAllDisciplines = (await accessControlService.AuthorizeForRolesAsync(
            currentUser.Roles, "EngineeringDocuments", "ViewAllDisciplines")).Allowed;

        // طبق رفتار دقیق فعلی کنترلر: بدون رکورد Employee مرتبط = نامحدود (نه محدودیت
        // سخت‌گیرانه‌تر جدید) - این‌جا فقط همون قانون موجود تکرار می‌شود
        if (canViewAllDisciplines || !currentUser.UserId.HasValue || !currentUser.EmployeeId.HasValue)
            return (false, null);

        var employee = await context.Employees.FirstOrDefaultAsync(e => e.EmployeeId == currentUser.EmployeeId.Value);
        if (employee == null)
            return (false, null);

        return (true, employee.DisciplineId);
    }

    public static IQueryable<EngineeringDocument> Apply(
        IQueryable<EngineeringDocument> query, bool isRestricted, int? disciplineId) =>
        isRestricted ? query.Where(x => x.DisciplineId == disciplineId || x.DisciplineId == null) : query;
}
