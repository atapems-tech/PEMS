using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Persistence.Context;
using PEMS.Persistence.Identity;
using PEMS.WorkflowPlatform.Entities;

namespace PEMS.Persistence.Seed;

/// <summary>
/// SCRUM-472: the warehouse intake workflow. When a planner requests the stock materials of a work order, one task lands in
/// the warehouse role queue of My Work (one stage, Sequential, any warehouse role may act). The storekeeper sources the
/// lines from a warehouse they pick (WarehouseIntakeService creates the linked store requisition); when every stock line is
/// sourced the service completes the task. Cancel closes it when the work order no longer needs the materials.
/// Roles are the existing warehouse roles from RoleCatalog; nothing is granted here.
/// </summary>
public static class WarehouseIntakeWorkflowSeeder
{
    public const string Module = "WarehouseIntake";
    public const string Category = "WarehouseIntake";
    /// <summary>The business object is the work order (its id); the type keeps it apart from the work-order lifecycle instance.</summary>
    public const string BusinessObjectType = "WorkOrderMaterialIntake";
    public const string StageName = "WarehouseIntake";

    /// <summary>SCRUM-804: the workflow is the shipped document Workflow/Shipped/warehouse-intake.workflow.json (clean-install parity is tested in ShippedWorkflowParityTests).</summary>
    public static Task SeedAsync(IServiceProvider services) => PEMS.Persistence.Workflow.ShippedWorkflowCatalog.ShipAsync(services, "warehouse-intake");
}
