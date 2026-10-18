namespace PEMS.Application.Interfaces;

/// <summary>
/// طبق ممیزی Package Procurement Contract Finalization (§C/§E، Phase 1.1 + Phase 3): فاز ۱ فقط
/// مدل داده (Vendor.QualificationStatus/... + VendorQualificationHistory) را ساخت - این سرویس
/// لایه‌ی کسب‌وکاری روی آن است: بدون این، هیچ راهی برای واقعاً Qualified کردن یک فروشنده نبود و
/// گیت TenderService.InviteVendorAsync هرگز راضی نمی‌شد.
/// </summary>
public interface IVendorQualificationService
{
    Task<(bool Success, string? Error)> SetQualificationAsync(
        int vendorId, string newStatus, string? category, DateTime? effectiveDate, DateTime? expiryDate,
        string? evidenceReference, string? reason, int? actorEmployeeId);
}
