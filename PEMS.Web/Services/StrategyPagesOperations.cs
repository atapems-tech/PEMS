using System.Text;
using Microsoft.Extensions.Localization;
using PEMS.Application.DTOs;
using PEMS.Application.Common;
using PEMS.Application.Interfaces;
using PEMS.Domain.Common;
using PEMS.Persistence.Context;
using PEMS.Web.Helpers;
using PEMS.Web.Resources;
using PEMS.Web.Security;

namespace PEMS.Web.Services;

/// <summary>SCRUM-829: the coverage report, the tag view and the failure-mode picker of MaintenanceStrategyController as read operations over the same services (see <see cref="IStrategyPagesOperations"/>).</summary>
public sealed class StrategyPagesOperations(IStrategyCoverageService coverage, IStrategyAssignmentService assignments, IFailureModeCatalogService catalog,
    IMaintenanceStrategyService strategies, ICurrentUserService currentUser, IPermissionService permissions, PEMSDbContext context, IStringLocalizer<SharedResource> localizer) : IStrategyPagesOperations
{
    public async Task<List<StrategyCoverageSummaryRow>> CoverageSummaryAsync()
    {
        var c = await coverage.GetCoverageAsync();
        var pct = c.TotalEquipment == 0 ? 0 : Math.Round(100.0 * c.CoveredEquipment / c.TotalEquipment, 1);
        return [new StrategyCoverageSummaryRow(1, c.TotalEquipment, c.CoveredEquipment, pct, c.UnratedEquipment, c.UnclassifiedEquipment, c.CriticalWithoutStrategyTotal, c.UnratedEquipment == c.TotalEquipment && c.TotalEquipment > 0)];
    }

    private static List<StrategyCoverageGroupRow> Groups(IEnumerable<CoverageRowDto> rows)
        => rows.Select(r => new StrategyCoverageGroupRow(r.Key, r.TotalEquipment, r.WithApprovedStrategy, r.WithoutStrategy, r.CoveragePercent)).ToList();

    public async Task<List<StrategyCoverageGroupRow>> CoverageByCriticalityAsync() => Groups((await coverage.GetCoverageAsync()).ByCriticality);

    public async Task<List<StrategyCoverageGroupRow>> CoverageByClassAsync() => Groups((await coverage.GetCoverageAsync()).ByClass);

    public async Task<List<StrategyUncoveredRow>> UncoveredCriticalAsync()
        => (await coverage.GetCoverageAsync()).CriticalWithoutStrategy
            .Select(r => new StrategyUncoveredRow(r.Equipment.EquipmentId, r.Equipment.Tag, r.Equipment.Name, r.Equipment.EquipmentClassName, r.Equipment.Area, r.Equipment.Unit, r.Equipment.CriticalityClass)).ToList();

    public async Task<StrategyFileAnswer> UncoveredCsvAsync()
    {
        var all = await coverage.GetCoverageAsync(topUncoveredCritical: 5000);
        var sb = new StringBuilder("Tag,Name,Class,Area,Unit,Criticality\n");
        foreach (var r in all.CriticalWithoutStrategy)
        {
            var e = r.Equipment;
            sb.Append(string.Join(',', new[] { e.Tag, e.Name, e.EquipmentClassName, e.Area, e.Unit, e.CriticalityClass }.Select(v => "\"" + (v ?? "").Replace("\"", "\"\"") + "\""))).Append('\n');
        }
        return new StrategyFileAnswer(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray(), "critical-equipment-without-strategy.csv", "text/csv");
    }

    public async Task<List<StrategyTagIdentityRow>> TagIdentityAsync(int equipmentId)
    {
        var v = await assignments.GetTagViewAsync(equipmentId);
        if (v == null) return [];
        var e = v.Equipment;
        var areaUnit = string.Join(" / ", new[] { e.Area, e.Unit }.Where(x => !string.IsNullOrWhiteSpace(x)));
        return [new StrategyTagIdentityRow(e.EquipmentId, e.Tag, e.Name, e.EquipmentClassName, areaUnit.Length > 0 ? areaUnit : null, e.CriticalityClass)];
    }

    public async Task<List<StrategyTagAssignmentRow>> TagAssignmentsAsync(int equipmentId)
        => (await assignments.GetTagViewAsync(equipmentId))?.Assignments.Select(a => new StrategyTagAssignmentRow(a.AssignmentId, a.MaintenanceStrategyId, a.StrategyVersion, a.OverrideStatus, a.PlanCount)).ToList() ?? [];

    public async Task<List<StrategyTagPlanRow>> TagPlansAsync(int equipmentId)
        => (await assignments.GetTagViewAsync(equipmentId))?.Plans.Select(p => new StrategyTagPlanRow(p.MaintenancePlanId, p.PlanNumber, p.Title, p.StrategyVersion, p.NextDueDate)).ToList() ?? [];

    public async Task<List<StrategyTagWorkOrderRow>> TagWorkOrdersAsync(int equipmentId)
        => (await assignments.GetTagViewAsync(equipmentId))?.OpenWorkOrders.Select(w => new StrategyTagWorkOrderRow(w.WorkOrderId, w.WorkOrderNumber, w.Status, w.Priority)).ToList() ?? [];

    public Task<List<FailureModeOptionDto>> FailureModesAsync(int? classId = null, string? q = null) => catalog.SearchAsync(classId, q, 50);

    private Task<bool> Can(string action) => permissions.HasPermissionAsync(currentUser.Roles, "MaintenanceStrategy", action);

    public async Task<List<StrategyListRow>> ListAsync()
    {
        // the old page hid replaced versions unless asked for; the status column filters the rest
        var result = await strategies.GetListAsync(new StrategyListFilter { Page = 1, PageSize = 5000 });
        return result.Items.Where(x => x.Status != RcmCodes.Status.Superseded).Select(s => new StrategyListRow(s.MaintenanceStrategyId, s.EquipmentClassName, s.EquipmentTagNumber,
            localizer.Label("StrategyType", s.StrategyType), FailureModeText(s), localizer.Label("Consequence", s.ConsequenceCategory), localizer.Label("Frequency", s.Frequency), localizer.Label("Status", s.Status),
            s.VersionNumber, s.AssignedTagCount, s.ReviewOverdue, s.Justification is { Length: > 140 } j ? j[..140] + "..." : s.Justification)).ToList();
    }

    private string FailureModeText(MaintenanceStrategyListDto s)
        => !string.IsNullOrEmpty(s.FailureModeName) ? RcmDisplay.Name(s.FailureModeName, s.FailureModeNameFa)
            : !string.IsNullOrEmpty(s.FailureModeCode) ? s.FailureModeCode + " (" + localizer["Rcm_NotInCatalog"].Value + ")"
            : localizer["Rcm_MissingFailureMode"].Value;

    public async Task<List<StrategyHeadRow>> HeadAsync(int id)
    {
        var d = await strategies.GetDetailAsync(id);
        if (d == null) return [];
        var i = d.Input;
        var actor = await context.ResolveStrategyActorAsync(currentUser);
        bool canEdit = await Can("Edit"), canApprove = await Can("Approve");
        var live = d.IsActive && d.Status != "Retired" && d.Status != "Superseded";
        var isAuthor = actor.EmployeeId.HasValue && d.AuthorEmployeeId == actor.EmployeeId;
        var cbm = i.StrategyType is "CBM" or "Predictive" ? (i.CbmParameter + " " + i.ThresholdUnit).Trim() : null;
        var thresholds = i.StrategyType is "CBM" or "Predictive" ? i.AlarmThreshold + " / " + i.TripThreshold : null;
        var approved = string.IsNullOrEmpty(d.ApprovedBy) ? null : (d.ApprovedBy + " " + (d.ApprovedAt?.ToString("yyyy-MM-dd") ?? "")).Trim();
        return [new StrategyHeadRow(d.MaintenanceStrategyId, string.IsNullOrWhiteSpace(i.Title) ? localizer["Rcm_Strategy"].Value : i.Title!, localizer.Label("Status", d.Status), d.Status, d.VersionNumber, d.EquipmentClassName,
            localizer.Label("StrategyType", i.StrategyType), RcmDisplay.Name(d.FailureModeName, d.FailureModeNameFa), localizer.Label("Consequence", i.ConsequenceCategory) + (i.IsHiddenFailure ? " / " + localizer["Rcm_HiddenFailure"].Value : ""), i.IsHiddenFailure,
            localizer.Label("TaskType", i.TaskType), d.OwnerDisciplineName, (localizer.Label("Frequency", i.Frequency) + (i.DefaultIntervalDays.HasValue ? $" ({i.DefaultIntervalDays} d)" : "")).Trim(), localizer.Label("Source", i.Source),
            i.FunctionalFailure, i.FailureEffect, cbm, thresholds, i.PfIntervalDays, i.InspectionIntervalDays, i.Justification, i.ReviewDate, approved, d.LegacyEquipmentTag,
            canEdit && live, canEdit && d.Status == "Draft", canApprove && d.Status == "InReview", canApprove && d.Status == "Approved", canApprove && live, canApprove && d.Status is "Approved" or "Active", isAuthor && canApprove && d.Status == "InReview")];
    }

    public async Task<List<StrategyVersionRow>> VersionsAsync(int id)
    {
        var d = await strategies.GetDetailAsync(id);
        return d == null || d.Versions.Count < 2 ? [] : d.Versions.Select(v => new StrategyVersionRow(v.MaintenanceStrategyId, v.VersionNumber, localizer.Label("Status", v.Status), v.ApprovedAt, v.MaintenanceStrategyId == id)).ToList();
    }

    public async Task<List<StrategyAssignmentRow>> AssignmentsAsync(int id)
    {
        var d = await strategies.GetDetailAsync(id);
        if (d == null) return [];
        bool canEdit = await Can("Edit"), canApprove = await Can("Approve");
        var usable = d.Status is "Approved" or "Active";
        return d.Assignments.Select(a => new StrategyAssignmentRow(a.AssignmentId, a.Equipment.EquipmentId, a.Equipment.Tag, a.Equipment.Name, a.Equipment.EquipmentClassName, a.Status,
            a.OverrideStatus == "None" ? "" : localizer.Label("OverrideStatus", a.OverrideStatus) + (a.OverrideIntervalDays.HasValue ? " " + a.OverrideIntervalDays + " d" : ""), a.OverrideJustification, a.PlanCount, a.AssignedDate,
            a.OverrideStatus == "None" && canEdit && usable, a.OverrideStatus == "Pending" && canApprove, canApprove)).ToList();
    }

    public async Task<List<StrategyPlanRow>> PlansAsync(int id)
        => (await strategies.GetDetailAsync(id))?.Plans.Select(p => new StrategyPlanRow(p.MaintenancePlanId, p.PlanNumber, p.Tag, p.Title, "v" + p.StrategyVersion, p.OutOfSync)).ToList() ?? [];

    public async Task<List<StrategyHistoryRow>> HistoryAsync(int id)
        => (await strategies.GetDetailAsync(id))?.History.Select((h, n) => new StrategyHistoryRow(n + 1, h.Timestamp, h.ActorName, h.Action, h.FromStatus, h.ToStatus, h.ChangesSummary, h.Comment)).ToList() ?? [];
}
