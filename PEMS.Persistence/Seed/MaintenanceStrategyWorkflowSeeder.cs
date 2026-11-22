using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Persistence.Context;
using PEMS.Persistence.Identity;
using PEMS.WorkflowPlatform.Entities;

namespace PEMS.Persistence.Seed;

/// <summary>
/// گردش‌کار تایید استراتژی نگهداری (RCM): Draft → InReview → Approved. عیناً هم‌الگو با
/// IrnDispositionWorkflowSeeder روی همان موتور IWorkflowRuntime (بدون تغییر در PEMS.WorkflowPlatform).
/// تفکیک وظایف (نویسنده ≠ تاییدکننده) در MaintenanceStrategyCommandService اعمال می‌شود؛ این‌جا فقط
/// نقش‌های مجاز مرحله‌ی بازبینی تعریف می‌شود. Idempotent: اگر تعریف وجود دارد کاری نمی‌کند.
/// </summary>
public static class MaintenanceStrategyWorkflowSeeder
{
    public const string Module = "CMMS";
    public const string Category = "StrategyApproval";
    public const string BusinessObjectType = "MaintenanceStrategy";

    /// <summary>SCRUM-804: the workflow is the shipped document Workflow/Shipped/maintenance-strategy.workflow.json (clean-install parity is tested in ShippedWorkflowParityTests).</summary>
    public static Task SeedAsync(IServiceProvider services) => PEMS.Persistence.Workflow.ShippedWorkflowCatalog.ShipAsync(services, "maintenance-strategy");
}
