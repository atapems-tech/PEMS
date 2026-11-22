using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Persistence.Context;
using PEMS.Persistence.Identity;
using PEMS.WorkflowPlatform.Entities;

namespace PEMS.Persistence.Seed;

/// <summary>
/// ==============================================================================
/// گردش‌کار تایید NCR بحرانی - طبق ISO 9001 بند ۸.۷: عدم انطباق‌های بحرانی نمی‌توانند فقط
/// با تصمیم همان کسی که Disposition را ثبت کرده بسته شوند - نیاز به تایید مستقل مدیر QA
/// دارند. برای NCR های Minor/Major این مرحله اجرا نمی‌شود (سربار غیرضروری).
/// ==============================================================================
/// </summary>
public static class NcrApprovalWorkflowSeeder
{
    public const string Module = "NCR";
    public const string Category = "CriticalApproval";

    /// <summary>SCRUM-804: the workflow is the shipped document Workflow/Shipped/ncr-critical-approval.workflow.json (clean-install parity is tested in ShippedWorkflowParityTests).</summary>
    public static Task SeedAsync(IServiceProvider services) => PEMS.Persistence.Workflow.ShippedWorkflowCatalog.ShipAsync(services, "ncr-critical-approval");
}
