using PEMS.Application.Common;
using PEMS.Application.Interfaces;
using PEMS.Web.Security;

namespace PEMS.Web.Services;

/// <summary>SCRUM-685: every chosen file goes into the AI intake queue and is classified in the background (what DocumentUploadController did).</summary>
public sealed class DocumentUploadOperations(IIntakeService intake) : IDocumentUploadOperations
{
    public async Task<(bool Success, string? Error)> UploadAsync(int projectId, IReadOnlyList<OperationFile> files, int? employeeId, List<string> userRoles)
    {
        // no module permission: the same role gate as the Import Center card
        if (!ImportCatalog.CanUse("DocumentUpload", userRoles)) throw new UnauthorizedAccessException();
        if (files.Count == 0) return (false, "Please select at least one file.");

        var ids = new List<int>();
        foreach (var file in files)
        {
            await using var stream = new MemoryStream(file.Bytes);
            ids.Add(await intake.UploadToIntakeAsync(stream, file.FileName, file.Bytes.Length, file.ContentType, projectId, employeeId));
        }
        foreach (var id in ids) Hangfire.BackgroundJob.Enqueue<IIntakeService>(x => x.ProcessIntakeAsync(id));
        return (true, $"{ids.Count} file(s) uploaded to the AI Intake Queue and are being classified.");
    }
}
