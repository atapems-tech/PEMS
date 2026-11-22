using Microsoft.Extensions.Localization;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Context;
using PEMS.Web.Resources;
using PEMS.Web.Security;

namespace PEMS.Web.Services;

/// <summary>SCRUM-752: the strategy and assignment workbook import (see <see cref="IStrategyImportOperations"/>) over the same service and actor the controller used.</summary>
public sealed class StrategyImportOperations(PEMSDbContext context, IStrategyImportService import, ICurrentUserService currentUser, IStringLocalizer<SharedResource>? localizer = null) : IStrategyImportOperations
{
    private const long MaxBytes = 5 * 1024 * 1024;

    public Task<(bool Success, string? Error)> ImportStrategiesAsync(byte[] file, bool commit) => RunAsync(file, commit, (ms, c, actor) => import.ImportStrategiesAsync(ms, c, actor));
    public Task<(bool Success, string? Error)> ImportAssignmentsAsync(byte[] file, bool commit) => RunAsync(file, commit, (ms, c, actor) => import.ImportAssignmentsAsync(ms, c, actor));

    public Task<List<StrategyTemplateRow>> StrategyTemplateAsync() => Task.FromResult(new List<StrategyTemplateRow>
    {
        new("PUMP_CENT", "CBM", "PUMP_EXCESSIVE_VIBRATION", "Operational", "", "Vibration trend shows bearing wear before functional failure (OEM manual §7).", "Monthly", "30", "ConditionMonitoring", "MECH",
            "Vibration velocity", "mm/s", "4.5", "7.1", "90", "30", "OEM", "Centrifugal pump - vibration - CBM", "Pump cannot deliver rated flow", "Bearing failure, seal damage", "Vibration analyst", "2", "")
    });

    public Task<List<AssignmentTemplateRow>> AssignmentTemplateAsync() => Task.FromResult(new List<AssignmentTemplateRow> { new("", "PUMP_CENT", "CBM", "PUMP_EXCESSIVE_VIBRATION", "80-P-101A") });

    private async Task<(bool Success, string? Error)> RunAsync(byte[] file, bool commit, Func<MemoryStream, bool, StrategyActor, Task<StrategyImportResultDto>> run)
    {
        if (file.Length == 0 || file.Length > MaxBytes) return (false, localizer?["Rcm_Err_ImportFileInvalid"].Value ?? "Rcm_Err_ImportFileInvalid");
        var actor = await context.ResolveStrategyActorAsync(currentUser);
        using var ms = new MemoryStream(file);
        var result = await run(ms, commit, actor);
        var rejected = result.Rows.Where(r => !r.Accepted).Select(r => $"#{r.RowNumber} {r.Reference}: {string.Join("; ", localizer.TranslateAll(r.Errors))}").ToList();
        var summary = $"{(commit ? "Imported" : "Checked")}: {result.RowsRead} rows read, {result.RowsAccepted} accepted, {result.RowsRejected} rejected.";
        if (!commit) return (false, summary + (rejected.Count > 0 ? " " + string.Join(" | ", rejected.Take(10)) : " Tick the import box and upload again to write the valid rows."));
        return rejected.Count == 0 ? (true, null) : (false, summary + " " + string.Join(" | ", rejected.Take(10)));
    }
}
