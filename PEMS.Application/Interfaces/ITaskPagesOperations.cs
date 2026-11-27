namespace PEMS.Application.Interfaces;

/// <summary>One row of the two task lists; the status step is offered only when <c>CanMove</c> (the old page showed the step form only for personal tasks the user may edit that have a next status).</summary>
public sealed record TaskListRow(int Id, string? Key, string Title, string? Description, string? AssignedTo, DateTime? Due, string Priority, string PriorityCode, string Status, string StatusCode, bool Overdue, string? Related, bool CanMove, string? CompletionNote);

public sealed record TaskChoice(string Value, string Label);

/// <summary>
/// SCRUM-831: the two lists of TaskController.Index (the tasks assigned to me, the tasks I assigned) and the assignee choices of the new-task form, as read operations. Task.View is the right the page checked;
/// the status step stays the TaskItem.UpdateStatus operation (Task.Edit) and the new task TaskItem.Create (Task.Create).
/// </summary>
public interface ITaskPagesOperations
{
    [PEMS.Application.Common.PemsOperation("TaskPage", "ActionItems", "Task.View")]
    Task<List<TaskListRow>> ActionItemsAsync();

    [PEMS.Application.Common.PemsOperation("TaskPage", "FollowUp", "Task.View")]
    Task<List<TaskListRow>> FollowUpAsync();

    [PEMS.Application.Common.PemsOperation("TaskPage", "AssigneeOptions", "Task.Create")]
    Task<List<TaskChoice>> AssigneeOptionsAsync();
}
