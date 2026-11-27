using PEMS.Web.Security;
using Microsoft.Extensions.Localization;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Web.Helpers;
using PEMS.Web.Resources;

namespace PEMS.Web.Services;

/// <summary>SCRUM-831: the two task lists and the assignee choices of TaskController.Index as read operations over the same services (see <see cref="ITaskPagesOperations"/>).</summary>
public sealed class TaskPagesOperations(ITaskService tasks, ITaskManagerService manager, ICurrentUserService currentUser, IPermissionService permissions, IStringLocalizer<SharedResource> localizer) : ITaskPagesOperations
{
    private const int Take = 500;

    private Task<TaskActor> ActorAsync() => manager.ResolveActorAsync(currentUser.UserId, currentUser.Roles, currentUser.FullName);

    public async Task<List<TaskListRow>> ActionItemsAsync()
        => currentUser.UserId is not { } id ? [] : Rows((await tasks.GetMyActionItemsAsync(id, page: 1, pageSize: Take)).Items, await CanEditAsync());

    public async Task<List<TaskListRow>> FollowUpAsync()
        => currentUser.UserId is not { } id ? [] : Rows((await tasks.GetAssignedByMeAsync(id, page: 1, pageSize: Take)).Items, await CanEditAsync());

    public async Task<List<TaskChoice>> AssigneeOptionsAsync()
        => (await manager.GetEligibleAssigneesAsync(null, await ActorAsync())).Candidates.Select(e => new TaskChoice(e.EmployeeId.ToString(), e.Name)).ToList();

    private Task<bool> CanEditAsync() => permissions.HasPermissionAsync(currentUser.Roles, "Task", "Edit");

    private List<TaskListRow> Rows(IEnumerable<TaskItemDto> items, bool canEdit)
        => items.Select(t => new TaskListRow(t.TaskItemId, t.ItemKey, t.Title, t.Description, t.AssignedToEmployeeName, t.DueDate, TaskManagerUi.Priority(localizer, t.Priority), t.Priority,
            t.WorkSpaceId != null ? TaskManagerUi.Name(t.StatusName, t.StatusNameFa) : TaskManagerUi.LegacyStatus(localizer, t.Status), t.Status, t.IsOverdue,
            string.IsNullOrEmpty(t.RelatedType) ? null : t.RelatedType + " #" + t.RelatedId,
            canEdit && t.WorkSpaceId == null && TaskItemStatuses.Transitions.TryGetValue(t.Status, out var targets) && targets.Length > 0, t.CompletionNote)).ToList();
}
