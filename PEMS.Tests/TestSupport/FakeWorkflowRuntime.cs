using PEMS.WorkflowPlatform.Dtos;
using PEMS.WorkflowPlatform.Interfaces;

namespace PEMS.Tests.TestSupport;

/// <summary>
/// جایگزین سبک IWorkflowRuntime برای تست‌های Grid که فقط GetPendingTasksAsync لازم دارند
/// (مثلاً PermitController.Index با pendingMyApproval=true). بقیه‌ی متدها در این تست‌ها صدا
/// زده نمی‌شوند.
/// </summary>
public class FakeWorkflowRuntime : IWorkflowRuntime
{
    public HashSet<int> PendingPermitIds { get; set; } = new();

    /// <summary>
    /// وقتی ست شود، به‌جای AssignedDate خودکار (بر اساس ترتیب Enumerate شدن HashSet - که
    /// تضمین‌شده نیست)، برای هر PermitId دقیقاً همین تاریخ استفاده می‌شود - برای تست‌های
    /// مرتب‌سازی قطعی (چند Permit با AssignedDate یکسان) لازم است.
    /// </summary>
    public Dictionary<int, DateTime>? ExplicitAssignedDates { get; set; }

    public Task<int> StartInstanceAsync(string module, string businessObjectType, int businessObjectId,
        string workflowCategory, int? projectId, int? initiatedByEmployeeId,
        IReadOnlyList<string>? firstStageParticipantRoles = null) => throw new NotImplementedException();

    public Task ExecuteActionAsync(int workflowInstanceId, string actionType, int? actorEmployeeId,
        IReadOnlyList<string> actorRoles, string? comment, string? ipAddress, string? userAgent,
        IReadOnlyList<string>? nextStageParticipantRoles = null) => throw new NotImplementedException();

    public Task<WorkflowInstanceDto?> GetInstanceAsync(string businessObjectType, int businessObjectId) =>
        Task.FromResult<WorkflowInstanceDto?>(null);

    public Task<WorkflowInstanceDto?> GetInstanceByIdAsync(int workflowInstanceId) =>
        Task.FromResult<WorkflowInstanceDto?>(null);

    public Task<IReadOnlyList<WorkflowHistoryDto>> GetHistoryAsync(int workflowInstanceId) =>
        Task.FromResult<IReadOnlyList<WorkflowHistoryDto>>(new List<WorkflowHistoryDto>());

    public Task<IReadOnlyList<PendingTaskDto>> GetPendingTasksAsync(int employeeId, IReadOnlyList<string> actorRoles)
    {
        var tasks = PendingPermitIds.Select((permitId, idx) => new PendingTaskDto
        {
            WorkflowInstanceId = idx + 1,
            WorkflowInstanceStageId = idx + 1,
            Module = "Permit",
            BusinessObjectType = "Permit",
            BusinessObjectId = permitId,
            StageName = "Approval",
            AssignedDate = ExplicitAssignedDates != null && ExplicitAssignedDates.TryGetValue(permitId, out var explicitDate)
                ? explicitDate
                : DateTime.Today.AddMinutes(-idx)
        }).ToList();

        return Task.FromResult<IReadOnlyList<PendingTaskDto>>(tasks);
    }
}
