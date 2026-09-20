using Microsoft.EntityFrameworkCore;
using PEMS.WorkflowPlatform.Entities;

namespace PEMS.WorkflowPlatform.Interfaces;

/// <summary>
/// ==============================================================================
/// چرا این اینترفیس لازم است (نه استفاده‌ی مستقیم از PEMSDbContext):
/// اگر WorkflowRuntimeService مستقیماً به PEMSDbContext (در PEMS.Persistence) وابسته بود،
/// آن‌وقت PEMS.WorkflowPlatform باید به PEMS.Persistence رفرنس می‌داد. ولی PEMS.Persistence
/// هم برای ثبت DbSet های Workflow باید به PEMS.WorkflowPlatform رفرنس بدهد - یعنی وابستگی
/// چرخه‌ای (که در .NET اصلاً ممکن نیست). این اینترفیس مسئله را طبق اصل Dependency Inversion
/// حل می‌کند: PEMS.WorkflowPlatform این قرارداد را تعریف می‌کند، PEMSDbContext در
/// PEMS.Persistence آن را پیاده‌سازی می‌کند. وابستگی فقط در یک جهت است.
/// ==============================================================================
/// </summary>
public interface IWorkflowDbContext
{
    DbSet<WorkflowDefinition> WorkflowDefinitions { get; }
    DbSet<WorkflowVersion> WorkflowVersions { get; }
    DbSet<WorkflowStage> WorkflowStages { get; }
    DbSet<WorkflowTransition> WorkflowTransitions { get; }
    DbSet<WorkflowInstance> WorkflowInstances { get; }
    DbSet<WorkflowInstanceStage> WorkflowInstanceStages { get; }
    DbSet<WorkflowHistory> WorkflowHistories { get; }
    DbSet<WorkflowBinding> WorkflowBindings { get; }
    DbSet<WorkflowConfigAudit> WorkflowConfigAudits { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
