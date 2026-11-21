namespace PEMS.Application.Interfaces;

/// <summary>
/// SCRUM-756: the scalar commands of TaskManagerController as operations over the task manager and the work space services: the task steps (assign, status, archive, rank, sprint, comments, links, watch),
/// the board and backlog ordering, the bulk bar, the sprint commands and the space administration (members, statuses, transitions, types; system administrators only). The actor is the current user and the
/// error keys of the services are translated. The create and update of a task (an input object) and the save of a space stay on the controller together with the pages.
/// </summary>
public interface ITaskManagerOperations
{
    [PEMS.Application.Common.PemsOperation("TaskManager", "Assign", "Task.Edit")]
    Task<(bool Success, string? Error)> AssignAsync(int id, int? assigneeEmployeeId = null, string? rowVersion = null);

    [PEMS.Application.Common.PemsOperation("TaskManager", "Transition", "Task.Edit")]
    Task<(bool Success, string? Error)> TransitionAsync(int id, int? statusId = null, string? legacyStatus = null, string? note = null, string? rowVersion = null);

    [PEMS.Application.Common.PemsOperation("TaskManager", "Archive", "Task.Edit")]
    Task<(bool Success, string? Error)> ArchiveAsync(int id, bool archived = true);

    [PEMS.Application.Common.PemsOperation("TaskManager", "Rank", "Task.Edit")]
    Task<(bool Success, string? Error)> RankAsync(int id, bool up);

    [PEMS.Application.Common.PemsOperation("TaskManager", "Reorder", "Task.Edit")]
    Task<(bool Success, string? Error)> ReorderAsync(int workSpaceId, int? sprintId, List<int> ids);

    [PEMS.Application.Common.PemsOperation("TaskManager", "SetSprint", "Task.Edit")]
    Task<(bool Success, string? Error)> SetSprintAsync(int id, int? sprintId = null);

    /// <summary>The bulk bar: assign, status, sprint or archive for the selected tasks; each task is authorized on its own, the answer counts the successes and lists the first failures.</summary>
    [PEMS.Application.Common.PemsOperation("TaskManager", "Bulk", "Task.Edit")]
    Task<(bool Success, string? Error)> BulkAsync(List<int> ids, string bulkAction, string? value = null);

    [PEMS.Application.Common.PemsOperation("TaskManager", "Comment", "Task.Edit")]
    Task<(bool Success, string? Error)> CommentAsync(int id, string body);

    [PEMS.Application.Common.PemsOperation("TaskManager", "DeleteComment", "Task.Edit")]
    Task<(bool Success, string? Error)> DeleteCommentAsync(int commentId);

    [PEMS.Application.Common.PemsOperation("TaskManager", "AddLink", "Task.Edit")]
    Task<(bool Success, string? Error)> AddLinkAsync(int id, string linkType, string target);

    [PEMS.Application.Common.PemsOperation("TaskManager", "RemoveLink", "Task.Edit")]
    Task<(bool Success, string? Error)> RemoveLinkAsync(int linkId);

    [PEMS.Application.Common.PemsOperation("TaskManager", "Watch", "Task.Edit")]
    Task<(bool Success, string? Error)> WatchAsync(int id, bool watch);

    [PEMS.Application.Common.PemsOperation("TaskManager", "CreateSprint", "Task.Edit")]
    Task<(bool Success, string? Error)> CreateSprintAsync(int workSpaceId, string name, string? goal = null, DateTime? startDate = null, DateTime? endDate = null);

    [PEMS.Application.Common.PemsOperation("TaskManager", "UpdateSprint", "Task.Edit")]
    Task<(bool Success, string? Error)> UpdateSprintAsync(int sprintId, string name, string? goal = null, DateTime? startDate = null, DateTime? endDate = null);

    [PEMS.Application.Common.PemsOperation("TaskManager", "StartSprint", "Task.Edit")]
    Task<(bool Success, string? Error)> StartSprintAsync(int sprintId);

    [PEMS.Application.Common.PemsOperation("TaskManager", "CompleteSprint", "Task.Edit")]
    Task<(bool Success, string? Error)> CompleteSprintAsync(int sprintId, int? moveTo = null);

    // the space administration: system administrators only (the actor decides; anyone else is Forbid)
    [PEMS.Application.Common.PemsOperation("TaskManager", "AddMember")]
    Task<(bool Success, string? Error)> AddMemberAsync(int workSpaceId, int employeeId, string role);

    [PEMS.Application.Common.PemsOperation("TaskManager", "SetMember")]
    Task<(bool Success, string? Error)> SetMemberAsync(int memberId, string role, bool isActive = false);

    [PEMS.Application.Common.PemsOperation("TaskManager", "AddStatus")]
    Task<(bool Success, string? Error)> AddStatusAsync(int workSpaceId, string name, string category, string? nameFa = null);

    [PEMS.Application.Common.PemsOperation("TaskManager", "EditStatus")]
    Task<(bool Success, string? Error)> EditStatusAsync(int statusId, string name, string category, int sortOrder, string? nameFa = null, bool isActive = false);

    [PEMS.Application.Common.PemsOperation("TaskManager", "SetTransition")]
    Task<(bool Success, string? Error)> SetTransitionAsync(int workSpaceId, int fromStatusId, int toStatusId, bool allowed = false);

    [PEMS.Application.Common.PemsOperation("TaskManager", "AddType")]
    Task<(bool Success, string? Error)> AddTypeAsync(int workSpaceId, string name, string icon, string? nameFa = null, bool isSubtask = false);

    [PEMS.Application.Common.PemsOperation("TaskManager", "SetTypeActive")]
    Task<(bool Success, string? Error)> SetTypeActiveAsync(int typeId, bool isActive = false);
}
