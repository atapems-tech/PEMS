namespace PEMS.Application.Interfaces;

/// <summary>
/// طبق ممیزی Package Procurement Contract Finalization (§C/§D، Phase 3): چرخه‌ی کامل
/// Tender→Bid→Evaluation→Negotiation→Winner Selection→Contract Draft. ProcurementPackage همچنان
/// سرصفحه‌ی بسته می‌ماند - این سرویس یک معماری موازی نیست، فقط مرحله‌ی مناقصه‌ی رسمی را که پیش
/// از این اصلاً وجود نداشت اضافه می‌کند.
/// </summary>
public interface ITenderService
{
    Task<int> CreateAsync(int procurementPackageId, string title, DateTime? issueDate, DateTime? responseDeadline,
        bool requiresVendorQualification, string? technicalRequirements, string? commercialRequirements,
        string? inspectionRequirements, string? contractualRequirements);

    Task<(bool Success, string? Error)> InviteVendorAsync(int tenderId, int vendorId);

    Task<(bool Success, string? Error)> OpenForBiddingAsync(int tenderId);

    Task<(bool Success, string? Error)> CloseBiddingAsync(int tenderId);

    Task<(bool Success, string? Error, int? BidId)> SubmitBidAsync(
        int tenderId, int vendorId, decimal originalValue, string currency,
        string? technicalSubmissionReference, string? commercialSubmissionReference);

    Task<(bool Success, string? Error)> RecordBidEvaluationAsync(
        int bidId, string evaluationType, int? evaluatorEmployeeId, string? disciplineRole, string decision, string? comment);

    Task<(bool Success, string? Error, int? NegotiationId)> RequestNegotiationAsync(
        int bidId, decimal negotiatedValue, string? participants, string? commercialChanges, string? technicalChanges,
        string? contractualChanges, string? justification, string? supportingDocumentReference, int? actorEmployeeId);

    Task<(bool Success, string? Error)> ApproveNegotiationAsync(int bidNegotiationId, int? actorEmployeeId);

    Task<(bool Success, string? Error)> SelectWinnerAsync(int tenderId, int bidId, int? actorEmployeeId);

    Task<(bool Success, string? Error, int? ContractId)> CreateContractFromWinningBidAsync(
        int tenderId, string contractNo, DateTime? startDate, DateTime? endDate);
}
