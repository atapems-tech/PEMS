namespace PEMS.Application.Interfaces;

/// <summary>SCRUM-470: <c>Procurement__*</c> settings (env files).</summary>
public class ProcurementOptions
{
    /// <summary>True = every purchase-order line must be a catalogued part (no free-text lines).</summary>
    public bool CatalogueOnlyLines { get; set; }
}

/// <summary>Who acts on a purchase order (identity from the server-side principal).</summary>
public sealed record PoActor(int? EmployeeId, IReadOnlyList<string> Roles, string? UserName);

/// <summary>Outcome of a PO command. <c>Error</c> is a resource key (<c>Po_Err_*</c>) or a message passed through from the PO service.</summary>
public sealed record PoResult(bool Success, string? Error, int? Id = null)
{
    public static PoResult Fail(string error) => new(false, error);
}

/// <summary>Editable values of a PO line in Draft.</summary>
public sealed record PoLineInput(decimal Quantity, decimal UnitPrice, DateTime? RequiredDate, bool IsStockLine, int? CommodityGroupId, string? FreeTextReason);

public sealed record PoLineView(
    int PurchaseOrderLineId, int LineNo, string Description, string? PartNumber, bool IsCatalogued, decimal Quantity, string? Unit,
    decimal UnitPrice, decimal LineAmount, DateTime? RequiredDate, bool IsStockLine, int? CommodityGroupId, string? CommodityGroup,
    string? FreeTextReason, int? MaterialRequisitionItemId, int? TenderId, int? BidId);

public sealed record PoApprovalView(
    int PoId, string PoNumber, string Status, string? Currency, decimal Total, int? MaterialRequisitionId, string? MaterialRequisitionNumber,
    string? ApproverRole, int? AuthorityRuleLevel, DateTime? SubmittedAt, string? SubmittedBy, DateTime? ApprovedAt, string? ApprovedBy,
    string? RejectionReason, string? CreatedBy, IReadOnlyList<PoLineView> Lines)
{
    /// <summary>Submitted but no authority rule matched: the PO waits for the admin to add a rule.</summary>
    public bool ResponsibleNotSet => Status == "Submitted" && string.IsNullOrEmpty(ApproverRole);
}

public sealed record PoAuthorityRuleInput(int? Id, int Level, decimal MinAmount, decimal? MaxAmount, string Currency, string ApproverRole, string? Description, bool IsActive);

public sealed record PoAuthorityRuleView(int Id, int Level, decimal MinAmount, decimal? MaxAmount, string Currency, string ApproverRole, string? Description, bool IsActive);

/// <summary>
/// SCRUM-470: purchase-order lines from the pre-procurement path (MR items) and approval by the PO authority table
/// (owner decisions 2026-10-29). Draft → Submitted → Approved; Issue (Approved → Issued) stays in IPurchaseOrderService.
/// </summary>
public interface IPurchaseOrderApprovalService
{
    /// <summary>MRs whose items may become PO lines (issued to procurement, or raised from the procurement cartable).</summary>
    Task<IReadOnlyList<(int Id, string Number, string Description, int ProjectId)>> GetRequisitionsForPoAsync();

    Task<IReadOnlyList<(int Id, string Code, string Description)>> GetCommodityGroupsAsync();

    /// <summary>Creates a Draft PO for the MR's project and adds every MR item that is not already on a live PO.</summary>
    Task<PoResult> CreateFromRequisitionAsync(int mrId, int vendorId, string currency, string? poNumber, PoActor actor);

    /// <summary>Adds the MR's free items to a Draft PO of the same project.</summary>
    Task<PoResult> AddLinesFromRequisitionAsync(int poId, int mrId, PoActor actor);

    Task<PoResult> UpdateLineAsync(int lineId, PoLineInput input, PoActor actor);

    Task<PoResult> RemoveLineAsync(int lineId, PoActor actor);

    Task<PoApprovalView?> GetAsync(int poId);

    /// <summary>Checks the lines, totals the value and routes the PO to the role of the matching authority rule (or leaves it "responsible not set").</summary>
    Task<PoResult> SubmitAsync(int poId, PoActor actor);

    /// <summary>Retries the routing of a submitted PO whose responsible was not set (after the admin added a rule).</summary>
    Task<PoResult> RouteAsync(int poId, PoActor actor);

    /// <summary>Approval by a holder of the routed role who neither created nor submitted the PO.</summary>
    Task<PoResult> ApproveAsync(int poId, PoActor actor);

    /// <summary>Back to Draft with a reason.</summary>
    Task<PoResult> RejectAsync(int poId, string reason, PoActor actor);

    Task<IReadOnlyList<PoAuthorityRuleView>> GetRulesAsync();

    Task<PoResult> SaveRuleAsync(PoAuthorityRuleInput input, PoActor actor);
}
