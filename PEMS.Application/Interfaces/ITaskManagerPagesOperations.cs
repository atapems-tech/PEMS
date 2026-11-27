namespace PEMS.Application.Interfaces;

/// <summary>The one-row head of the task manager start page: whether the user is a system administrator (the admin link) and the notice shown when the account has no employee record.</summary>
public sealed record TaskManagerHeadRow(int Id, bool IsAdmin, string? Notice);

/// <summary>One work space of the start page: <c>Role</c> is "Lead" for a lead of the space.</summary>
public sealed record TaskSpaceRow(int Id, string Key, string Name, string? OwnerDepartment, string? Role, int OpenItems, int Members);

public sealed record TaskBoardHeadRow(int Id, string Key, string Name, string? Role, string? Description, string Sprint, string Hint, bool CanCreate);
public sealed record TaskBoardGroupRow(string Value, string Label, string Tone);
public sealed record TaskBoardCardRow(int Id, string Title, string Subtitle, string? Badge, string Status, string Allowed, string? RowVersion);

public sealed record TaskBacklogHeadRow(int Id, string Key, string Name, string? Role, string? Description, string Hint, bool CanCreate, bool CanManageSprints, bool CanRank);
public sealed record TaskSprintRow(int Id, string Name, string State, string StateCode, string Period, string Counts, string? Goal, bool CanStart, bool CanComplete, bool CanManage);
public sealed record TaskBacklogItemRow(int Id, string Key, string Title, string? Type, string Status, string Priority, string Assignee, DateTime? DueDate, bool Overdue, decimal? Points, string Sprint, int SprintId, bool CanRank, bool Archived);

public sealed record TaskWorkRow(int Id, string Key, string Space, string Title, string? Type, string Status, string Priority, string Assignee, DateTime? DueDate, bool Overdue, decimal? Points, bool Archived);

public sealed record TaskAdminSpaceRow(int Id, string Key, string Name, string? Department, int Members, int OpenItems, bool Active);
public sealed record TaskAdminSpaceHeadRow(int Id, string Key, string Name, string? NameFa, string? Description, int? OwnerDepartmentId, bool IsActive);
public sealed record TaskAdminMemberRow(int Id, string Name, string Role, string RoleCode, bool Active);
public sealed record TaskAdminTypeRow(int Id, string Name, string Icon, bool Subtask, bool Active, bool NextActive);
public sealed record TaskAdminStatusRow(int Id, string Name, string? NameFa, string Category, string CategoryCode, int SortOrder, bool Active);
public sealed record TaskAdminTransitionRow(int Id, string From, string FromLabel, string To, string ToLabel, string Allowed);

/// <summary>SCRUM-831: the read side of TaskManagerController.Index (the spaces the user may see) as operations; Task.View is the right the controller checked.</summary>
public interface ITaskManagerPagesOperations
{
    [PEMS.Application.Common.PemsOperation("TaskManagerPage", "Head", "Task.View")]
    Task<List<TaskManagerHeadRow>> HeadAsync();

    [PEMS.Application.Common.PemsOperation("TaskManagerPage", "Spaces", "Task.View")]
    Task<List<TaskSpaceRow>> SpacesAsync();

    /// <summary>The board of a space (the key of the space in the address): the header, the columns (the statuses of the space) and the cards with the columns each may move to.</summary>
    [PEMS.Application.Common.PemsOperation("TaskManagerPage", "BoardHead", "Task.View")]
    Task<List<TaskBoardHeadRow>> BoardHeadAsync(string? key = null);

    [PEMS.Application.Common.PemsOperation("TaskManagerPage", "BoardGroups", "Task.View")]
    Task<List<TaskBoardGroupRow>> BoardGroupsAsync(string? key = null);

    [PEMS.Application.Common.PemsOperation("TaskManagerPage", "BoardCards", "Task.View")]
    Task<List<TaskBoardCardRow>> BoardCardsAsync(string? key = null);

    /// <summary>The choices of the new task form of a space (the id of the space): its types, the people who may be assigned, its sprints (the backlog first) and the priorities.</summary>
    [PEMS.Application.Common.PemsOperation("TaskManagerPage", "TypeOptions", "Task.Create")]
    Task<List<TaskChoice>> TypeOptionsAsync(int id);

    [PEMS.Application.Common.PemsOperation("TaskManagerPage", "AssigneeOptions", "Task.Create")]
    Task<List<TaskChoice>> AssigneeOptionsAsync(int id);

    [PEMS.Application.Common.PemsOperation("TaskManagerPage", "SprintOptions", "Task.Create")]
    Task<List<TaskChoice>> SprintOptionsAsync(int id);

    [PEMS.Application.Common.PemsOperation("TaskManagerPage", "PriorityOptions", "Task.Create")]
    Task<List<TaskChoice>> PriorityOptionsAsync();

