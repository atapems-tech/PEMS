using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Persistence.Context;
using PEMS.Persistence.Identity;
using PEMS.WorkflowPlatform.Entities;

namespace PEMS.Persistence.Seed;

/// <summary>
/// SCRUM-473 (D7, owner 2026-10-30): Hold resolution of a goods receipt. When QA approves an IRN disposition that leaves Hold
/// quantity, one task per IRN lands in the Warehouse Manager queue of My Work. The manager splits each Hold into accepted (posted
/// to stock) and rejected (the buyer's replace / close-short decision, D6b); PurchaseOrderReceivingService completes the task when
/// no Hold is left. Roles come from RoleCatalog; nothing is granted here.
/// </summary>
public static class IrnHoldResolutionWorkflowSeeder
{
    public const string Module = "IRN";
    public const string Category = "HoldResolution";
    /// <summary>The business object is the IRN (its id); the type keeps it apart from the disposition approval instance.</summary>
    public const string BusinessObjectType = "IrnHold";
    public const string StageName = "HoldResolution";

    /// <summary>SCRUM-804: the workflow is the shipped document Workflow/Shipped/irn-hold-resolution.workflow.json (clean-install parity is tested in ShippedWorkflowParityTests).</summary>
    public static Task SeedAsync(IServiceProvider services) => PEMS.Persistence.Workflow.ShippedWorkflowCatalog.ShipAsync(services, "irn-hold-resolution");
}
