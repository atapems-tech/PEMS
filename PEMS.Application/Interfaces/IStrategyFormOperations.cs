namespace PEMS.Application.Interfaces;

public sealed record StrategyAnswer(bool Success, string? Error, int NewId);
public sealed record StrategyFormRow(int Id, string RowVersion, string Status, int Version, bool NewVersion, string? EquipmentClassId, string? Title, string? FailureModeId, string? FailureCauseId, string? FunctionalFailure, string? FailureEffect, string? ConsequenceCategory, string? IsHiddenFailure, string? StrategyType, string? Source, string? TaskType, string? OwnerDisciplineId, string? Frequency, string? DefaultIntervalDays, string? RecommendedTriggerType, string? RequiredSkills, string? EstimatedDurationHours, string? ReviewDate, string? CbmParameter, string? ThresholdUnit, string? AlarmThreshold, string? TripThreshold, string? PfIntervalDays, string? InspectionIntervalDays, string? StartDate, string? Justification, string? Description);
public sealed record StrategyChoice(string Value, string Label);
public sealed record StrategyCandidateRow(int Id, string Tag, string Name, string? Class, string? Area, string? Unit, string? Criticality, string Conflict, bool WillReceive);

/// <summary>SCRUM-829: the forms of the maintenance strategy (create, edit) and the bulk assignment (candidates, assign) as operations over the same services; the rules and the messages are the services'.</summary>
public interface IStrategyFormOperations
{
    [PEMS.Application.Common.PemsOperation("StrategyForm", "One", "MaintenanceStrategy.Edit")]
    Task<List<StrategyFormRow>> OneAsync(int id);

    [PEMS.Application.Common.PemsOperation("StrategyForm", "Save")]
    Task<StrategyAnswer> SaveAsync(int id, string? rowVersion, string? equipmentClassId, string? title, string? failureModeId, string? failureCauseId, string? functionalFailure, string? failureEffect, string? consequenceCategory, bool isHiddenFailure, string? strategyType, string? source, string? taskType, string? ownerDisciplineId, string? frequency, string? defaultIntervalDays, string? recommendedTriggerType, string? requiredSkills, string? estimatedDurationHours, string? reviewDate, string? cbmParameter, string? thresholdUnit, string? alarmThreshold, string? tripThreshold, string? pfIntervalDays, string? inspectionIntervalDays, string? startDate, string? justification, string? description);

    [PEMS.Application.Common.PemsOperation("StrategyForm", "Classes", "MaintenanceStrategy.View")] Task<List<StrategyChoice>> ClassesAsync();
    [PEMS.Application.Common.PemsOperation("StrategyForm", "Disciplines", "MaintenanceStrategy.View")] Task<List<StrategyChoice>> DisciplinesAsync();
    [PEMS.Application.Common.PemsOperation("StrategyForm", "Modes", "MaintenanceStrategy.View")] Task<List<StrategyChoice>> ModesAsync(string? classId = null);
    [PEMS.Application.Common.PemsOperation("StrategyForm", "Causes", "MaintenanceStrategy.View")] Task<List<StrategyChoice>> CausesAsync(string? modeId = null);
    [PEMS.Application.Common.PemsOperation("StrategyForm", "Strategies", "MaintenanceStrategy.Approve")] Task<List<StrategyChoice>> StrategiesAsync();
    [PEMS.Application.Common.PemsOperation("StrategyForm", "EquipmentTypes", "MaintenanceStrategy.Approve")] Task<List<StrategyChoice>> EquipmentTypesAsync();
    [PEMS.Application.Common.PemsOperation("StrategyForm", "Plants", "MaintenanceStrategy.Approve")] Task<List<StrategyChoice>> PlantsAsync();
    [PEMS.Application.Common.PemsOperation("StrategyForm", "Areas", "MaintenanceStrategy.Approve")] Task<List<StrategyChoice>> AreasAsync();
    [PEMS.Application.Common.PemsOperation("StrategyForm", "Units", "MaintenanceStrategy.Approve")] Task<List<StrategyChoice>> UnitsAsync();

    /// <summary>The equipment a bulk assignment may take: nothing without a narrowing filter, at most 200 rows; with a strategy each row says whether it would receive it.</summary>
    [PEMS.Application.Common.PemsOperation("StrategyForm", "Candidates", "MaintenanceStrategy.Approve")]
    Task<List<StrategyCandidateRow>> CandidatesAsync(string? strategyId = null, string? classId = null, string? disciplineId = null, string? equipmentType = null, string? plant = null, string? area = null, string? unit = null, string? tagSearch = null, string? tagList = null);

    [PEMS.Application.Common.PemsOperation("StrategyForm", "Assign", "MaintenanceStrategy.Approve")]
    Task<StrategyAnswer> AssignAsync(int strategyId, List<int> equipmentIds, string? culture = null);
}
