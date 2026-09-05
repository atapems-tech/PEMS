using Microsoft.EntityFrameworkCore;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

public class DocumentNumberingService : IDocumentNumberingService
{
    private readonly PEMSDbContext _context;
    private readonly INumberingService _numberingService;

    public DocumentNumberingService(PEMSDbContext context, INumberingService numberingService)
    {
        _context = context;
        _numberingService = numberingService;
    }

    public async Task<string> GenerateNextNumberAsync(int projectId, int? disciplineId, string documentType)
    {
        var project = await _context.Projects.FirstOrDefaultAsync(x => x.ProjectId == projectId);
        var projectCode = project?.ProjectCode ?? "PRJ";

        var disciplineCode = "GEN";
        if (disciplineId.HasValue)
        {
            var discipline = await _context.Disciplines.FirstOrDefaultAsync(x => x.DisciplineId == disciplineId.Value);
            disciplineCode = discipline?.Code ?? "GEN";
        }

        // طبق «VENDOR DOCUMENTATION MANAGEMENT PROCEDURE» Appendix 2: قبلاً اینجا فقط ۳ حرف اول
        // رشتهٔ آزاد documentType برداشته می‌شد (یعنی یک غلط تایپی هم شماره‌ی «موفق» ولی نامعتبر
        // تولید می‌کرد). حالا ابتدا با جدول رسمی DocumentTypeCode تطبیق داده می‌شود؛ فقط اگر
        // تطبیقی پیدا نشد (نوع مدرک هنوز در جدول رسمی نیست)، به همان رفتار قبلی برمی‌گردیم تا
        // مدارک موجود نشکنند.
        var officialTypes = await _context.DocumentTypeCodes.Where(x => x.IsActive).ToListAsync();
        var matchedType = DocumentTypeMatcher.Match(officialTypes, documentType);

        var typeCode = matchedType?.Code
            ?? (string.IsNullOrWhiteSpace(documentType)
                ? "DOC"
                : new string(documentType.Where(char.IsLetter).Take(3).ToArray()).ToUpper());

        if (string.IsNullOrEmpty(typeCode)) typeCode = "DOC";

        // طبق رفع باگ P0 ممیزی: قبلاً توالی از COUNT(*) در حافظه محاسبه می‌شد (بدون قفل)، پس دو
        // درخواست هم‌زمان می‌توانستند شماره‌ی یکسان بگیرند. حالا از همان NumberingService اتمیک
        // (UPDATE...OUTPUT) استفاده می‌شود که پروژه از قبل برای WorkOrder/Permit/... دارد - فقط
        // بخش توالی (عدد Padded شده) از رشته‌ی برگشتی استخراج و در همان قالب نمایشی قبلی
        // (ProjectCode-DisciplineCode-TypeCode-Sequence) بازساخته می‌شود تا فرمت شماره‌ی مدارک
        // موجود دست‌نخورده بماند.
        var scopeKey = $"{projectId}:{disciplineId?.ToString() ?? "none"}:{documentType}";
        var raw = await _numberingService.GetNextNumberAsync("EngineeringDocument", scopeKey);
        var sequencePart = raw.Substring(raw.LastIndexOf('-') + 1);

        return $"{projectCode}-{disciplineCode}-{typeCode}-{sequencePart}";
    }
}
