using PEMS.Application.DTOs;

namespace PEMS.Application.Interfaces;

/// <summary>
/// طبق «VENDOR DOCUMENTATION MANAGEMENT PROCEDURE» بخش ۶-۷: تحویل مستندات نهایی (MDB).
/// </summary>
public interface IManufacturerDataBookService
{
    Task<List<ManufacturerDataBookDto>> GetAllAsync(int? projectId = null, int? vendorId = null);

    // طبق «PEMS — Global Grid Hardening» P1: GetAllAsync بالا کل جدول فعال MDB را با ۴ Include
    // بارگذاری می‌کرد. این متد همان فیلترها را حفظ می‌کند اما شمارش/مرتب‌سازی/صفحه‌بندی/
    // Projection را در SQL انجام می‌دهد.
    Task<PagedResult<ManufacturerDataBookDto>> GetPagedAsync(int page, int pageSize, int? projectId = null, int? vendorId = null);
    Task<ManufacturerDataBookDto?> GetByIdAsync(int id);
    Task<int> CreateAsync(int equipmentId, int vendorId, int? poId, int projectId, DateTime? plannedFinalIssueDate);
    Task AddDocumentAsync(int mdbId, string part, int? documentId, string title, int? dvdVolumeNo, bool isNotApplicable, string? notes);
    Task RemoveDocumentAsync(int mdbDocumentId);
    Task MarkIndexSubmittedAsync(int mdbId);
    Task MarkFinalIssuedAsync(int mdbId);
    Task MarkHardCopyShippedAsync(int mdbId, int? hardCopyCount);
    Task MarkSoftCopyDeliveredAsync(int mdbId);
}
