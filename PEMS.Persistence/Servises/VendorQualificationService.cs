using Microsoft.EntityFrameworkCore;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

public class VendorQualificationService : IVendorQualificationService
{
    private static readonly string[] ValidStatuses = { "NotQualified", "Pending", "Qualified", "Suspended", "Expired" };

    private readonly PEMSDbContext _context;

    public VendorQualificationService(PEMSDbContext context)
    {
        _context = context;
    }

    public async Task<(bool Success, string? Error)> SetQualificationAsync(
        int vendorId, string newStatus, string? category, DateTime? effectiveDate, DateTime? expiryDate,
        string? evidenceReference, string? reason, int? actorEmployeeId)
    {
        if (!ValidStatuses.Contains(newStatus))
            return (false, $"وضعیت صلاحیت نامعتبر است: {newStatus}");

        var vendor = await _context.Vendors.FirstOrDefaultAsync(x => x.VendorId == vendorId);
        if (vendor == null) return (false, "فروشنده یافت نشد.");

        // طبق §G: تغییر وضعیت append-only ثبت می‌شود - فیلدهای روی Vendor فقط Cache آخرین ردیف‌اند
        _context.VendorQualificationHistories.Add(new VendorQualificationHistory
        {
            VendorId = vendorId,
            PreviousStatus = vendor.QualificationStatus,
            NewStatus = newStatus,
            Category = category,
            EffectiveDate = effectiveDate,
            ExpiryDate = expiryDate,
            EvidenceReference = evidenceReference,
            ChangedById = actorEmployeeId,
            ChangeDate = DateTime.Now,
            Reason = reason
        });

        vendor.QualificationStatus = newStatus;
        vendor.QualificationCategory = category;
        vendor.QualificationEffectiveDate = effectiveDate;
        vendor.QualificationExpiryDate = expiryDate;
        vendor.QualificationEvidenceReference = evidenceReference;
        vendor.QualificationReviewedById = actorEmployeeId;
        vendor.QualificationNotes = reason;

        await _context.SaveChangesAsync();
        return (true, null);
    }
}
