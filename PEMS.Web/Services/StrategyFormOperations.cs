using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using PEMS.Application.Common;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Domain.Common;
using PEMS.Persistence.Context;
using PEMS.Web.Resources;
using PEMS.Web.Security;

namespace PEMS.Web.Services;

/// <summary>SCRUM-829: the strategy forms and the bulk assignment as operations (see <see cref="IStrategyFormOperations"/>).</summary>
public sealed class StrategyFormOperations(PEMSDbContext context, IMaintenanceStrategyService strategies, IMaintenanceStrategyCommandService commands, IStrategyAssignmentService assignments, IFailureModeCatalogService catalog,
    IPermissionService permissions, ICurrentUserService currentUser, IStringLocalizer<SharedResource> l) : IStrategyFormOperations
{
    private static int? Int(string? s) => int.TryParse(s, out var v) ? v : null;
    private static decimal? Dec(string? s) => decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : null;
    private static DateTime? Date(string? s) => DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;
    private static byte[]? Version(string? s) { try { return string.IsNullOrEmpty(s) ? null : Convert.FromBase64String(s); } catch (FormatException) { return null; } }

    public async Task<List<StrategyFormRow>> OneAsync(int id)
    {
        var d = await strategies.GetDetailAsync(id);
        if (d == null) return [];
        var i = d.Input;
        return [new StrategyFormRow(id, i.RowVersion is { Length: > 0 } ? Convert.ToBase64String(i.RowVersion) : "", d.Status, d.VersionNumber, d.Status is RcmCodes.Status.Approved or RcmCodes.Status.Active, i.EquipmentClassId?.ToString(CultureInfo.InvariantCulture), i.Title, i.FailureModeId?.ToString(CultureInfo.InvariantCulture), i.FailureCauseId?.ToString(CultureInfo.InvariantCulture), i.FunctionalFailure, i.FailureEffect, i.ConsequenceCategory, i.IsHiddenFailure.ToString().ToLowerInvariant(), i.StrategyType, i.Source, i.TaskType, i.OwnerDisciplineId?.ToString(CultureInfo.InvariantCulture), i.Frequency, i.DefaultIntervalDays?.ToString(CultureInfo.InvariantCulture), i.RecommendedTriggerType, i.RequiredSkills, i.EstimatedDurationHours?.ToString(CultureInfo.InvariantCulture), i.ReviewDate?.ToString("yyyy-MM-dd"), i.CbmParameter, i.ThresholdUnit, i.AlarmThreshold?.ToString(CultureInfo.InvariantCulture), i.TripThreshold?.ToString(CultureInfo.InvariantCulture), i.PfIntervalDays?.ToString(CultureInfo.InvariantCulture), i.InspectionIntervalDays?.ToString(CultureInfo.InvariantCulture), i.StartDate?.ToString("yyyy-MM-dd"), i.Justification, i.Description)];
    }

    public async Task<StrategyAnswer> SaveAsync(int id, string? rowVersion, string? equipmentClassId, string? title, string? failureModeId, string? failureCauseId, string? functionalFailure, string? failureEffect, string? consequenceCategory, bool isHiddenFailure, string? strategyType, string? source, string? taskType, string? ownerDisciplineId, string? frequency, string? defaultIntervalDays, string? recommendedTriggerType, string? requiredSkills, string? estimatedDurationHours, string? reviewDate, string? cbmParameter, string? thresholdUnit, string? alarmThreshold, string? tripThreshold, string? pfIntervalDays, string? inspectionIntervalDays, string? startDate, string? justification, string? description)
    {
        if (!await permissions.HasPermissionAsync(currentUser.Roles, "MaintenanceStrategy", id == 0 ? "Create" : "Edit")) throw new UnauthorizedAccessException();
        var input = new StrategyInputDto { RowVersion = Version(rowVersion), EquipmentClassId = Int(equipmentClassId), Title = title, FailureModeId = Int(failureModeId), FailureCauseId = Int(failureCauseId), FunctionalFailure = functionalFailure, FailureEffect = failureEffect, ConsequenceCategory = consequenceCategory, IsHiddenFailure = isHiddenFailure, StrategyType = strategyType, Source = source, TaskType = taskType, OwnerDisciplineId = Int(ownerDisciplineId), Frequency = frequency, DefaultIntervalDays = Int(defaultIntervalDays), RecommendedTriggerType = recommendedTriggerType, RequiredSkills = requiredSkills, EstimatedDurationHours = Dec(estimatedDurationHours), ReviewDate = Date(reviewDate), CbmParameter = cbmParameter, ThresholdUnit = thresholdUnit, AlarmThreshold = Dec(alarmThreshold), TripThreshold = Dec(tripThreshold), PfIntervalDays = Int(pfIntervalDays), InspectionIntervalDays = Int(inspectionIntervalDays), StartDate = Date(startDate), Justification = justification, Description = description };
        var actor = await context.ResolveStrategyActorAsync(currentUser);
        var result = id == 0 ? await commands.CreateAsync(input, actor) : await commands.UpdateAsync(id, input, actor);
        return result.Success ? new(true, null, result.Id ?? id) : new(false, string.Join(" | ", l.TranslateAll(result.Errors)), 0);
    }

    private static List<StrategyChoice> Pick<T>(IEnumerable<T> rows, Func<T, string> v, Func<T, string> t) => rows.Select(r => new StrategyChoice(v(r), t(r))).ToList();

    public async Task<List<StrategyChoice>> ClassesAsync() => Pick(await context.EquipmentClasses.AsNoTracking().Where(c => c.IsActive).OrderBy(c => c.Name).Select(c => new { c.EquipmentClassId, c.Name, c.Code }).ToListAsync(), x => x.EquipmentClassId.ToString(), x => x.Name + " (" + x.Code + ")");
    public async Task<List<StrategyChoice>> DisciplinesAsync() => Pick(await context.Disciplines.AsNoTracking().Where(d => d.IsActive).OrderBy(d => d.Name).Select(d => new { d.DisciplineId, d.Name }).ToListAsync(), x => x.DisciplineId.ToString(), x => x.Name);
    public async Task<List<StrategyChoice>> ModesAsync(string? classId = null) => Pick(await catalog.SearchAsync(Int(classId), null, 2000), m => m.FailureModeId.ToString(), m => m.Code + " " + m.Name);
    public async Task<List<StrategyChoice>> CausesAsync(string? modeId = null)
    {
        var mode = Int(modeId);
        var modes = await catalog.SearchAsync(null, null, 3000);
        return Pick(modes.Where(m => mode == null || m.FailureModeId == mode).SelectMany(m => m.Causes).DistinctBy(c => c.FailureCauseId), c => c.FailureCauseId.ToString(), c => c.Code + " " + c.Name);
    }

    public async Task<List<StrategyChoice>> StrategiesAsync()
        => Pick(await context.MaintenanceStrategies.AsNoTracking().Where(s => s.IsActive && (s.Status == RcmCodes.Status.Approved || s.Status == RcmCodes.Status.Active)).OrderBy(s => s.EquipmentClass!.Name).ThenBy(s => s.FailureModeCode)
            .Select(s => new { s.MaintenanceStrategyId, Class = s.EquipmentClass != null ? s.EquipmentClass.Name : "-", s.StrategyType, Mode = s.FailureMode != null ? s.FailureMode.Name : s.FailureModeCode, s.VersionNumber }).Take(500).ToListAsync(),
            x => x.MaintenanceStrategyId.ToString(), x => $"{x.Class} · {x.StrategyType} · {x.Mode} · v{x.VersionNumber}");
    public async Task<List<StrategyChoice>> EquipmentTypesAsync() => Pick(await context.Equipments.Where(x => x.IsActive && x.EquipmentType != "").Select(x => x.EquipmentType).Distinct().OrderBy(x => x).ToListAsync(), x => x, x => x);
    public async Task<List<StrategyChoice>> PlantsAsync() => Pick(await context.FunctionalLocations.Where(x => x.IsActive && x.Plant != null && x.Plant != "").Select(x => x.Plant!).Distinct().OrderBy(x => x).ToListAsync(), x => x, x => x);
    public async Task<List<StrategyChoice>> AreasAsync() => Pick(await context.FunctionalLocations.Where(x => x.IsActive && x.Area != null && x.Area != "").Select(x => x.Area!).Distinct().OrderBy(x => x).ToListAsync(), x => x, x => x);
    public async Task<List<StrategyChoice>> UnitsAsync() => Pick(await context.FunctionalLocations.Where(x => x.IsActive && x.Unit != null && x.Unit != "").Select(x => x.Unit!).Distinct().OrderBy(x => x).ToListAsync(), x => x, x => x);

    public async Task<List<StrategyCandidateRow>> CandidatesAsync(string? strategyId = null, string? classId = null, string? disciplineId = null, string? equipmentType = null, string? plant = null, string? area = null, string? unit = null, string? tagSearch = null, string? tagList = null)
    {
        const int maxResults = 200;
        var requestedTags = (tagList ?? "").Split([',', '\n', '\r', ';'], StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).Where(x => x.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var cls = Int(classId);
        HashSet<int>? classScope = null;
        if (cls.HasValue)
        {
            var pairs = await context.EquipmentClasses.AsNoTracking().Select(c => new { c.EquipmentClassId, c.ParentEquipmentClassId }).ToListAsync();
            classScope = [cls.Value];
            var queue = new Queue<int>(classScope);
            while (queue.Count > 0) { var cur = queue.Dequeue(); foreach (var child in pairs.Where(p => p.ParentEquipmentClassId == cur)) if (classScope.Add(child.EquipmentClassId)) queue.Enqueue(child.EquipmentClassId); }
        }
        var disciplineIds = new List<int>();
        if (Int(disciplineId) is { } did)
        {
            var name = (await context.Disciplines.AsNoTracking().Where(d => d.DisciplineId == did).Select(d => d.Name).FirstOrDefaultAsync() ?? "").Trim().ToLowerInvariant();
            disciplineIds = (await context.Disciplines.AsNoTracking().Where(d => d.IsActive).Select(d => new { d.DisciplineId, d.Name }).ToListAsync()).Where(d => (d.Name ?? "").Trim().ToLowerInvariant() == name).Select(d => d.DisciplineId).ToList();
            if (disciplineIds.Count == 0) disciplineIds.Add(did);
        }
        var narrowing = disciplineIds.Count > 0 || !string.IsNullOrWhiteSpace(equipmentType) || !string.IsNullOrWhiteSpace(plant) || !string.IsNullOrWhiteSpace(area) || !string.IsNullOrWhiteSpace(unit) || !string.IsNullOrWhiteSpace(tagSearch) || classScope != null;
        if (!narrowing && requestedTags.Count == 0) return [];
        var ids = new HashSet<int>();
        if (narrowing)
        {
            var q = context.Equipments.Where(x => x.IsActive);
            if (disciplineIds.Count > 0) q = q.Where(x => disciplineIds.Contains(x.DisciplineId));
            if (!string.IsNullOrWhiteSpace(equipmentType)) q = q.Where(x => x.EquipmentType == equipmentType);
            if (classScope != null) q = q.Where(x => x.EquipmentClassId != null && classScope.Contains(x.EquipmentClassId.Value));
            if (!string.IsNullOrWhiteSpace(plant)) q = q.Where(x => x.FunctionalLocationRef != null && x.FunctionalLocationRef.Plant == plant);
            if (!string.IsNullOrWhiteSpace(area)) q = q.Where(x => x.FunctionalLocationRef != null && x.FunctionalLocationRef.Area == area);
            if (!string.IsNullOrWhiteSpace(unit)) q = q.Where(x => x.FunctionalLocationRef != null && x.FunctionalLocationRef.Unit == unit);
            if (!string.IsNullOrWhiteSpace(tagSearch)) q = q.Where(x => x.TagNumber.Contains(tagSearch.Trim()));
            foreach (var id in await q.OrderBy(x => x.TagNumber).Take(maxResults).Select(x => x.EquipmentId).ToListAsync()) ids.Add(id);
        }
        if (requestedTags.Count > 0)
            foreach (var id in await context.Equipments.Where(x => x.IsActive && requestedTags.Contains(x.TagNumber)).Select(x => x.EquipmentId).ToListAsync()) ids.Add(id);
        var list = await context.Equipments.AsNoTracking().Include(x => x.EquipmentClassRef).Where(x => ids.Contains(x.EquipmentId)).OrderBy(x => x.TagNumber).Take(maxResults).ToListAsync();
        var equipmentIds = list.Select(x => x.EquipmentId).ToList();
        var criticality = await context.EquipmentCriticalities.Where(x => x.IsActive && equipmentIds.Contains(x.EquipmentId)).ToDictionaryAsync(x => x.EquipmentId, x => x.CriticalityClass);
        var candidates = Int(strategyId) is { } sid && equipmentIds.Count > 0 ? (await assignments.EvaluateAsync(sid, equipmentIds)).ToDictionary(c => c.Equipment.EquipmentId) : [];
        return list.Select(x => new StrategyCandidateRow(x.EquipmentId, x.TagNumber, x.Name, x.EquipmentClassRef?.Name, null, null, criticality.GetValueOrDefault(x.EquipmentId),
            candidates.TryGetValue(x.EquipmentId, out var c) ? c.Conflict : "", !candidates.TryGetValue(x.EquipmentId, out var c2) || c2.WillReceive)).ToList();
    }

    public async Task<StrategyAnswer> AssignAsync(int strategyId, List<int> equipmentIds, string? culture = null)
    {
        if (equipmentIds == null || equipmentIds.Count == 0) return new(false, l["Rcm_Err_NoEquipmentSelected"].Value, 0);
        var result = await assignments.AssignAsync(strategyId, equipmentIds, await context.ResolveStrategyActorAsync(currentUser));
        if (result.Errors.Count > 0) return new(false, string.Join(" | ", l.TranslateAll(result.Errors)), 0);
        var parts = new List<string> { l["Rcm_Info_AssignedCount", result.Assigned].Value };
        if (result.SkippedAlreadyAssigned > 0) parts.Add(l["Rcm_Info_SkippedAlready", result.SkippedAlreadyAssigned].Value);
        if (result.SkippedConflict > 0) parts.Add(l["Rcm_Info_SkippedConflict", result.SkippedConflict].Value);
        if (result.SkippedCriticality > 0) parts.Add(l["Rcm_Info_SkippedCriticality", result.SkippedCriticality].Value);
        if (result.SkippedNotFound > 0) parts.Add(l["Rcm_Info_SkippedNotFound", result.SkippedNotFound].Value);
        if (result.Skipped.Count > 0) parts.Add(string.Join(", ", result.Skipped.Take(20).Select(s => $"{s.Tag} ({s.Reason})")) + (result.Skipped.Count > 20 ? " ..." : ""));
        return new(true, string.Join(" · ", parts), strategyId);
    }
}