    /// <summary>The backlog of a space (the key in the address): the header with the rights, the sprints with their commands, every task in the order of the old panels (sprints first, then the backlog), and the choice lists of the bulk forms.</summary>
    [PEMS.Application.Common.PemsOperation("TaskManagerPage", "BacklogHead", "Task.View")]
    Task<List<TaskBacklogHeadRow>> BacklogHeadAsync(string? key = null);

    [PEMS.Application.Common.PemsOperation("TaskManagerPage", "BacklogSprints", "Task.View")]
    Task<List<TaskSprintRow>> BacklogSprintsAsync(string? key = null);

    [PEMS.Application.Common.PemsOperation("TaskManagerPage", "BacklogItems", "Task.View")]
    Task<List<TaskBacklogItemRow>> BacklogItemsAsync(string? key = null);

    [PEMS.Application.Common.PemsOperation("TaskManagerPage", "StatusOptionsByKey", "Task.View")]
    Task<List<TaskChoice>> StatusOptionsByKeyAsync(string? key = null);

    [PEMS.Application.Common.PemsOperation("TaskManagerPage", "AssigneeOptionsByKey", "Task.View")]
    Task<List<TaskChoice>> AssigneeOptionsByKeyAsync(string? key = null);

    [PEMS.Application.Common.PemsOperation("TaskManagerPage", "SprintOptionsByKey", "Task.View")]
    Task<List<TaskChoice>> SprintOptionsByKeyAsync(string? key = null);

    [PEMS.Application.Common.PemsOperation("TaskManagerPage", "FutureSprintOptions", "Task.View")]
    Task<List<TaskChoice>> FutureSprintOptionsAsync(string? key = null);

    /// <summary>All work the user may see (AllWork): every filter of the old page is an optional text argument (<c>spaceKey</c>, <c>text</c>, <c>assignee</c> me, unassigned or an employee id, <c>statusCategory</c>, <c>priority</c>, <c>includeArchived</c> true); at most 500 tasks, newest changes first.</summary>
    [PEMS.Application.Common.PemsOperation("TaskManagerPage", "AllWork", "Task.View")]
    Task<List<TaskWorkRow>> AllWorkAsync(string? spaceKey = null, string? text = null, string? assignee = null, string? statusCategory = null, string? priority = null, string? includeArchived = null);

    [PEMS.Application.Common.PemsOperation("TaskManagerPage", "SpaceOptions", "Task.View")]
    Task<List<TaskChoice>> SpaceOptionsAsync();

    [PEMS.Application.Common.PemsOperation("TaskManagerPage", "AllWorkAssigneeOptions", "Task.View")]
    Task<List<TaskChoice>> AllWorkAssigneeOptionsAsync();

    [PEMS.Application.Common.PemsOperation("TaskManagerPage", "StatusCategoryOptions", "Task.View")]
    Task<List<TaskChoice>> StatusCategoryOptionsAsync();

    /// <summary>The space administration pages (system administrators only; another user is refused like the old pages did).</summary>
    [PEMS.Application.Common.PemsOperation("TaskManagerPage", "AdminSpaces", "Task.View")]
    Task<List<TaskAdminSpaceRow>> AdminSpacesAsync();

    [PEMS.Application.Common.PemsOperation("TaskManagerPage", "AdminSpaceHead", "Task.View")]
    Task<List<TaskAdminSpaceHeadRow>> AdminSpaceHeadAsync(int id);

    [PEMS.Application.Common.PemsOperation("TaskManagerPage", "AdminMembers", "Task.View")]
    Task<List<TaskAdminMemberRow>> AdminMembersAsync(int id);

    [PEMS.Application.Common.PemsOperation("TaskManagerPage", "AdminTypes", "Task.View")]
    Task<List<TaskAdminTypeRow>> AdminTypesAsync(int id);

    [PEMS.Application.Common.PemsOperation("TaskManagerPage", "AdminStatuses", "Task.View")]
    Task<List<TaskAdminStatusRow>> AdminStatusesAsync(int id);

    [PEMS.Application.Common.PemsOperation("TaskManagerPage", "AdminTransitions", "Task.View")]
    Task<List<TaskAdminTransitionRow>> AdminTransitionsAsync(int id);

    [PEMS.Application.Common.PemsOperation("TaskManagerPage", "DepartmentOptions", "Task.View")]
    Task<List<TaskChoice>> DepartmentOptionsAsync();

    [PEMS.Application.Common.PemsOperation("TaskManagerPage", "NewMemberOptions", "Task.View")]
    Task<List<TaskChoice>> NewMemberOptionsAsync(int id);

    [PEMS.Application.Common.PemsOperation("TaskManagerPage", "RoleOptions", "Task.View")]
    Task<List<TaskChoice>> RoleOptionsAsync();

    [PEMS.Application.Common.PemsOperation("TaskManagerPage", "CategoryOptions", "Task.View")]
    Task<List<TaskChoice>> CategoryOptionsAsync();
}
