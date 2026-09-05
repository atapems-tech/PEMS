using PEMS.Application.DTOs;

namespace PEMS.Application.Interfaces;

/// <summary>
/// طبق سند Engineering Intelligence Redesign (بخش ۱): جایگزین سیستم جدای MR/MRQ.
/// جریان: Procurement Package → MR → Vendor Inquiry → Technical Clarification → MRQ →
/// Response → Approval → Closeout - همه از یک صفحه قابل مدیریت.
/// </summary>
public interface IProcurementPackageService
{
    Task<List<ProcurementPackageDto>> GetAllAsync();

    Task<ProcurementPackageDetailDto?> GetDetailAsync(int packageId);

    Task<int> CreateAsync(string packageNumber, int projectId, int? disciplineId, string title, string? description);

    Task AdvanceStageAsync(int packageId, string newStage);

    Task CloseoutAsync(int packageId);

    // پیوند یک MR موجود، یا ساخت یک MR جدید زیر همین Package
    Task LinkExistingMRAsync(int packageId, int mrId);

    Task<int> AddCommunicationAsync(int packageId, string type, string subject, string body,
        int? vendorId, string? raisedByName, int? attachmentId);

    Task RespondToCommunicationAsync(int communicationId, string responseText, string? respondedByName);

    Task SetCommunicationStatusAsync(int communicationId, string actionStatus);

    Task AddApprovalAsync(int packageId, string approverName, string decision, string? comment);
}
