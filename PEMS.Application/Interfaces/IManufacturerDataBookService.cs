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
    [PEMS.Application.Common.PemsOperation("Mdb", "Create")]   // SCRUM-718
    Task<int> CreateAsync(int equipmentId, int vendorId, int? poId, int projectId, DateTime? plannedFinalIssueDate);
    [PEMS.Application.Common.PemsOperation("Mdb", "AddDocument")]   // SCRUM-718
    Task AddDocumentAsync(int mdbId, string part, int? documentId, string? title, int? dvdVolumeNo, bool isNotApplicable, string? notes);
    [PEMS.Application.Common.PemsOperation("Mdb", "RemoveDocument")]   // SCRUM-718
    Task RemoveDocumentAsync(int mdbDocumentId);
    [PEMS.Application.Common.PemsOperation("Mdb", "MarkIndexSubmitted")]   // SCRUM-718
    Task MarkIndexSubmittedAsync(int mdbId);
    [PEMS.Application.Common.PemsOperation("Mdb", "MarkFinalIssued")]   // SCRUM-718
    Task MarkFinalIssuedAsync(int mdbId);
    [PEMS.Application.Common.PemsOperation("Mdb", "MarkHardCopyShipped")]   // SCRUM-718
    Task MarkHardCopyShippedAsync(int mdbId, int? hardCopyCount);
    [PEMS.Application.Common.PemsOperation("Mdb", "MarkSoftCopyDelivered")]   // SCRUM-718
    Task MarkSoftCopyDeliveredAsync(int mdbId);
}
