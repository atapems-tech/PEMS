using PEMS.Application.Common;

namespace PEMS.Application.Interfaces;

/// <summary>SCRUM-685: the multi-file upload into the AI intake queue (same role gate as the Import Center card, same texts as DocumentUploadController). The roles gate is the one of the card, not a module permission.</summary>
public interface IDocumentUploadOperations
{
    [PemsOperation("DocumentUpload", "Upload")]
    Task<(bool Success, string? Error)> UploadAsync(int projectId, IReadOnlyList<OperationFile> files, int? employeeId, List<string> userRoles);
}
