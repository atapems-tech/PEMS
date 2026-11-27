using System.Text;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;

namespace PEMS.Web.Services;

/// <summary>SCRUM-829: the coverage report, the tag view and the failure-mode picker of MaintenanceStrategyController as read operations over the same services (see <see cref="IStrategyPagesOperations"/>).</summary>
public sealed class StrategyPagesOperations(IStrategyCoverageService coverage, IStrategyAssignmentService assignments, IFailureModeCatalogService catalog) : IStrategyPagesOperations
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
}
