namespace PEMS.Application.Interfaces;

/// <summary>One row of the downloadable template: the properties are the column headers of the workbook the importer reads.</summary>
public sealed record StrategyTemplateRow(string EquipmentClassCode, string StrategyType, string FailureModeCode, string ConsequenceCategory, string IsHiddenFailure, string Justification, string Frequency, string DefaultIntervalDays,
    string TaskType, string OwnerDisciplineCode, string CbmParameter, string ThresholdUnit, string AlarmThreshold, string TripThreshold, string PfIntervalDays, string InspectionIntervalDays, string Source, string Title,
    string FunctionalFailure, string FailureEffect, string RequiredSkills, string EstimatedDurationHours, string ReviewDate);

public sealed record AssignmentTemplateRow(string StrategyId, string EquipmentClassCode, string StrategyType, string FailureModeCode, string Tag);

/// <summary>
/// The strategy and assignment workbook import as operations (SCRUM-752). Each import takes the file and a commit flag: without it the workbook is only validated and the report of the rows comes back as the message
/// (nothing is written); with it the valid rows are written (the strategies as drafts). Strategies need MaintenanceStrategy.Create, assignments MaintenanceStrategy.Approve, as StrategyImportController checked.
/// </summary>
public interface IStrategyImportOperations
{
    [PEMS.Application.Common.PemsOperation("StrategyImport", "ImportStrategies", "MaintenanceStrategy.Create")]
    Task<(bool Success, string? Error)> ImportStrategiesAsync(byte[] file, bool commit);

    [PEMS.Application.Common.PemsOperation("StrategyImport", "ImportAssignments", "MaintenanceStrategy.Approve")]
    Task<(bool Success, string? Error)> ImportAssignmentsAsync(byte[] file, bool commit);

    [PEMS.Application.Common.PemsOperation("StrategyImport", "StrategyTemplate", "MaintenanceStrategy.Create")]
    Task<List<StrategyTemplateRow>> StrategyTemplateAsync();

    [PEMS.Application.Common.PemsOperation("StrategyImport", "AssignmentTemplate", "MaintenanceStrategy.Approve")]
    Task<List<AssignmentTemplateRow>> AssignmentTemplateAsync();
}
