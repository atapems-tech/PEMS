using PEMS.Application.DTOs;

namespace PEMS.Application.Interfaces;

public sealed record RcmChoice(string Value, string Label);
public sealed record RcmFunctionalFailureRow(int FunctionalFailureId, string RequirementCode, string Code, string Title, string? FailureDefinition);
public sealed record RcmFmeaItemRow(int FmeaItemId, string AnalysisTitle, int Revision, string AnalysisStatus, string FailureModeName, string? FailureCauseName, string? FailureEffectName, string? FailureConsequenceName,
    int? Severity, int? Occurrence, int? Detectability, int? RiskPriorityNumber, string? StrategyTypeName);
public sealed record RcmDecisionRow(int RcmDecisionId, int RcmRevision, string RcmStatus, string FailureModeName, string DecisionType, string? StrategyTypeName, string? PmTemplateName, string ConsequenceCategory, string Status);

/// <summary>
/// The RCM / FMEA coverage matrix and the class detail as read operations (SCRUM-744). RcmFmeaController checked RCMFMEA.View on both pages, so every read carries it; the writes keep their own permissions (Edit, Approve) in IRcmFmeaOperations.
/// The class pages take the equipment class id; the choice lists are of that class (a form field reads the class from the address with optionsArgs, SCRUM-820).
/// </summary>
public interface IRcmFmeaPagesOperations
{
    [PEMS.Application.Common.PemsOperation("RcmFmeaPage", "Coverage", "RCMFMEA.View")]
    Task<RcmFmeaCoverageMatrixDto> CoverageAsync();

    [PEMS.Application.Common.PemsOperation("RcmFmeaPage", "Readiness", "RCMFMEA.View")]
    Task<EquipmentClassRcmFmeaReadinessDto?> ReadinessAsync(int equipmentClassId);

    [PEMS.Application.Common.PemsOperation("RcmFmeaPage", "FunctionalFailures", "RCMFMEA.View")]
    Task<List<RcmFunctionalFailureRow>> FunctionalFailuresAsync(int equipmentClassId);

    [PEMS.Application.Common.PemsOperation("RcmFmeaPage", "FmeaAnalyses", "RCMFMEA.View")]
    Task<List<FMEAAnalysisDto>> FmeaAnalysesAsync(int equipmentClassId);

    [PEMS.Application.Common.PemsOperation("RcmFmeaPage", "FmeaItems", "RCMFMEA.View")]
    Task<List<RcmFmeaItemRow>> FmeaItemsAsync(int equipmentClassId);

    [PEMS.Application.Common.PemsOperation("RcmFmeaPage", "RcmAnalyses", "RCMFMEA.View")]
    Task<List<RCMAnalysisDto>> RcmAnalysesAsync(int equipmentClassId);

    [PEMS.Application.Common.PemsOperation("RcmFmeaPage", "RcmDecisions", "RCMFMEA.View")]
    Task<List<RcmDecisionRow>> RcmDecisionsAsync(int equipmentClassId);

    [PEMS.Application.Common.PemsOperation("RcmFmeaPage", "FailureModeOptions", "RCMFMEA.View")]
    Task<List<RcmChoice>> FailureModeOptionsAsync(int equipmentClassId);

    [PEMS.Application.Common.PemsOperation("RcmFmeaPage", "FunctionalFailureOptions", "RCMFMEA.View")]
    Task<List<RcmChoice>> FunctionalFailureOptionsAsync(int equipmentClassId);

    [PEMS.Application.Common.PemsOperation("RcmFmeaPage", "FunctionalRequirementOptions", "RCMFMEA.View")]
    Task<List<RcmChoice>> FunctionalRequirementOptionsAsync(int equipmentClassId);

    [PEMS.Application.Common.PemsOperation("RcmFmeaPage", "StrategyTypeOptions", "RCMFMEA.View")]
    Task<List<RcmChoice>> StrategyTypeOptionsAsync();

    [PEMS.Application.Common.PemsOperation("RcmFmeaPage", "PmTemplateOptions", "RCMFMEA.View")]
    Task<List<RcmChoice>> PmTemplateOptionsAsync(int equipmentClassId);

    [PEMS.Application.Common.PemsOperation("RcmFmeaPage", "EquipmentOptions", "RCMFMEA.View")]
    Task<List<RcmChoice>> EquipmentOptionsAsync(int equipmentClassId);

    [PEMS.Application.Common.PemsOperation("RcmFmeaPage", "DraftFmeaOptions", "RCMFMEA.View")]
    Task<List<RcmChoice>> DraftFmeaOptionsAsync(int equipmentClassId);

    [PEMS.Application.Common.PemsOperation("RcmFmeaPage", "ApprovedFmeaOptions", "RCMFMEA.View")]
    Task<List<RcmChoice>> ApprovedFmeaOptionsAsync(int equipmentClassId);

    [PEMS.Application.Common.PemsOperation("RcmFmeaPage", "ApprovedFmeaItemOptions", "RCMFMEA.View")]
    Task<List<RcmChoice>> ApprovedFmeaItemOptionsAsync(int equipmentClassId);

    [PEMS.Application.Common.PemsOperation("RcmFmeaPage", "DraftRcmOptions", "RCMFMEA.View")]
    Task<List<RcmChoice>> DraftRcmOptionsAsync(int equipmentClassId);
}
