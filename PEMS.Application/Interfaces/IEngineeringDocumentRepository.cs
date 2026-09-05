using PEMS.Application.DTOs;
using PEMS.Domain.Entities;

namespace PEMS.Application.Interfaces;

public interface IEngineeringDocumentRepository
{
    Task<List<EngineeringDocument>> GetAllAsync();

    // طبق Phase E-03 (رفع P1 ممیزی Performance): فیلتر/صفحه‌بندی/محدودیت دیسیپلین همه در SQL
    // اعمال می‌شوند - نه بارگذاری کل جدول در حافظه. restrictToDiscipline/disciplineId دقیقاً
    // همان دو سیگنال جدایی هستند که EngineeringDocumentsController.ScopeToOwnDisciplineIfRestrictedAsync
    // امروز به‌صورت درون‌حافظه‌ای اعمال می‌کند - نگه‌داشتن آن‌ها جدا از هم برای حفظ دقیق رفتار
    // فعلی لازم است (کارمندی که دیسیپلین ندارد، محدود به مدارک بدون‌دیسیپلین می‌ماند - نه
    // "نامحدود").
    Task<PagedResult<EngineeringDocument>> GetPagedAsync(EngineeringDocumentListFilter filter, bool restrictToDiscipline, int? disciplineId);

    Task<EngineeringDocumentSummaryCountsDto> GetSummaryCountsAsync(bool restrictToDiscipline, int? disciplineId);

    // طبق Phase E-05C (E-05B §9 Quality Model): RevisionCount/RejectionCount/FirstPassApproved
    // به‌ازای هر سند - سه Query جداگانه‌ی کوچک (طبق ADR-1، نه یک Join غول‌پیکر)، در لایه‌ی
    // سرویس با هم ترکیب می‌شوند.
    Task<List<EngineeringRevisionQualityDto>> GetRevisionQualityAsync(bool restrictToDiscipline, int? disciplineId, int? minRevisionCount, int? projectId = null);

    Task<EngineeringDocument?> GetByIdAsync(int id);

    // طبق ممیزی مقیاس‌پذیری (P1 - Document Register Remediation): جایگزین GetAllAsync برای
    // DocumentRegisterController - فیلتر/صفحه‌بندی/Projection همه در SQL، بدون Include سنگین
    // (Attachments/Revisions/Workflows که این صفحه اصلاً نمایش نمی‌دهد). PageSize همیشه به
    // DocumentRegisterFilter.MaxPageSize محدود می‌شود، حتی اگر ورودی چیز دیگری بخواهد.
    Task<PagedResult<EngineeringDocumentDto>> GetRegisterPagedAsync(
        DocumentRegisterFilter filter, bool restrictToDiscipline, int? disciplineId);

    // برای Export Excel: همه‌ی ردیف‌های منطبق با فیلتر (نه فقط یک صفحه) - همون Projection لاغر،
    // بدون Skip/Take، اما با سقف امن (MaxExportRows) به‌جای بارگذاری واقعاً نامحدود.
    Task<(List<EngineeringDocumentDto> Items, int TotalMatching)> GetRegisterAllMatchingAsync(
        DocumentRegisterFilter filter, bool restrictToDiscipline, int? disciplineId, int maxRows);

    // برای Export Selected: فقط شناسه‌های انتخاب‌شده - Scope دیسیپلین این‌جا هم اعمال می‌شود تا
    // کاربر محدود نتواند با دستکاری ids، مدرک خارج از دیسیپلین خودش را Export کند.
    Task<List<EngineeringDocumentDto>> GetRegisterByIdsAsync(
        List<int> ids, bool restrictToDiscipline, int? disciplineId);

    // سه Query کوچک و مستقل (SELECT DISTINCT) به‌جای Distinct روی کل نتیجه‌ی GetAllAsync در حافظه
    Task<DocumentRegisterFilterOptionsDto> GetRegisterFilterOptionsAsync(bool restrictToDiscipline, int? disciplineId);

    Task AddAsync(EngineeringDocument document);

    Task UpdateAsync(EngineeringDocument document);

    Task DeleteAsync(EngineeringDocument document);

    // طبق درخواست پورتال شخصی: مسیر امضای دیجیتال کارمند - برای نمایش کنار Checked/Approved By
    Task<string?> GetEmployeeSignaturePathAsync(int? employeeId);

    // طبق Phase E-01: مسیر اختصاصی Freeze/Unfreeze - جدا از UpdateAsync عمومی (که حالا خودش
    // مدرک منجمد را رد می‌کند)، چون عمل Freeze/Unfreeze خودش دقیقاً باید همین فیلد را تغییر دهد.
    Task FreezeAsync(int documentId, int? actorEmployeeId, string reason);

    Task UnfreezeAsync(int documentId, int? actorEmployeeId, string reason);
}