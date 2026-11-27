using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using PEMS.Application.Common;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Context;
using PEMS.Web.Resources;
using PEMS.WorkflowPlatform.Interfaces;

namespace PEMS.Web.Services;

/// <summary>SCRUM-831: the read side of the work order details page in sections (see <see cref="IWorkOrderDetailsOperations"/>); the same rules and the same services as WorkOrderController.Details.</summary>
public sealed class WorkOrderDetailsOperations(PEMSDbContext context, ICurrentUserService currentUser, IPermissionService permissions, IWorkOrderLifecycleService lifecycle, IWorkOrderReviewService review,
    IWorkOrderLaborService labor, IWorkOrderSectionGuard sectionGuard, IWarehouseIntakeService warehouseIntake, IStringLocalizer<SharedResource> localizer) : IWorkOrderDetailsOperations
{
    private static readonly string[] StatusFlow = { "Open", "Assigned", "InProgress", "Completed", "Closed" };

    private static bool Fa => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "fa";

    private string T(string? key) { if (string.IsNullOrEmpty(key)) return ""; var s = localizer[key]; return s.ResourceNotFound ? key : s.Value; }

    private async Task<int?> EmployeeIdAsync()
        => currentUser.UserId.HasValue ? await context.Users.Where(u => u.Id == currentUser.UserId.Value).Select(u => u.EmployeeId).FirstOrDefaultAsync() : null;

    private async Task<WorkOrderActor> ActorAsync() => new(await EmployeeIdAsync(), currentUser.Roles);

    private async Task<PEMS.WorkflowPlatform.Interfaces.WorkflowRuntimeViewModel?> WorkflowAsync(int id) => await lifecycle.GetWorkflowViewAsync(id, await ActorAsync());

    private static string? NextStatus(string current)
    {
        var index = Array.IndexOf(StatusFlow, current);
        return index < 0 || index >= StatusFlow.Length - 1 ? null : StatusFlow[index + 1];
    }

    private static string StageName(WorkflowStageView st) => Fa && !string.IsNullOrWhiteSpace(st.NameFa) ? st.NameFa! : st.Name;

    private static string StageByName(WorkflowRuntimeViewModel? wf, string? name)
        => string.IsNullOrEmpty(name) ? "-" : (Fa ? (wf?.Stages.FirstOrDefault(x => x.Name == name)?.NameFa ?? name) : name);

    public async Task<List<WorkOrderHeadRow>> HeadAsync(int id)
    {
        var wo = await context.WorkOrders.AsNoTracking().Include(x => x.Equipment).Include(x => x.MaintenancePlan).Include(x => x.AssignedTeam).Include(x => x.AssignedEmployee).FirstOrDefaultAsync(x => x.WorkOrderId == id);
        if (wo == null) return [];
        var wf = await WorkflowAsync(id);
        var legacy = wf == null;
        var roles = currentUser.Roles;
        bool canEdit = await permissions.HasPermissionAsync(roles, "WorkOrder", "Edit"), canApprove = await permissions.HasPermissionAsync(roles, "WorkOrder", "Approve");
        var employeeId = await EmployeeIdAsync();
        var page = await sectionGuard.GetPageAsync(id, new WoCaller(currentUser.UserId, currentUser.UserName, employeeId, roles));
        bool Ctl(WoSection s) => page != null && page.Controls(s);
        var hasPendingReview = await review.HasPendingDisciplineReviewAsync(id);
        var next = legacy ? NextStatus(wo.Status) : null;
        var disciplines = await context.Disciplines.AsNoTracking().Where(d => d.DisciplineId == wo.OriginatingDisciplineId || d.DisciplineId == wo.ResponsibleDisciplineId).ToDictionaryAsync(d => d.DisciplineId, d => d.Name);
        string? origin = legacy ? null : wo.OriginatingOrganization + " / " + (wo.OriginatingDisciplineId.HasValue && disciplines.TryGetValue(wo.OriginatingDisciplineId.Value, out var o) ? o : "-");
        string? responsible = legacy ? null : (wo.ResponsibleDisciplineId.HasValue && disciplines.TryGetValue(wo.ResponsibleDisciplineId.Value, out var r) ? wo.ResponsibleOrganization + " / " + r : "-");
        var assigned = string.Join(" ", new[] { wo.AssignedTeam?.Name, wo.AssignedEmployee != null ? "(" + wo.AssignedEmployee.FirstName + " " + wo.AssignedEmployee.LastName + ")" : null }.Where(x => !string.IsNullOrEmpty(x)));
        if (assigned.Length == 0) assigned = wo.AssignedTo ?? "";
        var failure = string.IsNullOrWhiteSpace(wo.FailureModeCode) && string.IsNullOrWhiteSpace(wo.RootCause) ? null
            : $"{wo.FailureModeCode} / {wo.FailureCauseCode}" + (wo.DowntimeHours.HasValue ? $" - Downtime: {wo.DowntimeHours}" : "") + (string.IsNullOrWhiteSpace(wo.RootCause) ? "" : " - Root Cause: " + wo.RootCause);
        var spareCost = (await context.MaintenanceSpareUsages.AsNoTracking().Where(x => x.WorkOrderId == id && x.IsActive).ToListAsync()).Sum(x => x.TotalCost ?? 0);
        var planned = wo.PlannedStart.HasValue && wo.PlannedFinish.HasValue ? wo.PlannedStart.Value.ToString("yyyy-MM-dd HH:mm") + " -> " + wo.PlannedFinish.Value.ToString("yyyy-MM-dd HH:mm") : null;
        return [new WorkOrderHeadRow(wo.WorkOrderId, wo.WorkOrderNumber, T(wo.Status), wo.Status, T(wo.Priority), T(wo.WorkOrderType), wo.EquipmentId, wo.Equipment?.TagNumber, wo.Equipment?.Name, assigned, planned,
            wo.StartDate, wo.CompletionDate, wo.MaintenancePlan == null ? null : wo.MaintenancePlan.PlanNumber + " - " + wo.MaintenancePlan.Title, wo.Description, origin, responsible, wo.SourceWorkRequestId, failure,
            await labor.GetTotalLaborCostAsync(id), spareCost, legacy, hasPendingReview, next, wf == null ? null : string.Join(" | ", wf.Warnings),
            legacy && next != null && canEdit, legacy && next == "Completed" && canEdit, legacy && canEdit && wo.Status is "Assigned" or "InProgress", legacy && canEdit && wo.Status == "OnHold", legacy && canApprove && wo.Status == "Open",
            legacy && canApprove && wo.Status is "Open" or "Assigned" or "InProgress" or "OnHold", !legacy && (wf?.AvailableActions.Count ?? 0) > 0, wo.Status == "InProgress", wo.WorkOrderType is "Corrective" or "Emergency",
            page?.CanReopen ?? false, Ctl(WoSection.ResponsibleDiscipline), Ctl(WoSection.ExecutorAssignment), Ctl(WoSection.Schedule), Ctl(WoSection.RequiredSkills), Ctl(WoSection.RequiredMaterials), Ctl(WoSection.Crew),
            Ctl(WoSection.Labor), Ctl(WoSection.SpareParts), Ctl(WoSection.DisciplineReview), Ctl(WoSection.JobPlanTasks),
            legacy && next != null && canEdit ? (next == "Completed" ? (wo.WorkOrderType is "Corrective" or "Emergency" ? "work-order-advance-complete-failure" : "work-order-advance-complete") : next == "Assigned" ? "work-order-advance-assign" : "work-order-advance") : null,
            wo.ResponsibleDisciplineId, wo.PlannedStart?.ToString("yyyy-MM-ddTHH:mm"), wo.PlannedFinish?.ToString("yyyy-MM-ddTHH:mm"))];
    }

    public async Task<List<WorkOrderStageRow>> StagesAsync(int id)
    {
        var wf = await WorkflowAsync(id);
        if (wf == null) return [];
        return wf.Stages.OrderBy(x => x.Order).Select(st => new WorkOrderStageRow(st.StageId, st.Order, StageName(st), st.Status,
            st.Status == "Current" ? string.Join(", ", st.Participants.Where(x => x.Status == "Pending").Select(p => (p.Role ?? "").Replace("Permission:", "").Replace(",", " / ")).Distinct()) : "")).ToList();
    }

    public async Task<List<WorkOrderActionRow>> WorkflowActionsAsync(int id)
    {
        var wf = await WorkflowAsync(id);
        if (wf == null) return [];
        var info = await context.WorkOrders.AsNoTracking().Where(x => x.WorkOrderId == id).Select(x => new { x.Status, x.WorkOrderType }).FirstOrDefaultAsync();
        var inProgress = info?.Status == "InProgress";
        var failure = info?.WorkOrderType is "Corrective" or "Emergency";
        string FormKey(bool reason) => "work-order-wf" + (reason ? "-reason" : "") + (inProgress ? (failure ? "-completion-failure" : "-completion") : "");
        return wf.AvailableActions.Select(a => new WorkOrderActionRow(a.ActionType, Fa && !string.IsNullOrWhiteSpace(a.LabelFa) ? a.LabelFa! : !string.IsNullOrWhiteSpace(a.Label) ? a.Label! : a.ActionType,
            string.IsNullOrWhiteSpace(a.ToStageName) ? "" : StageByName(wf, a.ToStageName), a.IsAllowed, a.IsAllowed ? null : a.DeniedReason, WorkflowActionRules.ActionRequiresReason(a.ActionType), FormKey(WorkflowActionRules.ActionRequiresReason(a.ActionType)))).ToList();
    }

    public async Task<List<WorkOrderWorkflowHistoryRow>> WorkflowHistoryAsync(int id)
    {
        var wf = await WorkflowAsync(id);
        if (wf == null) return [];
        var userIds = wf.HistorySummary.Where(h => h.UserId.HasValue).Select(h => h.UserId!.Value).Distinct().ToList();
        var names = userIds.Count == 0 ? new Dictionary<int, string>() : await context.Employees.AsNoTracking().Where(e => userIds.Contains(e.EmployeeId)).ToDictionaryAsync(e => e.EmployeeId, e => e.FirstName + " " + e.LastName);
        return wf.HistorySummary.OrderByDescending(x => x.ActionDate).Select((h, n) => new WorkOrderWorkflowHistoryRow(n + 1, h.ActionDate, T(h.ActionType), StageByName(wf, h.FromStageName), StageByName(wf, h.ToStageName),
            h.UserId.HasValue ? (names.TryGetValue(h.UserId.Value, out var nm) ? nm : "#" + h.UserId) : "-", h.Comment)).ToList();
    }

    public async Task<List<WorkOrderStatusHistoryRow>> StatusHistoryAsync(int id)
        => (await context.WorkOrderStatusHistories.AsNoTracking().Include(x => x.ChangedBy).Where(x => x.WorkOrderId == id).OrderByDescending(x => x.ChangedDate).ToListAsync())
            .Select(h => new WorkOrderStatusHistoryRow(h.WorkOrderStatusHistoryId, h.ChangedDate, T(h.FromStatus), T(h.ToStatus), h.Reason, h.ChangedBy != null ? h.ChangedBy.FirstName + " " + h.ChangedBy.LastName : "-")).ToList();

    private async Task<PEMS.Domain.Entities.WorkOrder?> OrderAsync(int id) => await context.WorkOrders.AsNoTracking().Include(x => x.Equipment).FirstOrDefaultAsync(x => x.WorkOrderId == id);

    private async Task<WoSectionPage?> PageAsync(int id) => await sectionGuard.GetPageAsync(id, new WoCaller(currentUser.UserId, currentUser.UserName, await EmployeeIdAsync(), currentUser.Roles));

    public async Task<List<WorkOrderLaborRow>> LaborAsync(int id)
        => (await context.WorkOrderLabors.AsNoTracking().Include(x => x.Employee).Include(x => x.Team).Where(x => x.WorkOrderId == id && x.IsActive).OrderByDescending(x => x.WorkDate).ToListAsync())
            .Select(x => new WorkOrderLaborRow(x.WorkOrderLaborId, x.WorkDate, x.Employee != null ? x.Employee.FirstName + " " + x.Employee.LastName : "-", x.Team?.Name, x.RegularHours, x.OvertimeHours, x.LaborCost, x.Notes)).ToList();

    public async Task<List<WorkOrderSpareRow>> SparesAsync(int id)
    {
        var page = await PageAsync(id);
        var can = page != null && page.Controls(WoSection.SpareParts);
        return (await context.MaintenanceSpareUsages.AsNoTracking().Include(x => x.MaterialItem).Where(x => x.WorkOrderId == id && x.IsActive).ToListAsync())
            .Select(x => new WorkOrderSpareRow(x.MaintenanceSpareUsageId, x.MaterialItem != null ? x.MaterialItem.MaterialCode + " - " + x.MaterialItem.Description : "-", x.Quantity, x.UnitCost, x.TotalCost, x.ReturnedQuantity ?? 0, x.UsageDate,
                can && (x.ReturnedQuantity ?? 0) < x.Quantity)).ToList();
    }

    public async Task<List<WorkOrderTaskRow>> TasksAsync(int id)
    {
        var page = await PageAsync(id);
        var can = page != null && page.Controls(WoSection.JobPlanTasks);
        return (await context.WorkOrderTasks.AsNoTracking().Where(x => x.WorkOrderId == id).OrderBy(x => x.TaskNumber).ToListAsync())
            .Select(x => new WorkOrderTaskRow(x.WorkOrderTaskId, x.TaskNumber, x.Title, x.Description, x.IsCompleted, x.CompletedBy, x.CompletedDate, can && !x.IsCompleted, can && x.IsCompleted)).ToList();
    }

    public async Task<List<WorkOrderSkillRow>> SkillsAsync(int id)
    {
        var page = await PageAsync(id);
        var can = page != null && page.Controls(WoSection.RequiredSkills);
        return (await context.RequiredSkills.AsNoTracking().Include(x => x.Skill).Where(x => x.WorkOrderId == id).ToListAsync())
            .Select(x => new WorkOrderSkillRow(x.RequiredSkillId, x.Skill?.Name ?? "-", x.MinimumProficiencyLevel, x.PlannedHours, can)).ToList();
    }

    public async Task<List<WorkOrderMaterialRow>> MaterialsAsync(int id)
    {
        var page = await PageAsync(id);
        var can = page != null && page.Controls(WoSection.RequiredMaterials);
        var intake = await warehouseIntake.GetAsync(id);
        var rows = await context.WorkOrderMaterials.AsNoTracking().Include(x => x.MaterialItem).Where(x => x.WorkOrderId == id).ToListAsync();
        return rows.Select(rm =>
        {
            var il = intake?.Lines.FirstOrDefault(l => l.WorkOrderMaterialId == rm.WorkOrderMaterialId);
            var sourcedAlready = il != null && il.Requisitions.Count > 0;
            var sourced = il != null && rm.SupplyCode == PEMS.Domain.Entities.WorkOrderMaterialSupplyCodes.Stock ? il.SourcedQuantity.ToString("0.###") + " / " + il.RequiredQuantity.ToString("0.###") : "-";
            return new WorkOrderMaterialRow(rm.WorkOrderMaterialId, rm.MaterialItem != null ? rm.MaterialItem.MaterialCode + " - " + rm.MaterialItem.Description : "-", rm.RequiredQuantity, T("Whi_Supply_" + rm.SupplyCode), sourced,
                il == null ? null : string.Join(", ", il.Requisitions.Select(r => r.RequisitionNumber + " (" + r.WarehouseCode + ")")), can && !sourcedAlready, can);
        }).ToList();
    }

    public async Task<List<WorkOrderIntakeRow>> IntakeAsync(int id)
    {
        var intake = await warehouseIntake.GetAsync(id);
        if (intake == null || intake.Lines.Count == 0) return [];
        var unsourced = intake.Lines.Where(l => l.UnsourcedQuantity > 0).ToList();
        var hasDirect = intake.Lines.Any(l => l.SupplyCode == PEMS.Domain.Entities.WorkOrderMaterialSupplyCodes.DirectPurchase);
        var canEdit = await permissions.HasPermissionAsync(currentUser.Roles, "WorkOrder", "Edit");
        if (intake.IsOpen) return [new WorkOrderIntakeRow(id, T("Whi_State_Open"), false, true)];
        if (unsourced.Count == 0 && !hasDirect) return [new WorkOrderIntakeRow(id, T("Whi_State_Sourced"), false, false)];
        return [new WorkOrderIntakeRow(id, T("Whi_State_NotRequested"), canEdit, false)];
    }

    public async Task<List<WorkOrderCrewRow>> CrewsAsync(int id)
        => (await context.Crews.AsNoTracking().Include(x => x.Members).ThenInclude(m => m.Employee).Where(x => x.WorkOrderId == id && x.IsActive).ToListAsync())
            .Select(c => new WorkOrderCrewRow(c.CrewId, c.Code, c.Name, string.Join(", ", c.Members.Where(m => m.IsActive).Select(m => m.Employee != null ? m.Employee.FirstName + " " + m.Employee.LastName : "#" + m.EmployeeId)))).ToList();

    public async Task<List<WorkOrderReviewRow>> ReviewAsync(int id)
    {
        var status = await review.GetDisciplineReviewStatusAsync(id);
        var myDiscipline = await MyDisciplineIdAsync();
        return status.Select(r => new WorkOrderReviewRow(r.DisciplineId, r.DisciplineName, r.Status, r.Status == "Pending" && r.DisciplineId == myDiscipline)).ToList();
    }

    private async Task<int?> MyDisciplineIdAsync()
    {
        var employeeId = await EmployeeIdAsync();
        return employeeId.HasValue ? await context.Employees.AsNoTracking().Where(e => e.EmployeeId == employeeId.Value).Select(e => (int?)e.DisciplineId).FirstOrDefaultAsync() : null;
    }

    public async Task<List<WorkOrderFindingRow>> FindingsAsync(int id)
        => (await context.WorkOrderDisciplineFindings.AsNoTracking().Include(x => x.Discipline).Where(x => x.WorkOrderId == id).OrderByDescending(x => x.RespondedDate).ToListAsync())
            .Select(x => new WorkOrderFindingRow(x.WorkOrderDisciplineFindingId, x.Discipline?.Name ?? "-", x.CycleNumber, x.Decision, x.Findings, x.FailureModeCode, x.FailureCauseCode, x.RespondedDate)).ToList();

    // the choices of the forms: the same lists the old page offered (teams and employees of the responsible discipline when it is set)
    public async Task<List<WorkOrderChoiceRow>> TeamOptionsAsync(int id)
    {
        var wo = await OrderAsync(id);
        var q = context.Teams.AsNoTracking().Where(x => x.IsActive);
        if (wo?.ResponsibleDisciplineId is { } d) q = q.Where(x => x.MaintenanceGroup!.DisciplineId == d);
        return await q.OrderBy(x => x.Name).Select(x => new WorkOrderChoiceRow(x.TeamId.ToString(), x.Name)).ToListAsync();
    }

    public async Task<List<WorkOrderChoiceRow>> EmployeeOptionsAsync(int id)
    {
        var wo = await OrderAsync(id);
        var q = context.Employees.AsNoTracking().Where(x => x.IsActive);
        if (wo?.ResponsibleDisciplineId is { } d) q = q.Where(x => x.DisciplineId == d);
        return await q.OrderBy(x => x.FirstName).ThenBy(x => x.LastName).Select(x => new WorkOrderChoiceRow(x.EmployeeId.ToString(), x.FirstName + " " + x.LastName)).ToListAsync();
    }

    public async Task<List<WorkOrderChoiceRow>> DisciplineOptionsAsync(int id)
        => await context.Disciplines.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name).Select(x => new WorkOrderChoiceRow(x.DisciplineId.ToString(), x.Name)).ToListAsync();

    public async Task<List<WorkOrderChoiceRow>> MaterialItemOptionsAsync(int id)
        => await context.MaterialItems.AsNoTracking().Where(x => x.IsActive && x.IsStockItem).OrderBy(x => x.MaterialCode).Select(x => new WorkOrderChoiceRow(x.MaterialItemId.ToString(), x.MaterialCode + " - " + x.Description)).ToListAsync();

    public async Task<List<WorkOrderChoiceRow>> SkillOptionsAsync(int id)
        => await context.Skills.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name).Select(x => new WorkOrderChoiceRow(x.SkillId.ToString(), x.Name)).ToListAsync();

    // the failure catalogue of the class of the equipment and its parents (and the general modes), the same rule as the completion form
    public async Task<List<WorkOrderChoiceRow>> FailureModeOptionsAsync(int id)
    {
        var wo = await OrderAsync(id);
        var classChain = new HashSet<int>();
        for (int? cid = wo?.Equipment?.EquipmentClassId; cid.HasValue && classChain.Add(cid.Value);)
            cid = await context.EquipmentClasses.Where(c => c.EquipmentClassId == cid.Value).Select(c => c.ParentEquipmentClassId).FirstOrDefaultAsync();
        var q = context.FailureModes.AsNoTracking().Where(x => x.IsActive);
        if (classChain.Count > 0) q = q.Where(x => x.EquipmentClassId == null || classChain.Contains(x.EquipmentClassId.Value));
        return await q.OrderBy(x => x.DisplayOrder).ThenBy(x => x.Name).Select(x => new WorkOrderChoiceRow(x.Code, x.Code + " - " + x.Name)).ToListAsync();
    }

    public async Task<List<WorkOrderChoiceRow>> FailureCauseOptionsAsync(int id)
        => await context.FailureCauses.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.DisplayOrder).ThenBy(x => x.Name).Select(x => new WorkOrderChoiceRow(x.Code, x.Code + " - " + x.Name)).ToListAsync();

    public async Task<(bool Success, string? Error)> LegacyAdvanceAsync(int id, int? assignedTeamId = null, string? failureModeCode = null, string? failureCauseCode = null, string? rootCause = null, decimal? downtimeHours = null, string? reason = null,
        string? failureEffect = null, string? correctiveAction = null, decimal? contractorCost = null, string? contractorName = null)
    {
        var wo = await context.WorkOrders.AsNoTracking().FirstOrDefaultAsync(x => x.WorkOrderId == id);
        if (wo == null) return (false, T("Not found"));
        var next = NextStatus(wo.Status);
        if (next == null) return (false, "This work order is already at its final status");
        if (next == "Closed" && !await permissions.HasPermissionAsync(currentUser.Roles, "WorkOrder", "Approve")) return (false, "برای بستن نهایی WorkOrder مجوز کافی نداری.");
        if (next == "Completed" && await review.HasPendingDisciplineReviewAsync(id))
            return (false, "این WorkOrder یک بازبینی چند-دیسیپلینه‌ی باز داره - قبل از تکمیل، همه‌ی دیسیپلین‌های ارجاع‌شده باید نظرشون رو ثبت کنن.");
        return await lifecycle.AdvanceStatusAsync(id, next, reason, await EmployeeIdAsync(), assignedTeamId, null, failureModeCode, failureCauseCode, rootCause, downtimeHours, failureEffect, correctiveAction, contractorCost, contractorName);
    }
}
