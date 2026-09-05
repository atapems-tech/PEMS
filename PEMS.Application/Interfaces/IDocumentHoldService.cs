using PEMS.Application.DTOs;

namespace PEMS.Application.Interfaces;

/// <summary>
/// طبق بخش M ممیزی (Hold/Clouded Lifecycle): چرخه‌ی Hold مستقل از Freeze، با تاریخچه‌ی
/// کامل append-only (نه فقط یک bool).
/// </summary>
public interface IDocumentHoldService
{
    Task PlaceHoldAsync(int documentId, string reason, int? responsibleDisciplineId, int? actorEmployeeId);
    Task ReleaseHoldAsync(int documentId, string releaseReason, int? actorEmployeeId);
    Task<List<DocumentHoldDto>> GetHistoryAsync(int documentId);
}
