using Microsoft.EntityFrameworkCore;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

/// <summary>
/// طبق «VENDOR DOCUMENTATION MANAGEMENT PROCEDURE» بخش ۴.۳.۱: هر بخش از فرمت CONTRACTOR که
/// روی داده‌ی موجود هنوز خالی است (VendorCode، PlantCode/UnitCode، MaterialCode، اتصال واقعی
/// MR) باعث خطای قابل‌فهم می‌شود - نه حدس زدن مقدار اشتباه. مشابه TransmittalNumberingService.
/// </summary>
public class VdrDocumentNumberingService : IVdrDocumentNumberingService
{
    private readonly PEMSDbContext _context;
    private readonly INumberingService _numberingService;

    public VdrDocumentNumberingService(PEMSDbContext context, INumberingService numberingService)
    {
        _context = context;
        _numberingService = numberingService;
    }

    public async Task<string> GenerateContractorNumberAsync(int vdrItemId)
    {
        var item = await _context.VDRItems
            .Include(x => x.Vendor)
            .Include(x => x.Project)
            .Include(x => x.Discipline)
            .Include(x => x.MR)
            .FirstOrDefaultAsync(x => x.DocumentId == vdrItemId);

        if (item == null)
            throw new InvalidOperationException($"مدرک فروشنده (VDR Item) با شناسه {vdrItemId} یافت نشد.");

        if (item.MR == null)
            throw new InvalidOperationException(
                "این مدرک هنوز به یک MR واقعی وصل نشده (فقط شماره‌ی خام MR ثبت شده). " +
                "طبق روال پروژه، پیش از شماره‌گذاری رسمی باید این ارتباط از طریق Relationship Explorer تأیید شود.");

        var plantCode = item.Project?.PlantCode;
        if (string.IsNullOrWhiteSpace(plantCode))
            throw new InvalidOperationException($"پروژه '{item.Project?.ProjectName ?? "نامشخص"}' کد پلنت (PlantCode) ندارد.");

        var unitCode = item.Project?.UnitCode;
        if (string.IsNullOrWhiteSpace(unitCode))
            throw new InvalidOperationException($"پروژه '{item.Project?.ProjectName ?? "نامشخص"}' کد واحد (UnitCode) ندارد.");

        var vendorCode = item.Vendor?.VendorCode;
        if (string.IsNullOrWhiteSpace(vendorCode))
            throw new InvalidOperationException($"فروشنده '{item.Vendor?.Name ?? "نامشخص"}' کد فروشنده (VendorCode) ندارد.");

        Discipline? discipline = item.Discipline;
        if (discipline == null)
        {
            var allDisciplines = await _context.Disciplines.Where(x => x.IsActive).ToListAsync();
            discipline = DisciplineMatcher.Match(allDisciplines, item.DisciplineCodeRaw);
        }

        if (discipline == null || string.IsNullOrWhiteSpace(discipline.Code))
            throw new InvalidOperationException(
                $"دیسیپلین این مدرک مشخص نیست (کد خام: '{item.DisciplineCodeRaw}'). ابتدا دیسیپلین را تعیین کنید.");

        var officialTypes = await _context.DocumentTypeCodes.Where(x => x.IsActive).ToListAsync();
        var matchedType = DocumentTypeMatcher.Match(officialTypes, item.DocumentTypeCode);
        if (matchedType == null)
            throw new InvalidOperationException(
                $"نوع مدرک '{item.DocumentTypeCode}' با هیچ‌کدام از ۲۹ کد رسمی سند (Appendix 2) تطبیق نخورد.");

        if (string.IsNullOrWhiteSpace(item.MaterialCode))
            throw new InvalidOperationException("کد متریال (MaterialCode) برای این مدرک ثبت نشده.");

        var digits = new string(item.MR.MRNumber.Where(char.IsDigit).ToArray());
        var mrLast4 = digits.Length >= 4 ? digits[^4..] : digits.PadLeft(4, '0');

        var revision = "0";
        if (!string.IsNullOrWhiteSpace(item.RevisionNo))
            revision = int.TryParse(item.RevisionNo, out var revNum) ? revNum.ToString() : item.RevisionNo.Trim();

        var scopeKey = $"VDRItem:{vdrItemId}";
        var raw = await _numberingService.GetNextNumberAsync("VdrDocument", scopeKey);
        var sequencePart = raw.Substring(raw.LastIndexOf('-') + 1);

        return $"{plantCode}-{unitCode}-{vendorCode}-{discipline.Code}-{matchedType.Code}-{item.MaterialCode}-{mrLast4}-{sequencePart}-{revision}";
    }

