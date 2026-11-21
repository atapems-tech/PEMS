namespace PEMS.Application.Interfaces;

/// <summary>The workflow commands of CriticalityAssessmentController as operations (SCRUM-701): set the dimension scores, add evidence, submit for review (Edit), approve and publish, reject back to draft (Approve). Permissions are the ones the controller checked on the CriticalityAssessment module; the current user is the actor.</summary>
public interface ICriticalityAssessmentOperations
{
    [PEMS.Application.Common.PemsOperation("CriticalityAssessment", "SetScores", "CriticalityAssessment.Edit")]
    Task<(bool Success, string? Error)> SetScoresAsync(int id, int? safetyScore = null, int? productionScore = null, int? environmentScore = null, string? rationale = null);

    [PEMS.Application.Common.PemsOperation("CriticalityAssessment", "AddEvidence", "CriticalityAssessment.Edit")]
    Task<(bool Success, string? Error)> AddEvidenceAsync(int id, string sourceType, string description, string? sourceIdentifier = null, string? reference = null);

    [PEMS.Application.Common.PemsOperation("CriticalityAssessment", "SubmitForReview", "CriticalityAssessment.Edit")]
    Task<(bool Success, string? Error)> SubmitForReviewAsync(int id);

    [PEMS.Application.Common.PemsOperation("CriticalityAssessment", "Approve", "CriticalityAssessment.Approve")]
    Task<(bool Success, string? Error)> ApproveAsync(int id);

    [PEMS.Application.Common.PemsOperation("CriticalityAssessment", "Reject", "CriticalityAssessment.Approve")]
    Task<(bool Success, string? Error)> RejectAsync(int id);
}
