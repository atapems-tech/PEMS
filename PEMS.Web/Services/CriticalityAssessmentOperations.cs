using PEMS.Application.Interfaces;
using PEMS.Web.Security;

namespace PEMS.Web.Services;

/// <summary>SCRUM-701: the workflow commands of CriticalityAssessmentController over ICriticalityAssessmentService (same service, same actor).</summary>
public sealed class CriticalityAssessmentOperations(ICriticalityAssessmentService service, ICurrentUserService currentUser) : ICriticalityAssessmentOperations
{
    public Task<(bool Success, string? Error)> SetScoresAsync(int id, int? safetyScore = null, int? productionScore = null, int? environmentScore = null, string? rationale = null)
        => service.SetDimensionScoresAsync(id, safetyScore, productionScore, environmentScore, rationale);

    public Task<(bool Success, string? Error)> AddEvidenceAsync(int id, string sourceType, string description, string? sourceIdentifier = null, string? reference = null)
        => service.AddEvidenceAsync(id, sourceType, sourceIdentifier, reference, description, currentUser.UserName);

    public Task<(bool Success, string? Error)> SubmitForReviewAsync(int id) => service.SubmitForReviewAsync(id, currentUser.UserName);
    public Task<(bool Success, string? Error)> ApproveAsync(int id) => service.ApproveAsync(id, currentUser.UserName);
    public Task<(bool Success, string? Error)> RejectAsync(int id) => service.RejectAsync(id, currentUser.UserName);
}
