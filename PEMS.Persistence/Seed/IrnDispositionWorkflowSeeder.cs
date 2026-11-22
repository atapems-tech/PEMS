using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Persistence.Context;
using PEMS.Persistence.Identity;
using PEMS.WorkflowPlatform.Entities;

namespace PEMS.Persistence.Seed;

/// <summary>
/// ==============================================================================
/// طبق ممیزی Material Management (Gap B) + Step 3: گردش‌کار تایید Disposition دریافت مادی
/// (IRN) - عیناً هم‌الگو با NcrApprovalWorkflowSeeder (همون موتور IWorkflowRuntime، بدون هیچ
/// تغییری در PEMS.WorkflowPlatform). اصل «Received ≠ Available»: بازرس مقدار
/// Accepted/Rejected/Hold را روی خطوط ثبت می‌کند (IrnReceiptService.RecordDispositionAsync)،
/// ولی این‌جا (تایید مستقل QA) باید تصویب شود پیش از این‌که مقدار Accepted واقعاً به
/// InventoryTransaction/MaterialStock تبدیل شود - دقیقاً هم‌سو با قاعده‌ی ISO 9001 بند ۸.۷ که
/// NCR بحرانی از قبل در این کدبیس پیاده کرده. Hold یک فعل جداگانه‌ی گردش‌کار نیست - در سطح
/// داده (IrnReceiptLine.HoldQuantity) ثبت می‌شود؛ همین دو انتقال Approve/Reject (عیناً مثل NCR)
/// برای تصمیم نهایی روی کل Disposition کافی است.
/// ==============================================================================
/// </summary>
public static class IrnDispositionWorkflowSeeder
{
    public const string Module = "IRN";
    public const string Category = "DispositionApproval";
    public const string BusinessObjectType = "IrnReceiptDisposition";

    /// <summary>SCRUM-804: the workflow is the shipped document Workflow/Shipped/irn-disposition.workflow.json (clean-install parity is tested in ShippedWorkflowParityTests).</summary>
    public static Task SeedAsync(IServiceProvider services) => PEMS.Persistence.Workflow.ShippedWorkflowCatalog.ShipAsync(services, "irn-disposition");
}
