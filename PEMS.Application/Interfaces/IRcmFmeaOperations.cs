namespace PEMS.Application.Interfaces;

/// <summary>
/// The write commands of RcmFmeaController as operations (SCRUM-744): functional requirements and failures, the FMEA draft, its items and review steps, the RCM draft, decisions and review steps, and the
/// application of an approved decision to an equipment. Permissions as the controller checked them on the RCMFMEA module (Edit to prepare, Approve to approve, reject and apply).
/// </summary>
public interface IRcmFmeaOperations
{
    [PEMS.Application.Common.PemsOperation("RcmFmea", "CreateFunctionalRequirement", "RCMFMEA.Edit")]
    Task<(bool Success, string? Error)> CreateFunctionalRequirementAsync(int equipmentClassId, string code, string title, string? description = null, string? requiredFunction = null);

    [PEMS.Application.Common.PemsOperation("RcmFmea", "CreateFunctionalFailure", "RCMFMEA.Edit")]
    Task<(bool Success, string? Error)> CreateFunctionalFailureAsync(int functionalRequirementId, string code, string title, string? failureDefinition = null);

    [PEMS.Application.Common.PemsOperation("RcmFmea", "CreateFmeaDraft", "RCMFMEA.Edit")]
    Task<(bool Success, string? Error)> CreateFmeaDraftAsync(int equipmentClassId, string title, string? description = null);

    [PEMS.Application.Common.PemsOperation("RcmFmea", "AddFmeaItem", "RCMFMEA.Edit")]
    Task<(bool Success, string? Error)> AddFmeaItemAsync(int fmeaAnalysisId, int failureModeId, int? functionalFailureId = null, string? detectionMethod = null, string? existingControl = null,
        string? recommendedAction = null, int? severity = null, int? occurrence = null, int? detectability = null, int? maintenanceStrategyTypeId = null, int? standardReferenceId = null);

    [PEMS.Application.Common.PemsOperation("RcmFmea", "SubmitFmea", "RCMFMEA.Edit")]
    Task<(bool Success, string? Error)> SubmitFmeaAsync(int fmeaAnalysisId);

    [PEMS.Application.Common.PemsOperation("RcmFmea", "ApproveFmea", "RCMFMEA.Approve")]
    Task<(bool Success, string? Error)> ApproveFmeaAsync(int fmeaAnalysisId);

    [PEMS.Application.Common.PemsOperation("RcmFmea", "RejectFmea", "RCMFMEA.Approve")]
    Task<(bool Success, string? Error)> RejectFmeaAsync(int fmeaAnalysisId);

    [PEMS.Application.Common.PemsOperation("RcmFmea", "CreateRcmDraft", "RCMFMEA.Edit")]
    Task<(bool Success, string? Error)> CreateRcmDraftAsync(int equipmentClassId, int fmeaAnalysisId);

    [PEMS.Application.Common.PemsOperation("RcmFmea", "AddRcmDecision", "RCMFMEA.Edit")]
    Task<(bool Success, string? Error)> AddRcmDecisionAsync(int rcmAnalysisId, int fmeaItemId, string decisionType, string consequenceCategory, int? maintenanceStrategyTypeId = null,
        int? recommendedPMTemplateId = null, string? rationale = null);

    [PEMS.Application.Common.PemsOperation("RcmFmea", "ApproveRcmDecision", "RCMFMEA.Approve")]
    Task<(bool Success, string? Error)> ApproveRcmDecisionAsync(int rcmDecisionId);

    [PEMS.Application.Common.PemsOperation("RcmFmea", "SubmitRcm", "RCMFMEA.Edit")]
    Task<(bool Success, string? Error)> SubmitRcmAsync(int rcmAnalysisId);

    [PEMS.Application.Common.PemsOperation("RcmFmea", "ApproveRcm", "RCMFMEA.Approve")]
    Task<(bool Success, string? Error)> ApproveRcmAsync(int rcmAnalysisId);

    [PEMS.Application.Common.PemsOperation("RcmFmea", "RejectRcm", "RCMFMEA.Approve")]
    Task<(bool Success, string? Error)> RejectRcmAsync(int rcmAnalysisId);

    [PEMS.Application.Common.PemsOperation("RcmFmea", "ApplyRcmDecision", "RCMFMEA.Approve")]
    Task<(bool Success, string? Error)> ApplyRcmDecisionAsync(int rcmDecisionId, int equipmentId);
}
