namespace PEMS.Application.Interfaces;

/// <summary>The one row of the tender page: the head and what can be done next (the flags drive the row actions).</summary>
public sealed record TenderHeadRow(int Id, int TenderId, string TenderNumber, string Title, string Package, int ProcurementPackageId, string Status, DateTime? ResponseDeadline, bool RequiresVendorQualification, string AwardedTo, int? ContractId,
    bool CanOpen, bool CanClose, bool CanCreateContract, bool CanSendInvitations);

/// <summary>The negotiation waiting for approval on a bid (the approve form is loaded with it).</summary>
public sealed record TenderPendingNegotiationRow(int Id, int BidId, int BidNegotiationId, string Vendor, decimal? NegotiatedValue);
public sealed record TenderVendorRow(int Id, int TenderId, string Vendor, bool QualificationConfirmed);
public sealed record TenderInvitationRow(int Id, string Vendor, string ToAddress, string Status, int Attempts, string QueuedBy, DateTime? QueuedAt, DateTime? SentAt, string LastError, bool CanResend);
public sealed record TenderBidRow(int Id, int TenderId, string Vendor, string BidNumber, string Status, decimal? OriginalValue, decimal? NegotiatedValue, string Currency, string Technical, string Commercial,
    int? PendingNegotiationId, bool CanEvaluate, bool CanNegotiate, bool CanApproveNegotiation, bool CanSelectWinner);
public sealed record TenderClarificationRow(int Id, int TenderId, int PackageId, string Type, string Subject, string Vendor, string Body, string Status, string Responses, bool CanRespond);

/// <summary>
/// SCRUM-817: the tender details page (the former TenderController.Details) as read operations: the head with the next possible steps, the invited vendors, the invitation log, the bids with their evaluation and
/// negotiation state, and the clarifications. The tender id is the parameter of every read (the filter of the child grid). The commands are actions (the approval commands carry the guards of
/// <see cref="ITenderOperations"/>).
/// </summary>
public interface ITenderDetailsOperations
{
    [PEMS.Application.Common.PemsOperation("TenderDetails", "Head", "Tender.View")]
    Task<List<TenderHeadRow>> HeadAsync(int id);

    [PEMS.Application.Common.PemsOperation("TenderDetails", "Vendors", "Tender.View")]
    Task<List<TenderVendorRow>> VendorsAsync(int id);

    [PEMS.Application.Common.PemsOperation("TenderDetails", "InvitationLog", "Tender.View")]
    Task<List<TenderInvitationRow>> InvitationLogAsync(int id);

    [PEMS.Application.Common.PemsOperation("TenderDetails", "Bids", "Tender.View")]
    Task<List<TenderBidRow>> BidsAsync(int id);

    [PEMS.Application.Common.PemsOperation("TenderDetails", "Clarifications", "Tender.View")]
    Task<List<TenderClarificationRow>> ClarificationsAsync(int id);

    /// <summary>The invited vendors of a tender as choices (the vendor of a bid or of a clarification); the tender id comes from the form.</summary>
    [PEMS.Application.Common.PemsOperation("TenderDetails", "InvitedVendorChoices", "Tender.View")]
    Task<List<ReportChoiceRow>> InvitedVendorChoicesAsync(int? tenderId = null);

    [PEMS.Application.Common.PemsOperation("TenderDetails", "VendorChoices", "Tender.Edit")]
    Task<List<ReportChoiceRow>> VendorChoicesAsync();

    [PEMS.Application.Common.PemsOperation("TenderDetails", "PendingNegotiation", "Tender.View")]
    Task<List<TenderPendingNegotiationRow>> PendingNegotiationAsync(int bidId);
}
