using Microsoft.EntityFrameworkCore;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

/// <summary>
/// طبق ممیزی Package Procurement Contract Finalization (§C/§D/§I، Phase 3). سه قاعده‌ی حیاتی
/// این سرویس را هدایت می‌کنند: (۱) بی‌بازگشت‌بودن پیشنهاد اصلی - Bid.OriginalValue فقط یک‌بار در
/// SubmitBidAsync نوشته می‌شود و هیچ متد دیگری آن را تغییر نمی‌دهد؛ (۲) فنی ≠ تجاری - هیچ
/// Bid ای بدون هر دو ارزیابی مستقلِ «Passed» برنده نمی‌شود؛ (۳) صلاحیت فروشنده برای بسته‌های
/// بحرانی - اگر Tender.RequiresVendorQualification=true، دعوت و برد فقط برای فروشندگانی مجاز است
/// که در لحظه‌ی دعوت واجد صلاحیت بوده‌اند.
/// </summary>
public class TenderService : ITenderService
{
    private readonly PEMSDbContext _context;
    private readonly INumberingService _numbering;

    public TenderService(PEMSDbContext context, INumberingService numbering)
    {
        _context = context;
        _numbering = numbering;
    }

    public async Task<int> CreateAsync(int procurementPackageId, string title, DateTime? issueDate, DateTime? responseDeadline,
        bool requiresVendorQualification, string? technicalRequirements, string? commercialRequirements,
        string? inspectionRequirements, string? contractualRequirements)
    {
        var tenderNumber = await _numbering.GetNextNumberAsync("Tender");

        var tender = new Tender
        {
            TenderNumber = tenderNumber,
            ProcurementPackageId = procurementPackageId,
            Title = title,
            IssueDate = issueDate,
            ResponseDeadline = responseDeadline,
            RequiresVendorQualification = requiresVendorQualification,
            TechnicalRequirements = technicalRequirements,
            CommercialRequirements = commercialRequirements,
            InspectionRequirements = inspectionRequirements,
            ContractualRequirements = contractualRequirements,
            Status = "Draft"
        };

        _context.Tenders.Add(tender);
        await _context.SaveChangesAsync();
        return tender.TenderId;
    }

