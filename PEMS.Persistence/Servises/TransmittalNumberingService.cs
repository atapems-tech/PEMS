using Microsoft.EntityFrameworkCore;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

/// <summary>
/// طبق «VENDOR DOCUMENTATION MANAGEMENT PROCEDURE» بخش ۳: TV-X-UU-AAAA-NNNN. X و UU از
/// MR.Discipline.TransmittalCode و MR.Project.UnitCode می‌آیند - اگر هرکدام روی داده‌ی موجود
/// خالی باشد (چون این دو فیلد تازه اضافه شده‌اند و روی رکوردهای قدیمی مقداردهی نشده‌اند)،
/// شماره‌گذاری خودکار متوقف می‌شود و خطای قابل‌فهم برمی‌گردد - حدس زدن کد اشتباه، شماره‌ی
/// رسمی نامعتبر تولید می‌کند که در EPC واقعی قابل قبول نیست.
/// </summary>
public class TransmittalNumberingService : ITransmittalNumberingService
{
    private readonly PEMSDbContext _context;
    private readonly INumberingService _numberingService;

    public TransmittalNumberingService(PEMSDbContext context, INumberingService numberingService)
    {
        _context = context;
        _numberingService = numberingService;
    }

    public async Task<string> GenerateNextNumberAsync(int mrId)
    {
        var mr = await _context.MaterialRequisitions
            .Include(x => x.Project)
            .Include(x => x.Discipline)
            .FirstOrDefaultAsync(x => x.MRId == mrId);

        if (mr == null)
            throw new InvalidOperationException($"درخواست خرید متریال (MR) با شناسه {mrId} یافت نشد.");

        var deptCode = mr.Discipline?.TransmittalCode;
        if (string.IsNullOrWhiteSpace(deptCode))
            throw new InvalidOperationException(
                $"دیسیپلین '{mr.Discipline?.Name ?? "نامشخص"}' کد دپارتمان ترانسمیتال (TransmittalCode) ندارد - " +
                "شماره‌گذاری خودکار برای این MR ممکن نیست. لطفاً شماره را دستی وارد کنید یا ابتدا کد را برای این دیسیپلین تنظیم کنید.");

        var unitCode = mr.Project?.UnitCode;
        if (string.IsNullOrWhiteSpace(unitCode))
            throw new InvalidOperationException(
                $"پروژه '{mr.Project?.ProjectName ?? "نامشخص"}' کد واحد (UnitCode) ندارد - " +
                "شماره‌گذاری خودکار برای این MR ممکن نیست. لطفاً شماره را دستی وارد کنید یا ابتدا کد واحد را برای این پروژه تنظیم کنید.");

        var digits = new string(mr.MRNumber.Where(char.IsDigit).ToArray());
        var mrLast4 = digits.Length >= 4 ? digits[^4..] : digits.PadLeft(4, '0');

        // سریال به‌ازای هر MR جداگانه شمرده می‌شود - چون X و UU خودشان از همان MR مشتق می‌شوند،
        // کلید Scope فقط mrId کافی است (معادل ترکیب دپارتمان+واحد+MR طبق فرمت سند).
        var scopeKey = $"MR:{mrId}";
        var raw = await _numberingService.GetNextNumberAsync("Transmittal", scopeKey);
        var sequencePart = raw.Substring(raw.LastIndexOf('-') + 1);

        return $"TV-{deptCode}-{unitCode}-{mrLast4}-{sequencePart}";
    }
}
