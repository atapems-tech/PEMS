using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Persistence.Context;
using PEMS.WorkflowPlatform.Entities;

namespace PEMS.Persistence.Seed;

/// <summary>
/// SCRUM-470: purchase-order approval by the PO authority table. One Parallel stage whose participant is not fixed in the
/// definition: PurchaseOrderApprovalService passes the approver role of the matched authority rule at start (the same
/// mechanism as the VDR / contract discipline reviews). Approve completes it; Reject returns the PO to Draft.
/// No role or amount is hard-coded here; the admin owns the authority table.
/// </summary>
public static class PurchaseOrderApprovalWorkflowSeeder
{
    public const string Module = "PurchaseOrder";
    public const string Category = "PurchaseOrderApproval";
    public const string BusinessObjectType = "PurchaseOrder";
    public const string StageName = "Approval";

    /// <summary>SCRUM-804: the workflow is the shipped document Workflow/Shipped/purchase-order-approval.workflow.json (clean-install parity is tested in ShippedWorkflowParityTests).</summary>
    public static Task SeedAsync(IServiceProvider services) => PEMS.Persistence.Workflow.ShippedWorkflowCatalog.ShipAsync(services, "purchase-order-approval");
}