    public async Task<(bool Success, string? Error)> InviteVendorAsync(int tenderId, int vendorId)
    {
        var tender = await _context.Tenders.FirstOrDefaultAsync(x => x.TenderId == tenderId);
        if (tender == null) return (false, "مناقصه یافت نشد.");
        if (tender.Status != "Draft" && tender.Status != "Issued")
            return (false, $"دعوت فروشنده فقط پیش از باز شدن مناقصه مجاز است (وضعیت فعلی: {tender.Status}).");

        var vendor = await _context.Vendors.FirstOrDefaultAsync(x => x.VendorId == vendorId);
        if (vendor == null) return (false, "فروشنده یافت نشد.");

        var alreadyInvited = await _context.TenderInvitedVendors.AnyAsync(x => x.TenderId == tenderId && x.VendorId == vendorId);
        if (alreadyInvited) return (false, "این فروشنده قبلاً دعوت شده است.");

        // طبق §I قاعده‌ی ۱/۱۴: بسته‌های نیازمند صلاحیت نمی‌توانند فروشنده‌ی غیرواجد‌شرایط را دعوت کنند
        var isQualifiedNow = vendor.QualificationStatus == "Qualified"
            && (!vendor.QualificationExpiryDate.HasValue || vendor.QualificationExpiryDate.Value >= DateTime.Now);

        if (tender.RequiresVendorQualification && !isQualifiedNow)
            return (false, $"فروشنده «{vendor.Name}» واجد صلاحیت نیست (وضعیت: {vendor.QualificationStatus}) - این مناقصه نیازمند صلاحیت است.");

        _context.TenderInvitedVendors.Add(new TenderInvitedVendor
        {
            TenderId = tenderId,
            VendorId = vendorId,
            InvitedDate = DateTime.Now,
            QualificationConfirmed = isQualifiedNow
        });

        await _context.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> OpenForBiddingAsync(int tenderId)
    {
        var tender = await _context.Tenders.FirstOrDefaultAsync(x => x.TenderId == tenderId);
        if (tender == null) return (false, "مناقصه یافت نشد.");
        if (tender.Status != "Draft")
            return (false, $"باز کردن مناقصه فقط از وضعیت Draft مجاز است (وضعیت فعلی: {tender.Status}).");

        var hasInvitedVendor = await _context.TenderInvitedVendors.AnyAsync(x => x.TenderId == tenderId);
        if (!hasInvitedVendor) return (false, "پیش از باز کردن مناقصه، حداقل یک فروشنده باید دعوت شده باشد.");

        tender.Status = "Open";
        await _context.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> CloseBiddingAsync(int tenderId)
    {
        var tender = await _context.Tenders.FirstOrDefaultAsync(x => x.TenderId == tenderId);
        if (tender == null) return (false, "مناقصه یافت نشد.");
        if (tender.Status != "Open")
            return (false, $"بستن مناقصه فقط از وضعیت Open مجاز است (وضعیت فعلی: {tender.Status}).");

        var hasBid = await _context.Bids.AnyAsync(x => x.TenderId == tenderId);
        if (!hasBid) return (false, "پیش از بستن مناقصه، حداقل یک پیشنهاد باید ثبت شده باشد.");

        tender.Status = "Closed";
        await _context.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Success, string? Error, int? BidId)> SubmitBidAsync(
        int tenderId, int vendorId, decimal originalValue, string currency,
        string? technicalSubmissionReference, string? commercialSubmissionReference)
    {
        var tender = await _context.Tenders.FirstOrDefaultAsync(x => x.TenderId == tenderId);
        if (tender == null) return (false, "مناقصه یافت نشد.", null);
        if (tender.Status != "Open")
            return (false, $"ثبت پیشنهاد فقط در مناقصه‌ی باز مجاز است (وضعیت فعلی: {tender.Status}).", null);

        var invitation = await _context.TenderInvitedVendors
            .FirstOrDefaultAsync(x => x.TenderId == tenderId && x.VendorId == vendorId);
        if (invitation == null) return (false, "این فروشنده به این مناقصه دعوت نشده است.", null);

        // طبق §I قاعده‌ی ۱/۱۴: عکس‌فوری صلاحیت در لحظه‌ی دعوت - نه یک ارجاع زنده که بشود بعداً دور زد
        if (tender.RequiresVendorQualification && !invitation.QualificationConfirmed)
            return (false, "فروشنده در لحظه‌ی دعوت واجد صلاحیت تایید نشده بود - نمی‌تواند پیشنهاد ثبت کند.", null);

        var alreadyBid = await _context.Bids.AnyAsync(x => x.TenderId == tenderId && x.VendorId == vendorId);
        if (alreadyBid) return (false, "این فروشنده قبلاً برای این مناقصه پیشنهاد ثبت کرده است.", null);

        var bidNumber = await _numbering.GetNextNumberAsync("Bid");

        var bid = new Bid
        {
            BidNumber = bidNumber,
            TenderId = tenderId,
            VendorId = vendorId,
            SubmittedDate = DateTime.Now,
            OriginalValue = originalValue,
            Currency = currency,
            TechnicalSubmissionReference = technicalSubmissionReference,
            CommercialSubmissionReference = commercialSubmissionReference,
            Status = "Submitted"
        };

        _context.Bids.Add(bid);
        await _context.SaveChangesAsync();
        return (true, null, bid.BidId);
    }

    public async Task<(bool Success, string? Error)> RecordBidEvaluationAsync(
        int bidId, string evaluationType, int? evaluatorEmployeeId, string? disciplineRole, string decision, string? comment)
    {
        if (evaluationType != "Technical" && evaluationType != "Commercial")
            return (false, "نوع ارزیابی باید Technical یا Commercial باشد.");
        if (decision != "Pending" && decision != "Passed" && decision != "Failed")
            return (false, "تصمیم ارزیابی باید Pending/Passed/Failed باشد.");

        var bid = await _context.Bids.FirstOrDefaultAsync(x => x.BidId == bidId);
        if (bid == null) return (false, "پیشنهاد یافت نشد.");
        if (bid.Status != "Submitted" && bid.Status != "UnderTechnicalEvaluation" && bid.Status != "UnderCommercialEvaluation")
            return (false, $"ارزیابی روی این پیشنهاد در وضعیت فعلی ({bid.Status}) مجاز نیست.");

        _context.BidEvaluations.Add(new BidEvaluation
        {
            BidId = bidId,
            EvaluationType = evaluationType,
            EvaluatorId = evaluatorEmployeeId,
            DisciplineRole = disciplineRole,
            Decision = decision,
            Comment = comment,
            EvaluationDate = DateTime.Now
        });

        bid.Status = evaluationType == "Technical" ? "UnderTechnicalEvaluation" : "UnderCommercialEvaluation";

        await _context.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Success, string? Error, int? NegotiationId)> RequestNegotiationAsync(
        int bidId, decimal negotiatedValue, string? participants, string? commercialChanges, string? technicalChanges,
        string? contractualChanges, string? justification, string? supportingDocumentReference, int? actorEmployeeId)
    {
        var bid = await _context.Bids.Include(x => x.Negotiations).FirstOrDefaultAsync(x => x.BidId == bidId);
        if (bid == null) return (false, "پیشنهاد یافت نشد.", null);

        var previousValue = bid.NegotiatedValue ?? bid.OriginalValue;
        var nextRound = (bid.Negotiations.Count == 0 ? 0 : bid.Negotiations.Max(x => x.RoundNo)) + 1;

        var negotiation = new BidNegotiation
        {
            BidId = bidId,
            RoundNo = nextRound,
            NegotiationDate = DateTime.Now,
            Participants = participants,
            PreviousValue = previousValue,
            NegotiatedValue = negotiatedValue,
            CommercialChanges = commercialChanges,
            TechnicalChanges = technicalChanges,
            ContractualChanges = contractualChanges,
            Justification = justification,
            SupportingDocumentReference = supportingDocumentReference,
            Status = "Proposed"
        };

        _context.BidNegotiations.Add(negotiation);
        bid.Status = "UnderNegotiation";

        await _context.SaveChangesAsync();
        return (true, null, negotiation.BidNegotiationId);
    }

    public async Task<(bool Success, string? Error)> ApproveNegotiationAsync(int bidNegotiationId, int? actorEmployeeId)
    {
        var negotiation = await _context.BidNegotiations.Include(x => x.Bid).FirstOrDefaultAsync(x => x.BidNegotiationId == bidNegotiationId);
        if (negotiation == null) return (false, "این مذاکره یافت نشد.");
        if (negotiation.Status != "Proposed") return (false, "این مذاکره قبلاً تصمیم‌گیری شده است.");

        negotiation.Status = "Approved";
        negotiation.ApprovedById = actorEmployeeId;
        negotiation.ApprovedDate = DateTime.Now;

        // طبق §I قاعده‌ی ۴: فقط NegotiatedValue به‌روز می‌شود - OriginalValue هرگز بازنویسی نمی‌شود
        negotiation.Bid!.NegotiatedValue = negotiation.NegotiatedValue;

        await _context.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> SelectWinnerAsync(int tenderId, int bidId, int? actorEmployeeId)
    {
        var tender = await _context.Tenders.FirstOrDefaultAsync(x => x.TenderId == tenderId);
        if (tender == null) return (false, "مناقصه یافت نشد.");
        if (tender.Status != "Closed")
            return (false, $"انتخاب برنده فقط پس از بستن مناقصه مجاز است (وضعیت فعلی: {tender.Status}).");

        var bid = await _context.Bids.Include(x => x.Evaluations).FirstOrDefaultAsync(x => x.BidId == bidId && x.TenderId == tenderId);
        if (bid == null) return (false, "این پیشنهاد متعلق به این مناقصه نیست.");

        // طبق §D/§I قاعده‌ی ۲: تایید فنی و تجاری همیشه دو گیت مستقل‌اند - هیچ‌کدام جایگزین دیگری نیست
        var technicalPassed = bid.Evaluations.Any(x => x.EvaluationType == "Technical" && x.Decision == "Passed");
        var commercialPassed = bid.Evaluations.Any(x => x.EvaluationType == "Commercial" && x.Decision == "Passed");

        if (!technicalPassed) return (false, "این پیشنهاد هنوز ارزیابی فنی موفق ندارد - انتخاب به‌عنوان برنده مجاز نیست.");
        if (!commercialPassed) return (false, "این پیشنهاد هنوز ارزیابی تجاری موفق ندارد - انتخاب به‌عنوان برنده مجاز نیست.");

        if (tender.RequiresVendorQualification)
        {
            var invitation = await _context.TenderInvitedVendors.FirstOrDefaultAsync(x => x.TenderId == tenderId && x.VendorId == bid.VendorId);
            if (invitation == null || !invitation.QualificationConfirmed)
                return (false, "فروشنده‌ی این پیشنهاد واجد صلاحیت تاییدشده نیست - این مناقصه نیازمند صلاحیت است.");
        }

        var allBidsForTender = await _context.Bids.Where(x => x.TenderId == tenderId).ToListAsync();
        foreach (var b in allBidsForTender)
            b.Status = b.BidId == bidId ? "Won" : "Lost";

        bid.FinalValue = bid.NegotiatedValue ?? bid.OriginalValue;

        tender.Status = "Awarded";
        tender.AwardedBidId = bidId;
        tender.AwardedDate = DateTime.Now;
        tender.AwardedById = actorEmployeeId;

        await _context.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Success, string? Error, int? ContractId)> CreateContractFromWinningBidAsync(
        int tenderId, string contractNo, DateTime? startDate, DateTime? endDate)
    {
        var tender = await _context.Tenders
            .Include(x => x.ProcurementPackage)
            .Include(x => x.AwardedBid)
            .FirstOrDefaultAsync(x => x.TenderId == tenderId);

        if (tender == null) return (false, "مناقصه یافت نشد.", null);
        if (tender.Status != "Awarded" || tender.AwardedBid == null)
            return (false, "پیش از ساخت قرارداد، مناقصه باید به یک برنده رسیده باشد (Awarded).", null);
        if (tender.ContractId.HasValue)
            return (false, "برای این مناقصه قبلاً قرارداد ساخته شده است.", null);

        var contract = new Contract
        {
            ContractNo = contractNo,
            ProjectId = tender.ProcurementPackage!.ProjectId,
            VendorId = tender.AwardedBid.VendorId,
            ContractValue = tender.AwardedBid.FinalValue,
            StartDate = startDate,
            EndDate = endDate,
            Status = "Draft",
            // طبق گزارش سخت‌افزاری نهایی (Gap ۱): بسته‌ای که بازرسی الزامی داشت، قرارداد برخاسته
            // از آن هم باید همان الزام را به ارث ببرد - نه این‌که خاموش شروع شود
            RequiresInspection = tender.ProcurementPackage.RequiresInspection,
            CriticalityClass = tender.ProcurementPackage.CriticalityClass
        };

        _context.Contracts.Add(contract);
        await _context.SaveChangesAsync();

        tender.ContractId = contract.ContractId;
        await _context.SaveChangesAsync();

        return (true, null, contract.ContractId);
    }
}
