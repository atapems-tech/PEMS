using System.Globalization;
using PEMS.Application.Common;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;

namespace PEMS.Web.Services;

/// <summary>SCRUM-713: what KpiController did, as operations: the same services (KPI, reliability, executive, targets), the same period clamp (at most 366 days, swapped dates normalised), the same number parsing for targets.</summary>
public sealed class KpiOperations(IKpiService kpi, IReliabilityService reliability, IExecutiveKpiService executive, IKpiTargetService targets) : IKpiOperations
{
    private const int MaxPeriodDays = 366;
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public async Task<List<ReportRow>> DashboardAsync(DateTime? fromDate = null, DateTime? toDate = null)
    {
        var cost = await kpi.GetCostKpisAsync(fromDate, toDate);
        var maintenance = await kpi.GetMaintenanceKpisAsync(fromDate, toDate);
        var fleet = await reliability.GetFleetReliabilityAsync();
        var rows = new List<ReportRow>();
        rows.AddRange(ReportReads.Flatten(cost).Select(r => r with { Section = "Cost" }));
        rows.AddRange(ReportReads.Flatten(maintenance).Select(r => r with { Section = "Maintenance" }));
        var withAvailability = fleet.Where(x => x.AvailabilityPercent.HasValue).ToList();
        rows.Add(new("Reliability", "Equipment with failures", fleet.Count.ToString(Inv)));
        rows.Add(new("Reliability", "Total failure count", fleet.Sum(x => x.FailureCount).ToString(Inv)));
        rows.Add(new("Reliability", "Average availability percent", withAvailability.Count > 0 ? withAvailability.Average(x => x.AvailabilityPercent!.Value).ToString("N1", Inv) : ""));
        rows.AddRange(cost.TopEquipmentByCost.Select(e => new ReportRow("Top equipment by cost", e.TagNumber, e.Cost.ToString("N0", Inv))));
        rows.AddRange(cost.CostByFunctionalLocation.Select(e => new ReportRow("Cost by functional location", e.GroupName, e.Cost.ToString("N0", Inv))));
        rows.AddRange(cost.CostByMaintenanceType.Select(e => new ReportRow("Cost by maintenance type", e.GroupName, e.Cost.ToString("N0", Inv))));
        return rows;
    }

    public async Task<List<KpiTileRow>> TilesAsync(DateTime? fromDate = null, DateTime? toDate = null)
    {
        var (from, to) = Period(fromDate, toDate);
        var d = await executive.GetAsync(from, to);
        return d.Tiles.Select(t => new KpiTileRow(t.Section, t.Key, t.Value is null ? "" : Number(t.Value.Value, t.Decimals) + Suffix(t.Unit),
            t.Target is null ? "" : (t.Direction == KpiDirections.LowerIsBetter ? "<= " : ">= ") + Number(t.Target.Value, t.Decimals) + Suffix(t.Unit), t.Status, t.Detail ?? "")).ToList();
    }

    public async Task<List<ReportRow>> BreakdownAsync(DateTime? fromDate = null, DateTime? toDate = null)
    {
        var (from, to) = Period(fromDate, toDate);
        var d = await executive.GetAsync(from, to);
        var rows = d.EquipmentByStatus.Select(s => new ReportRow("Equipment by status", s.Name, s.Count.ToString("N0", Inv))).ToList();
        rows.AddRange(d.TopEquipmentByCost.Select(e => new ReportRow("Top equipment by cost", e.TagNumber, e.Cost.ToString("N0", Inv))));
        return rows;
    }

    public async Task<List<KpiTargetRow>> TargetsAsync()
        => (await targets.GetEffectiveAsync()).Select(x => new KpiTargetRow(x.Key, x.Section, x.Direction, x.Unit, x.Target?.ToString("0.####", Inv) ?? "", x.Warn?.ToString("0.####", Inv) ?? "", x.IsCustom ? "Custom" : "Default")).ToList();

    public async Task<KpiSaveResult> SaveTargetAsync(string key, string? target = null, string? warn = null)
    {
        if (!TryParse(target, out var targetValue) || !TryParse(warn, out var warnValue)) return new(false, "Kpi_Err_NotANumber");
        var result = await targets.SaveAsync(key, targetValue, warnValue);
        return result.Success ? new(true, null) : new(false, result.Errors[0].Key);
    }

    private static (DateTime From, DateTime To) Period(DateTime? fromDate, DateTime? toDate)
    {
        var to = (toDate ?? DateTime.Today).Date;
        var from = (fromDate ?? to.AddDays(-29)).Date;
        if (from > to) (from, to) = (to, from);
        if ((to - from).TotalDays > MaxPeriodDays) from = to.AddDays(-MaxPeriodDays);
        return (from, to);
    }

    private static string Number(decimal v, int decimals) => v.ToString("N" + decimals, Inv);
    private static string Suffix(string unit) => unit switch { "%" => "%", "h" => " h", "d" => " d", _ => "" };

    private static bool TryParse(string? text, out decimal? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(text)) return true;
        if (!decimal.TryParse(text.Trim(), NumberStyles.Number, Inv, out var parsed)) return false;
        value = parsed;
        return true;
    }
}
