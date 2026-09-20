using PEMS.Application.DTOs;
using PEMS.WorkflowPlatform.Interfaces;

namespace PEMS.Application.Interfaces;

/// <summary>
/// Phase 4 - لایه‌ی مدیریتی روی Workflow Platform موجود (نه موتور جدید). همه‌ی عملیات: (۱) نقش SystemAdmin + مجوز
/// WorkflowManagement.* را سمت سرور بررسی می‌کنند، (۲) فقط تعریف‌های «قابل‌ویرایش» را تغییر می‌دهند، (۳) همه‌ی تغییرات را از
/// IWorkflowDefinitionService/IWorkflowBindingService/IWorkflowVersionValidator موجود عبور می‌دهند (هیچ منطق اعتبارسنجی/انتشار تکراری نیست).
/// </summary>
public interface IWorkflowAdministrationService
{
    Task<WorkflowAdminResult> AuthorizeAsync(WorkflowAdminActor actor, string action);

    Task<List<WorkflowDefinitionRow>> ListAsync(WorkflowAdminActor actor);
    Task<WorkflowDefinitionDetails?> GetDetailsAsync(WorkflowAdminActor actor, int definitionId);
    Task<WorkflowDesignerModel?> GetDesignerAsync(WorkflowAdminActor actor, int versionId);
    Task<List<WorkflowBindingRow>> ListBindingsAsync(WorkflowAdminActor actor);
    Task<WorkflowBindingImpact?> GetBindingImpactAsync(WorkflowAdminActor actor, int bindingId, int targetVersionId);

    Task<WorkflowAdminResult> CloneVersionAsync(WorkflowAdminActor actor, int sourceVersionId, string? changeSummary);
    Task<WorkflowAdminResult> UpdateDefinitionAsync(WorkflowAdminActor actor, int definitionId, string name, string? nameFa, string? description);
    Task<WorkflowAdminResult> DiscardDraftAsync(WorkflowAdminActor actor, int versionId, string? rowVersion);

    Task<WorkflowAdminResult> AddStageAsync(WorkflowAdminActor actor, int versionId, string? rowVersion, WorkflowStageEditModel input);
    Task<WorkflowAdminResult> UpdateStageAsync(WorkflowAdminActor actor, int versionId, string? rowVersion, WorkflowStageEditModel input);
    Task<WorkflowAdminResult> RemoveStageAsync(WorkflowAdminActor actor, int versionId, string? rowVersion, int stageId);
    Task<WorkflowAdminResult> AddTransitionAsync(WorkflowAdminActor actor, int versionId, string? rowVersion, WorkflowTransitionEditModel input);
    Task<WorkflowAdminResult> UpdateTransitionAsync(WorkflowAdminActor actor, int versionId, string? rowVersion, WorkflowTransitionEditModel input);
    Task<WorkflowAdminResult> RemoveTransitionAsync(WorkflowAdminActor actor, int versionId, string? rowVersion, int transitionId);

    Task<WorkflowAdminResult> ValidateAsync(WorkflowAdminActor actor, int versionId, string? rowVersion);
    Task<WorkflowAdminResult> PublishAsync(WorkflowAdminActor actor, int versionId, string? rowVersion);
    Task<WorkflowAdminResult> RetireAsync(WorkflowAdminActor actor, int versionId);
    Task<WorkflowAdminResult> ArchiveDefinitionAsync(WorkflowAdminActor actor, int definitionId);
    Task<WorkflowAdminResult> ChangeBindingAsync(WorkflowAdminActor actor, int bindingId, int targetVersionId, bool confirmed);
    Task<WorkflowAdminResult> DeactivateBindingAsync(WorkflowAdminActor actor, int bindingId, bool confirmed);
}
