using Microsoft.EntityFrameworkCore;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Context;

namespace PEMS.Web.Services;

/// <summary>SCRUM-690: the lifecycle commands of EngineeringDocumentsController (same status rules, same messages, same services).</summary>
public sealed class EngineeringDocumentOperations(PEMSDbContext context, IEngineeringDocumentService service, IDocumentHoldService holdService, IDocumentWorkflowService workflow,
    IDocumentCommentService comments, IEngineeringCopilotService copilot, IAccessControlService access) : IEngineeringDocumentOperations
{
    private const string NotFound = "Document not found.";

    private Task<PEMS.Domain.Entities.EngineeringDocument?> DocumentAsync(int id) => context.EngineeringDocuments.FirstOrDefaultAsync(x => x.DocumentId == id);

    // the old actions showed the text of any exception of the workflow service as the error
    private static async Task<(bool Success, string? Error)> RunAsync(Func<Task> run)
    {
        try { await run(); return (true, null); }
        catch (Exception ex) { return (false, ex.Message); }
    }

    public async Task<(bool Success, string? Error)> FreezeAsync(int id, string? reason, int? actorEmployeeId)
    {
        if (string.IsNullOrWhiteSpace(reason)) return (false, "دلیل انجماد (Freeze Reason) الزامی است.");
        try { await service.FreezeAsync(id, actorEmployeeId, reason); return (true, null); }
        catch (InvalidOperationException ex) { return (false, ex.Message); }
    }

    public async Task<(bool Success, string? Error)> UnfreezeAsync(int id, string? reason, int? actorEmployeeId)
    {
        if (string.IsNullOrWhiteSpace(reason)) return (false, "دلیل رفع انجماد (Unfreeze Reason) الزامی است.");
        try { await service.UnfreezeAsync(id, actorEmployeeId, reason); return (true, null); }
        catch (InvalidOperationException ex) { return (false, ex.Message); }
    }

    public async Task<(bool Success, string? Error)> HoldAsync(int id, string reason, int? responsibleDisciplineId, int? actorEmployeeId)
    {
        try { await holdService.PlaceHoldAsync(id, reason, responsibleDisciplineId, actorEmployeeId); return (true, null); }
        catch (InvalidOperationException ex) { return (false, ex.Message); }
    }

    public async Task<(bool Success, string? Error)> ReleaseHoldAsync(int id, string reason, int? actorEmployeeId)
    {
        try { await holdService.ReleaseHoldAsync(id, reason, actorEmployeeId); return (true, null); }
        catch (InvalidOperationException ex) { return (false, ex.Message); }
    }

    public async Task<(bool Success, string? Error)> SubmitAsync(int id, string? comment, List<int>? idcDisciplineIds, int? actorEmployeeId, List<string> actorRoles, string? ipAddress, string? userAgent)
    {
        var document = await DocumentAsync(id);
        if (document == null) return (false, NotFound);
        if (document.Status != "Draft" && document.Status != "Rejected") return (false, "Document cannot be submitted in current status");
        return await RunAsync(() => workflow.SubmitAsync(id, comment, actorEmployeeId, actorRoles, idcDisciplineIds, ipAddress, userAgent));
    }

    public async Task<(bool Success, string? Error)> CheckAsync(int id, string? comment, int? actorEmployeeId, List<string> actorRoles, string? ipAddress, string? userAgent)
    {
        var document = await DocumentAsync(id);
        if (document == null) return (false, NotFound);
        if (document.Status != "Submitted") return (false, "Only submitted documents can be checked");
        return await RunAsync(() => workflow.CheckAsync(id, comment, actorEmployeeId, actorRoles, ipAddress, userAgent));
    }

    public async Task<(bool Success, string? Error)> IdcRespondAsync(int id, string decision, string? comment, int? actorEmployeeId, int? actorDisciplineId, string? ipAddress, string? userAgent)
    {
        if (!actorDisciplineId.HasValue || !actorEmployeeId.HasValue) return (false, "کارمند مرتبط با این کاربر یا دیسیپلینش مشخص نیست.");
        return await RunAsync(() => workflow.IdcRespondAsync(id, decision, comment, actorEmployeeId.Value, actorDisciplineId.Value, ipAddress, userAgent));
    }

    public async Task<(bool Success, string? Error)> ApproveAsync(int id, string? comment, int? actorEmployeeId, List<string> actorRoles, string? ipAddress, string? userAgent)
    {
        var document = await DocumentAsync(id);
        if (document == null) return (false, NotFound);
        if (document.Status != "Checked") return (false, "Only checked documents can be approved");
        if (!await CanApproveDisciplineAsync(document.DisciplineId, actorEmployeeId, actorRoles)) return (false, "You can only approve documents in your own discipline");
        return await RunAsync(() => workflow.ApproveAsync(id, comment, actorEmployeeId, actorRoles, ipAddress, userAgent));
    }

    // the approval is limited to the document's own discipline unless the role may approve every discipline (the module permission is the floor of the operation)
    private async Task<bool> CanApproveDisciplineAsync(int? documentDisciplineId, int? employeeId, List<string> roles)
    {
        if ((await access.AuthorizeForRolesAsync(roles, "EngineeringDocuments", "ApproveAllDisciplines")).Allowed) return true;
        if (!documentDisciplineId.HasValue) return true;     // a document without a discipline has no restriction
        if (!employeeId.HasValue) return false;
        var employee = await context.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.EmployeeId == employeeId.Value);
        return employee != null && employee.DisciplineId == documentDisciplineId.Value;
    }

    public async Task<(bool Success, string? Error)> RejectAsync(int id, string? comment, int? actorEmployeeId, List<string> actorRoles, string? ipAddress, string? userAgent)
    {
        var document = await DocumentAsync(id);
        if (document == null) return (false, NotFound);
        if (document.Status != "Submitted" && document.Status != "Checked") return (false, "Only submitted or checked documents can be rejected");
        return await RunAsync(() => workflow.RejectAsync(id, comment, actorEmployeeId, actorRoles, ipAddress, userAgent));
    }

    public async Task<(bool Success, string? Error)> IssueAsync(int id, string? comment, string deliveryUnitType, int? deliveryDepartmentId, string? externalReceiverName, string? letterNumber, string? letterSubject, string? letterBody,
        string? issuePurpose, int? actorEmployeeId, List<string> actorRoles, string? ipAddress, string? userAgent)
    {
        var document = await DocumentAsync(id);
        if (document == null) return (false, NotFound);
        if (document.Status != "Approved") return (false, "Only approved documents can be issued");
        return await RunAsync(() => workflow.IssueAsync(id, comment, actorEmployeeId, actorRoles, deliveryUnitType, deliveryDepartmentId, externalReceiverName, letterNumber, letterSubject, letterBody, issuePurpose, ipAddress, userAgent));
    }

    public async Task<(bool Success, string? Error)> ReviseAndResubmitAsync(int id, string? comment, int? actorEmployeeId, List<string> actorRoles, string? ipAddress, string? userAgent)
    {
        var document = await DocumentAsync(id);
        if (document == null) return (false, NotFound);
        if (document.Status != "Submitted" && document.Status != "Checked") return (false, "Only submitted or checked documents can be returned for revision");
        return await RunAsync(() => workflow.ReviseAndResubmitAsync(id, comment, actorEmployeeId, actorRoles, ipAddress, userAgent));
    }

    public Task<(bool Success, string? Error)> RecordClientResponseAsync(int id, string clientStatus, int incomingLetterId, string? clientComment, int? actorEmployeeId, List<string> actorRoles, string? ipAddress, string? userAgent)
        => RunAsync(() => workflow.RecordClientResponseAsync(id, clientStatus, incomingLetterId, clientComment, actorEmployeeId, actorRoles, ipAddress, userAgent));

    public async Task<(bool Success, string? Error)> SetCommentActionAsync(int commentId, string actionStatus)
    {
        await comments.SetActionStatusAsync(commentId, actionStatus);
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> AnalyzeRelationshipsAsync(int id)
    {
        await copilot.AnalyzeDocumentRelationshipsAsync(id);
        return (true, null);
    }
}
