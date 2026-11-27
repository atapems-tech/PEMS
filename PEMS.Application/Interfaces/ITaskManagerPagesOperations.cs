namespace PEMS.Application.Interfaces;

/// <summary>The one-row head of the task manager start page: whether the user is a system administrator (the admin link) and the notice shown when the account has no employee record.</summary>
public sealed record TaskManagerHeadRow(int Id, bool IsAdmin, string? Notice);

/// <summary>One work space of the start page: <c>Role</c> is "Lead" for a lead of the space.</summary>
public sealed record TaskSpaceRow(int Id, string Key, string Name, string? OwnerDepartment, string? Role, int OpenItems, int Members);

/// <summary>SCRUM-831: the read side of TaskManagerController.Index (the spaces the user may see) as operations; Task.View is the right the controller checked.</summary>
public interface ITaskManagerPagesOperations
{
    [PEMS.Application.Common.PemsOperation("TaskManagerPage", "Head", "Task.View")]
    Task<List<TaskManagerHeadRow>> HeadAsync();

    [PEMS.Application.Common.PemsOperation("TaskManagerPage", "Spaces", "Task.View")]
    Task<List<TaskSpaceRow>> SpacesAsync();
}
