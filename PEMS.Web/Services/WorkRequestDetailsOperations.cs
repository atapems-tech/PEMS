using System.Globalization;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Context;

namespace PEMS.Web.Services;

/// <summary>SCRUM-817: the reads of the work request details page (formerly WorkRequestController.Details). Names of stages and actions come from the workflow configuration, in the language of the user.</summary>
public sealed class WorkRequestDetailsOperations(IWorkRequestService service, PEMSDbContext context, IPermissionService permissions, ICurrentUserService currentUser) : IWorkRequestDetailsOperations
{
    private static string Text(string? s) => s ?? string.Empty;
    private static bool Fa => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "fa";

    private async Task<WorkRequestActor> ActorAsync()
    {
        int? employeeId = currentUser.UserId.HasValue ? await context.Users.Where(u => u.Id == currentUser.UserId.Value).Select(u => u.EmployeeId).FirstOrDefaultAsync() : null;
        return new WorkRequestActor(employeeId, currentUser.Roles);
    }

    private async Task<WorkRequestDetailDto?> DetailsAsync(int id)
    {
        var wr = await service.GetDetailsAsync(id);
        if (wr is null) return null;
        // the workflow model decides the stages, the current stage, the actions and the history; only a legacy record (no instance) goes the old way
        wr.Workflow = await service.GetWorkflowViewAsync(id, await ActorAsync());
        wr.IsLegacyWithoutWorkflow = wr.Workflow == null && wr.Status != "Draft" && wr.Status is not ("Rejected" or "Cancelled" or "Converted");
        wr.CanConvert = !wr.LinkedWorkOrderId.HasValue && wr.Workflow?.AvailableActions.Any(a => a.ActionType == "Complete" && a.IsAllowed) == true;
        return wr;
    }

    private static string StageName(PEMS.WorkflowPlatform.Interfaces.WorkflowStageView s) => Fa && !string.IsNullOrWhiteSpace(s.NameFa) ? s.NameFa! : s.Name;

    public async Task<List<WorkRequestHeadRow>> HeadAsync(int id)
    {
        var wr = await DetailsAsync(id);
        if (wr is null) return [];
        var canCreate = await permissions.HasPermissionAsync(currentUser.Roles, "WorkRequest", "Create");
        var wf = wr.Workflow;
        var risk = wr.RiskScore.HasValue ? $"{wr.RiskLikelihood}×{wr.RiskSeverity} = {wr.RiskScore} ({wr.RiskLevel})" : "";
        var next = wr.IsLegacyWithoutWorkflow ? Text(service.GetLegacyNextStatus(wr.Status)) : "";
        return [new WorkRequestHeadRow(wr.WorkRequestId, wr.WorkRequestId, wr.RequestNumber, wr.Title, wr.Status, wr.Priority, wr.RequestedByName, wr.RequestedDate, Text(wr.EquipmentTagNumber), Text(wr.AssetNumber),
            Text(wr.FunctionalLocationBreadcrumb), Text(wr.EquipmentCriticalityClass), wr.ResponseDueDate, wr.ResolutionDueDate, Text(wr.FailureModeName), Text(wr.FailureEffect), wr.ReportedDowntimeHours, Text(wr.ReportedCondition),
            Text(wr.SuggestedDisciplineName), Text(wr.Description), wr.IsEmergency, wr.IsHseRelated, risk, Text(wr.PriorityRationale), wr.Status == "Rejected" ? Text(wr.RejectionReason) : "",
            wr.LinkedWorkOrderId.HasValue ? Text(wr.LinkedWorkOrderNumber) : "", wr.LinkedWorkOrderId, wf is null ? "" : $"{wf.WorkflowName} v{wf.VersionNumber}", wf?.Status ?? "",
            wf is null && wr.Status == "Draft" && canCreate, wr.CanConvert || wr.IsLegacyWithoutWorkflow && wr.Status == "Planned", !wr.EquipmentId.HasValue && wr.CanConvert, wr.IsLegacyWithoutWorkflow, next,
            wr.IsLegacyWithoutWorkflow && wr.Status is "UnderReview" or "Approved")];
    }

    public async Task<List<WorkRequestStageRow>> StagesAsync(int id)
    {
        var wf = (await DetailsAsync(id))?.Workflow;
        if (wf is null) return [];
        return wf.Stages.OrderBy(s => s.Order).Select(s => new WorkRequestStageRow(s.StageId, s.Order, StageName(s), s.Status,
            s.Status == "Current" ? string.Join(", ", s.Participants.Where(p => p.Status == "Pending").Select(p => Text(p.Role)).Distinct()) : "")).ToList();
    }

    public async Task<List<WorkRequestWorkflowActionRow>> WorkflowActionsAsync(int id)
    {
        var wf = (await DetailsAsync(id))?.Workflow;
        if (wf is null) return [];
        return wf.AvailableActions.Where(a => a.ActionType != "Complete").Select(a => new WorkRequestWorkflowActionRow(a.ActionType, id,
            Fa && !string.IsNullOrWhiteSpace(a.LabelFa) ? a.LabelFa! : !string.IsNullOrWhiteSpace(a.Label) ? a.Label! : a.ActionType, Text(a.ToStageName), a.IsAllowed, Text(a.DeniedReason))).ToList();
    }

    public async Task<List<WorkRequestWorkflowHistoryRow>> WorkflowHistoryAsync(int id)
    {
        var wf = (await DetailsAsync(id))?.Workflow;
        if (wf is null) return [];
        var userIds = wf.HistorySummary.Where(h => h.UserId.HasValue).Select(h => h.UserId!.Value).Distinct().ToList();
        var names = userIds.Count == 0 ? new Dictionary<int, string>() : await context.Employees.AsNoTracking().Where(e => userIds.Contains(e.EmployeeId)).ToDictionaryAsync(e => e.EmployeeId, e => e.FirstName + " " + e.LastName);
        string Who(int? uid) => uid.HasValue ? names.GetValueOrDefault(uid.Value, "#" + uid.Value) : "-";
        var i = 0;
        return wf.HistorySummary.OrderByDescending(h => h.ActionDate).Select(h => new WorkRequestWorkflowHistoryRow(++i, h.ActionDate, h.ActionType, Text(h.FromStageName), Text(h.ToStageName), Who(h.UserId), Text(h.Comment))).ToList();
    }

    public async Task<List<WorkRequestStatusHistoryRow>> StatusHistoryAsync(int id)
    {
        var wr = await service.GetDetailsAsync(id);
        if (wr is null) return [];
        var i = 0;
        return wr.StatusHistory.Select(h => new WorkRequestStatusHistoryRow(++i, h.FromStatus, h.ToStatus, Text(h.ChangedByName), h.ChangedDate, Text(h.Reason))).ToList();
    }
}
