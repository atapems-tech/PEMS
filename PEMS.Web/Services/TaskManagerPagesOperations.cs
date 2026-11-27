using Microsoft.Extensions.Localization;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Web.Helpers;
using PEMS.Web.Resources;

namespace PEMS.Web.Services;

/// <summary>SCRUM-831: the task manager start page as read operations over the same service as TaskManagerController.Index (see <see cref="ITaskManagerPagesOperations"/>).</summary>
public sealed class TaskManagerPagesOperations(ITaskManagerService tasks, ICurrentUserService currentUser, IStringLocalizer<SharedResource> localizer) : ITaskManagerPagesOperations
{
    private Task<PEMS.Application.DTOs.TaskActor> ActorAsync() => tasks.ResolveActorAsync(currentUser.UserId, currentUser.Roles, currentUser.FullName);

    public async Task<List<TaskManagerHeadRow>> HeadAsync()
    {
        var actor = await ActorAsync();
        return [new TaskManagerHeadRow(1, actor.IsSystemAdmin, actor.EmployeeId == null ? localizer["Tm_NoEmployeeLink"].Value : null)];
    }

    public async Task<List<TaskSpaceRow>> SpacesAsync()
        => (await tasks.GetAccessibleSpacesAsync(await ActorAsync())).Select(s => new TaskSpaceRow(s.Id, s.Key, TaskManagerUi.Name(s.Name, s.NameFa), s.OwnerDepartmentName, s.IsLead ? localizer["Tm_Role_Lead"].Value : null, s.OpenItemCount, s.MemberCount)).ToList();

    private async Task<string?> KeyOfAsync(int spaceId) => (await tasks.GetAccessibleSpacesAsync(await ActorAsync())).FirstOrDefault(x => x.Id == spaceId)?.Key;

    public async Task<List<TaskBoardHeadRow>> BoardHeadAsync(string? key = null)
    {
        if (string.IsNullOrWhiteSpace(key)) return [];
        var board = await tasks.GetBoardAsync(key, await ActorAsync());
        if (board == null) return [];
        var sp = board.ActiveSprint;
        var sprint = sp == null ? localizer["Tm_BoardNoSprint"].Value
            : sp.Name + " - " + TaskManagerUi.Date(sp.StartDate) + " - " + TaskManagerUi.Date(sp.EndDate) + (string.IsNullOrWhiteSpace(sp.Goal) ? "" : " - " + sp.Goal);
        var space = board.View.Space;
        return [new TaskBoardHeadRow(space.Id, space.Key, TaskManagerUi.Name(space.Name, space.NameFa), space.IsLead ? localizer["Tm_Role_Lead"].Value : null, space.Description, sprint, localizer["Tm_BoardHint"].Value, board.View.CanCreate)];
    }

    public async Task<List<TaskBoardGroupRow>> BoardGroupsAsync(string? key = null)
    {
        if (string.IsNullOrWhiteSpace(key)) return [];
        var board = await tasks.GetBoardAsync(key, await ActorAsync());
        return board?.Columns.Select(c => new TaskBoardGroupRow(c.Status.Id.ToString(), TaskManagerUi.Name(c.Status.Name, c.Status.NameFa), TaskManagerUi.CategoryBadge(c.Status.Category))).ToList() ?? [];
    }

    public async Task<List<TaskBoardCardRow>> BoardCardsAsync(string? key = null)
    {
        if (string.IsNullOrWhiteSpace(key)) return [];
        var board = await tasks.GetBoardAsync(key, await ActorAsync());
        if (board == null) return [];
        var rows = new List<TaskBoardCardRow>();
        foreach (var col in board.Columns)
        {
            var targets = board.View.Transitions.TryGetValue(col.Status.Id, out var t) ? t : new List<int>();
            foreach (var i in col.Items)
            {
                var sub = string.Join(" - ", new[] { i.AssigneeName ?? localizer["Tm_Unassigned"].Value, i.StoryPoints?.ToString("0.#"), i.DueDate == null ? null : TaskManagerUi.Date(i.DueDate) + (i.IsOverdue ? " (" + localizer["Overdue"].Value + ")" : "") }.Where(x => !string.IsNullOrEmpty(x)));
                rows.Add(new TaskBoardCardRow(i.Id, (i.Key == null ? "" : i.Key + " ") + i.Title, sub, TaskManagerUi.Priority(localizer, i.Priority), col.Status.Id.ToString(), i.CanTransition ? string.Join(",", targets) : "", i.RowVersion));
            }
        }
        return rows;
    }

    public async Task<List<TaskChoice>> TypeOptionsAsync(int id)
    {
        var key = await KeyOfAsync(id);
        var view = key == null ? null : await tasks.GetSpaceViewAsync(key, await ActorAsync());
        return view?.Types.Where(x => x.IsActive).Select(x => new TaskChoice(x.Id.ToString(), TaskManagerUi.Name(x.Name, x.NameFa))).ToList() ?? [];
    }

    public async Task<List<TaskChoice>> AssigneeOptionsAsync(int id)
        => [new TaskChoice("", localizer["Tm_Unassigned"].Value), .. (await tasks.GetEligibleAssigneesAsync(id, await ActorAsync())).Candidates.Select(e => new TaskChoice(e.EmployeeId.ToString(), e.Name))];

    public async Task<List<TaskChoice>> SprintOptionsAsync(int id)
    {
        var key = await KeyOfAsync(id);
        var view = key == null ? null : await tasks.GetSpaceViewAsync(key, await ActorAsync());
        return [new TaskChoice("", localizer["Tm_Backlog"].Value), .. view?.Sprints.Select(s => new TaskChoice(s.Id.ToString(), s.Name)) ?? []];
    }

    public Task<List<TaskChoice>> PriorityOptionsAsync() => Task.FromResult(TaskItemPriorities.All.Select(p => new TaskChoice(p, TaskManagerUi.Priority(localizer, p))).ToList());
}
