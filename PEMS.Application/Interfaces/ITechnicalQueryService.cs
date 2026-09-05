namespace PEMS.Application.Interfaces;

public interface ITechnicalQueryService
{
    Task<int> RaiseAsync(
        string queryType, string subject, string? description, int projectId, int disciplineId,
        int? relatedDocumentId, int? relatedEquipmentId, DateTime? requiredResponseDate, string priority,
        int? raisedByEmployeeId, IReadOnlyList<string> actorRoles,
        string? ipAddress = null, string? userAgent = null);

    Task RespondAsync(
        int technicalQueryId, string responseText,
        int actorEmployeeId, int actorDisciplineId,
        string? ipAddress = null, string? userAgent = null);

    Task ConfirmAsync(
        int technicalQueryId, int? actorEmployeeId, IReadOnlyList<string> actorRoles,
        string? ipAddress = null, string? userAgent = null);

    Task ReopenAsync(
        int technicalQueryId, string reason, int? actorEmployeeId, IReadOnlyList<string> actorRoles,
        string? ipAddress = null, string? userAgent = null);
}