    public async Task<string> GenerateVendorNumberAsync(int vdrItemId)
    {
        var item = await _context.VDRItems
            .Include(x => x.Project)
            .Include(x => x.Discipline)
            .Include(x => x.MR)
            .Include(x => x.Equipment)
            .FirstOrDefaultAsync(x => x.DocumentId == vdrItemId);

        if (item == null)
            throw new InvalidOperationException($"مدرک فروشنده (VDR Item) با شناسه {vdrItemId} یافت نشد.");

        if (item.MR == null)
            throw new InvalidOperationException(
                "این مدرک هنوز به یک MR واقعی وصل نشده (فقط شماره‌ی خام MR ثبت شده). " +
                "طبق روال پروژه، پیش از شماره‌گذاری رسمی باید این ارتباط از طریق Relationship Explorer تأیید شود.");

        var jobNumber = item.Project?.JobNumber;
        if (string.IsNullOrWhiteSpace(jobNumber))
            throw new InvalidOperationException($"پروژه '{item.Project?.ProjectName ?? "نامشخص"}' شماره‌ی کار (JobNumber) ندارد.");

        var unitCode = item.Project?.UnitCode;
        if (string.IsNullOrWhiteSpace(unitCode))
            throw new InvalidOperationException($"پروژه '{item.Project?.ProjectName ?? "نامشخص"}' کد واحد (UnitCode) ندارد.");

        Discipline? discipline = item.Discipline;
        if (discipline == null)
        {
            var allDisciplines = await _context.Disciplines.Where(x => x.IsActive).ToListAsync();
            discipline = DisciplineMatcher.Match(allDisciplines, item.DisciplineCodeRaw);
        }

        if (discipline == null || string.IsNullOrWhiteSpace(discipline.Code))
            throw new InvalidOperationException(
                $"دیسیپلین این مدرک مشخص نیست (کد خام: '{item.DisciplineCodeRaw}'). ابتدا دیسیپلین را تعیین کنید.");

        if (string.IsNullOrWhiteSpace(item.TypologyCode))
            throw new InvalidOperationException("کد تایپولوژی مدرک فروشنده (TypologyCode - Appendix 3) برای این مدرک ثبت نشده.");

        var officialTypologies = await _context.VendorDocumentTypologyCodes.Where(x => x.IsActive).ToListAsync();
        var matchedTypology = VendorDocumentTypologyMatcher.Match(officialTypologies, item.TypologyCode);
        if (matchedTypology == null)
            throw new InvalidOperationException(
                $"کد تایپولوژی '{item.TypologyCode}' با هیچ‌کدام از ۷۹ کد رسمی سند (Appendix 3) تطبیق نخورد.");

        var mrDigits = new string(item.MR.MRNumber.Where(char.IsDigit).ToArray());
        var mrLast4 = mrDigits.Length >= 4 ? mrDigits[^4..] : mrDigits.PadLeft(4, '0');

        // طبق بخش ۴.۳.۲: ۵-۶ کاراکتر آخر تگ تجهیز؛ برای مدارک فله بدون تجهیز مشخص "000000"
        var equipmentTag = item.Equipment?.TagNumber;
        var equipmentSegment = string.IsNullOrWhiteSpace(equipmentTag)
            ? "000000"
            : (equipmentTag.Length >= 6 ? equipmentTag[^6..] : equipmentTag.PadLeft(6, '0')).ToUpperInvariant();

        var scopeKey = $"VDRItem:{vdrItemId}";
        var raw = await _numberingService.GetNextNumberAsync("VdrVendorDocument", scopeKey);
        var sequencePart = raw.Substring(raw.LastIndexOf('-') + 1);

        return $"{jobNumber}-{discipline.Code}-VD-{matchedTypology.Code}-{unitCode}-{mrLast4}-{equipmentSegment}-{sequencePart}";
    }
}
