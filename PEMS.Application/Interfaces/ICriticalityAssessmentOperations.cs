namespace PEMS.Application.Interfaces;

public sealed record CriticalityAssessmentQueueRow(int Id, string EquipmentTag, string EquipmentClass, string FrameworkVersion, string Status, string ProposedClass, string PreparedBy, string ApprovedBy, DateTime CreatedDate);

public sealed record CriticalityAssessmentHeadRow(int Id, string EquipmentTag, string EquipmentClass, string FrameworkVersion, string Status, DateTime? AssessmentDate, string PreparedBy, string ReviewedBy, string ApprovedBy,
    DateTime? ApprovedAt, string ReasonForChange, string SafetyScore, string ProductionScore, string EnvironmentScore, string CalculatedMaxScore, string ProposedClass, string Rationale, int EvidenceCount, int PriorCount);

public sealed record CriticalityAssessmentEvidenceRow(int Id, string SourceType, string SourceIdentifier, string Reference, string Description, string CreatedBy, DateTime CreatedDate);

public sealed record CriticalityAssessmentDashboardRow(int TotalActiveEquipment, int Assessed, int Unassessed, int Approved, int UnderReview, int Draft, int Superseded, int PublishedA, int PublishedB, int PublishedC, int PublishedOther, string FrameworkVersion);

/// <summary>
/// The engineering criticality assessment as operations (SCRUM-678, 701): the queue, one assessment, its evidence and history, the dashboard counts (View), the draft creation (Create), the scores, evidence and submit (Edit), the approve and the
/// reject back to draft (Approve). The permissions are the ones the controller checked on the CriticalityAssessment module; the current user is the actor.
/// </summary>
public interface ICriticalityAssessmentOperations
{
    [PEMS.Application.Common.PemsOperation("CriticalityAssessment", "Queue", "CriticalityAssessment.View")]
    Task<List<CriticalityAssessmentQueueRow>> QueueAsync(string? status = null);

    [PEMS.Application.Common.PemsOperation("CriticalityAssessment", "Head", "CriticalityAssessment.View")]
    Task<List<CriticalityAssessmentHeadRow>> HeadAsync(int id);

    [PEMS.Application.Common.PemsOperation("CriticalityAssessment", "Evidence", "CriticalityAssessment.View")]
    Task<List<CriticalityAssessmentEvidenceRow>> EvidenceAsync(int id);

    [PEMS.Application.Common.PemsOperation("CriticalityAssessment", "History", "CriticalityAssessment.View")]
    Task<List<CriticalityAssessmentQueueRow>> HistoryAsync(int id);

    [PEMS.Application.Common.PemsOperation("CriticalityAssessment", "Dashboard", "CriticalityAssessment.View")]
    Task<List<CriticalityAssessmentDashboardRow>> DashboardAsync();

    [PEMS.Application.Common.PemsOperation("CriticalityAssessment", "CreateDraft", "CriticalityAssessment.Create")]
    Task<int> CreateDraftAsync(int equipmentId, string? rationale = null);

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
