using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Persistence.Context;
using PEMS.Persistence.Identity;
using PEMS.WorkflowPlatform.Entities;

namespace PEMS.Persistence.Seed;

/// <summary>
/// SCRUM-471: the pre-procurement workflow. Each procurement demand (replenishment recommendation sent by the warehouse,
/// direct-purchase work-order line, engineering MR issued to procurement) is one task in the procurement role queue of My Work
/// (one stage, Sequential, buyer or procurement manager may act). PreProcurementService completes it when the buyer converts
/// or accepts the item and cancels it when the buyer dismisses or returns it. Roles come from RoleCatalog; nothing is granted here.
/// </summary>
public static class PreProcurementWorkflowSeeder
{
    public const string Module = "PreProcurement";
    public const string Category = "PreProcurement";
    /// <summary>The business object is the procurement demand (its id).</summary>
    public const string BusinessObjectType = "ProcurementDemand";
    public const string StageName = "BuyerReview";

    /// <summary>SCRUM-804: the workflow is the shipped document Workflow/Shipped/pre-procurement.workflow.json (clean-install parity is tested in ShippedWorkflowParityTests).</summary>
    public static Task SeedAsync(IServiceProvider services) => PEMS.Persistence.Workflow.ShippedWorkflowCatalog.ShipAsync(services, "pre-procurement");
}
