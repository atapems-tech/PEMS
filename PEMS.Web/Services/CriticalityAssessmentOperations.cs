using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Web.Security;

namespace PEMS.Web.Services;

/// <summary>SCRUM-678, 701: the engineering criticality assessment over ICriticalityAssessmentService (same service, same actor).</summary>
public sealed class CriticalityAssessmentOperations(ICriticalityAssessmentService service, ICurrentUserService currentUser) : ICriticalityAssessmentOperations
{
    private const int RowLimit = 5000;

    private static CriticalityAssessmentQueueRow Row(CriticalityAssessmentListDto a)
        => new(a.CriticalityAssessmentId, a.EquipmentTagNumber, a.EquipmentClassName ?? "", a.FrameworkVersion, a.Status, a.ProposedCriticalityClass ?? "", a.PreparedBy ?? "", a.ApprovedBy ?? "", a.CreatedDate);

    private static string Score(int? value) => value?.ToString() ?? "";

    public async Task<List<CriticalityAssessmentQueueRow>> QueueAsync(string? status = null)
        => (await service.GetQueueAsync(string.IsNullOrWhiteSpace(status) ? null : status, null, 1, RowLimit)).Items.Select(Row).ToList();

    public async Task<List<CriticalityAssessmentHeadRow>> HeadAsync(int id)
        => await service.GetByIdAsync(id) is { } a
            ? [new(a.CriticalityAssessmentId, a.EquipmentTagNumber, a.EquipmentClassName ?? "", a.FrameworkVersion, a.Status, a.AssessmentDate, a.PreparedBy ?? "", a.ReviewedBy ?? "", a.ApprovedBy ?? "", a.ApprovedAt,
                a.ReasonForChange ?? "", Score(a.SafetyScore), Score(a.ProductionScore), Score(a.EnvironmentScore), Score(a.CalculatedMaxScore), a.ProposedCriticalityClass ?? "", a.Rationale ?? "", a.Evidence.Count, a.PriorAssessments.Count)]
            : [];

    public async Task<List<CriticalityAssessmentEvidenceRow>> EvidenceAsync(int id)
        => (await service.GetByIdAsync(id))?.Evidence
            .Select(e => new CriticalityAssessmentEvidenceRow(e.CriticalityAssessmentEvidenceId, e.SourceType, e.SourceIdentifier ?? "", e.Reference ?? "", e.Description, e.CreatedBy ?? "", e.CreatedDate)).ToList() ?? [];

    public async Task<List<CriticalityAssessmentQueueRow>> HistoryAsync(int id)
        => (await service.GetByIdAsync(id))?.PriorAssessments.Select(Row).ToList() ?? [];

    public async Task<List<CriticalityAssessmentDashboardRow>> DashboardAsync()
    {
        var d = await service.GetDashboardAsync();
        return [new(d.TotalActiveEquipment, d.Assessed, d.Unassessed, d.Approved, d.UnderReview, d.Draft, d.Superseded, d.PublishedCriticalityA, d.PublishedCriticalityB, d.PublishedCriticalityC, d.PublishedCriticalityOther, d.FrameworkVersion)];
    }

    /// <summary>The id of the new draft (the action opens it); a refusal of the service is shown as it is.</summary>
    public async Task<int> CreateDraftAsync(int equipmentId, string? rationale = null)
    {
        var (success, error, id) = await service.CreateDraftAsync(equipmentId, currentUser.UserName, rationale);
        if (!success) throw new InvalidOperationException(error ?? "The assessment could not be created.");
        return id ?? 0;
    }

    public Task<(bool Success, string? Error)> SetScoresAsync(int id, int? safetyScore = null, int? productionScore = null, int? environmentScore = null, string? rationale = null)
        => service.SetDimensionScoresAsync(id, safetyScore, productionScore, environmentScore, rationale);

    public Task<(bool Success, string? Error)> AddEvidenceAsync(int id, string sourceType, string description, string? sourceIdentifier = null, string? reference = null)
        => service.AddEvidenceAsync(id, sourceType, sourceIdentifier, reference, description, currentUser.UserName);

    public Task<(bool Success, string? Error)> SubmitForReviewAsync(int id) => service.SubmitForReviewAsync(id, currentUser.UserName);
    public Task<(bool Success, string? Error)> ApproveAsync(int id) => service.ApproveAsync(id, currentUser.UserName);
    public Task<(bool Success, string? Error)> RejectAsync(int id) => service.RejectAsync(id, currentUser.UserName);
}
