using Microsoft.EntityFrameworkCore;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

/// <summary>
/// طبق بخش M ممیزی: هم‌الگو با EngineeringDocumentRepository.FreezeAsync/UnfreezeAsync (بررسی
/// سرور-محور واقعی داخل خودِ متد، نه فقط پنهان‌کردن دکمه در View) - با این تفاوت که Hold
/// تاریخچه‌ی چندچرخه‌ای کامل در DocumentHold نگه می‌دارد، نه فقط آخرین وضعیت.
/// </summary>
public class DocumentHoldService : IDocumentHoldService
{
    private readonly PEMSDbContext _context;

    public DocumentHoldService(PEMSDbContext context)
    {
        _context = context;
    }

    public async Task PlaceHoldAsync(int documentId, string reason, int? responsibleDisciplineId, int? actorEmployeeId)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new InvalidOperationException("دلیل Hold الزامی است.");

        var document = await _context.Documents.FirstOrDefaultAsync(x => x.DocumentId == documentId);
        if (document == null)
            throw new InvalidOperationException("مدرک یافت نشد.");

        // طبق بخش ۱۴-۱۵ (چه چیزی می‌تواند وارد Hold شود): مدرک منجمد یا صادرشده دیگر «در حال
        // پیشرفت» نیست - این‌ها حالت‌های نهایی/قفل‌شده‌اند، Hold برای سند در جریان کار معنا دارد
        if (document.IsFrozen)
            throw new InvalidOperationException($"مدرک {document.DocumentNo} منجمد (Frozen) است - Hold روی مدرک منجمد بی‌معناست.");

        if (document.Status == "Issued")
            throw new InvalidOperationException($"مدرک {document.DocumentNo} صادر شده (Issued) است - برای اصلاح، رویژن جدید بسازید نه Hold.");

        if (document.IsOnHold)
            throw new InvalidOperationException($"مدرک {document.DocumentNo} از قبل در وضعیت Hold است.");

        await _context.DocumentHolds.AddAsync(new DocumentHold
        {
            DocumentId = documentId,
            Reason = reason,
            HoldDate = DateTime.Now,
            HoldByEmployeeId = actorEmployeeId,
            ResponsibleDisciplineId = responsibleDisciplineId
        });

        document.IsOnHold = true;
        document.ModifiedDate = DateTime.Now;

        await SaveWithConcurrencyCheckAsync();
    }

    public async Task ReleaseHoldAsync(int documentId, string releaseReason, int? actorEmployeeId)
    {
        var document = await _context.Documents.FirstOrDefaultAsync(x => x.DocumentId == documentId);
        if (document == null)
            throw new InvalidOperationException("مدرک یافت نشد.");

        if (!document.IsOnHold)
            throw new InvalidOperationException($"مدرک {document.DocumentNo} در وضعیت Hold نیست.");

        var openHold = await _context.DocumentHolds
            .Where(x => x.DocumentId == documentId && x.ReleaseDate == null)
            .OrderByDescending(x => x.HoldDate)
            .FirstOrDefaultAsync();

        if (openHold == null)
            throw new InvalidOperationException("رکورد Hold باز برای این مدرک یافت نشد.");

        openHold.ReleaseDate = DateTime.Now;
        openHold.ReleaseByEmployeeId = actorEmployeeId;
        openHold.ReleaseReason = releaseReason;

        document.IsOnHold = false;
        document.ModifiedDate = DateTime.Now;

        await SaveWithConcurrencyCheckAsync();
    }

    public async Task<List<DocumentHoldDto>> GetHistoryAsync(int documentId)
    {
        return await _context.DocumentHolds
            .Include(x => x.HoldByEmployee)
            .Include(x => x.ReleaseByEmployee)
            .Include(x => x.ResponsibleDiscipline)
            .Where(x => x.DocumentId == documentId)
            .OrderByDescending(x => x.HoldDate)
            .Select(x => new DocumentHoldDto
            {
                DocumentHoldId = x.DocumentHoldId,
                Reason = x.Reason,
                HoldDate = x.HoldDate,
                HoldByName = x.HoldByEmployee != null ? x.HoldByEmployee.FirstName + " " + x.HoldByEmployee.LastName : null,
                ResponsibleDisciplineName = x.ResponsibleDiscipline != null ? x.ResponsibleDiscipline.Name : null,
                ReleaseDate = x.ReleaseDate,
                ReleaseByName = x.ReleaseByEmployee != null ? x.ReleaseByEmployee.FirstName + " " + x.ReleaseByEmployee.LastName : null,
                ReleaseReason = x.ReleaseReason
            })
            .ToListAsync();
    }

    private async Task SaveWithConcurrencyCheckAsync()
    {
        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new InvalidOperationException(
                "این مدرک هم‌زمان توسط کاربر دیگری تغییر کرده است. لطفاً صفحه را رفرش کنید و دوباره تلاش کنید.");
        }
    }
}
