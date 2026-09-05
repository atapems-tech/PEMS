using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Persistence.Context;
using PEMS.Persistence.Identity;
using PEMS.WorkflowPlatform.Entities;

namespace PEMS.Persistence.Seed;

/// <summary>
/// ==============================================================================
/// طبق CMMS-10b: زنجیره‌ی تایید Permit - HSE Review → Maintenance Approval → Operations
/// Approval، هرکدام Sequential (یک رکورد با لیست نقش‌های مجاز - رابطه‌ی OR). Reject در هر
/// مرحله زنجیره را با نتیجه‌ی Rejected می‌بندد (طبق خودِ موتور، بدون نیاز به تغییر).
///
/// نقش‌بندی: طبق §16 پلن - هیچ نقش اختصاصی HSE/Operations/Permit Issuer در SystemRoles وجود
/// ندارد؛ از نزدیک‌ترین نقش‌های موجود استفاده شده (QAManager/MaintenanceEngineer/
/// EngineeringManager). "Permit Issuer" عمداً بیرون این زنجیره مانده - همون اکشن Issue که در
/// CMMS-10a با RoleModuleAction ساخته شد.
/// ==============================================================================
/// </summary>
public static class PermitApprovalWorkflowSeeder
{
    public const string Module = "Permit";
    public const string BusinessObjectType = "Permit";
    public const string Category = "PermitApproval";

    public static async Task SeedAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<PEMSDbContext>();

        var alreadyExists = await db.WorkflowDefinitions.AnyAsync(x => x.Module == Module && x.Category == Category);
        if (alreadyExists) return;

        var definition = new WorkflowDefinition
        {
            Name = "تایید Permit to Work",
            Category = Category,
            Module = Module,
            Description = "HSE Review → Maintenance Approval → Operations Approval",
            IsActive = true,
            CreatedDate = DateTime.Now
        };

        var version = new WorkflowVersion
        {
            Definition = definition,
            VersionNumber = 1,
            Status = "Published",
            EffectiveDate = DateTime.Now,
            PublishedDate = DateTime.Now
        };

        var hseStage = new WorkflowStage
        {
            Version = version,
            Name = "HSEReview",
            StageOrder = 1,
            StageType = "Sequential",
            ResponsibleRole = string.Join(",", SystemRoles.SystemAdmin, SystemRoles.QAManager),
            ApprovalStrategy = "AllApprovalsRequired",
            IsMandatory = true
        };

        var maintenanceStage = new WorkflowStage
        {
            Version = version,
            Name = "MaintenanceApproval",
            StageOrder = 2,
            StageType = "Sequential",
            ResponsibleRole = string.Join(",", SystemRoles.SystemAdmin, SystemRoles.MaintenanceEngineer),
            ApprovalStrategy = "AllApprovalsRequired",
            IsMandatory = true
        };

        var operationsStage = new WorkflowStage
        {
            Version = version,
            Name = "OperationsApproval",
            StageOrder = 3,
            StageType = "Sequential",
            ResponsibleRole = string.Join(",", SystemRoles.SystemAdmin, SystemRoles.EngineeringManager),
            ApprovalStrategy = "AllApprovalsRequired",
            IsMandatory = true
        };

        version.Stages.Add(hseStage);
        version.Stages.Add(maintenanceStage);
        version.Stages.Add(operationsStage);

        version.Transitions.Add(new WorkflowTransition
        {
            Version = version, FromStage = null, ToStage = hseStage, ActionType = "Start", Priority = 0
        });

        version.Transitions.Add(new WorkflowTransition
        {
            Version = version, FromStage = hseStage, ToStage = maintenanceStage, ActionType = "Approve", Priority = 0
        });
        version.Transitions.Add(new WorkflowTransition
        {
            Version = version, FromStage = hseStage, ToStage = null, ActionType = "Reject", Priority = 0
        });

        version.Transitions.Add(new WorkflowTransition
        {
            Version = version, FromStage = maintenanceStage, ToStage = operationsStage, ActionType = "Approve", Priority = 0
        });
        version.Transitions.Add(new WorkflowTransition
        {
            Version = version, FromStage = maintenanceStage, ToStage = null, ActionType = "Reject", Priority = 0
        });

        version.Transitions.Add(new WorkflowTransition
        {
            Version = version, FromStage = operationsStage, ToStage = null, ActionType = "Approve", Priority = 0
        });
        version.Transitions.Add(new WorkflowTransition
        {
            Version = version, FromStage = operationsStage, ToStage = null, ActionType = "Reject", Priority = 0
        });

        definition.Versions.Add(version);

        db.WorkflowDefinitions.Add(definition);
        await db.SaveChangesAsync();
    }
}
