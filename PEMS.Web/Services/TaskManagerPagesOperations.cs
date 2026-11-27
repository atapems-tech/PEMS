using Microsoft.Extensions.Localization;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Web.Helpers;
using PEMS.Web.Resources;

namespace PEMS.Web.Services;

/// <summary>SCRUM-831: the task manager start page as read operations over the same service as TaskManagerController.Index (see <see cref="ITaskManagerPagesOperations"/>).</summary>
public sealed class TaskManagerPagesOperations(ITaskManagerService tasks, IWorkSpaceService spaces, ICurrentUserService currentUser, IStringLocalizer<SharedResource> localizer) : ITaskManagerPagesOperations
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

    public Task<List<TaskChoice>> PriorityOptionsAsync() => Task.FromResult(TaskItemPriorities.All.Select(p => new TaskChoice(p, TaskManagerUi.Priorit
    public async Task<List<TaskBacklogHeadRow>> BacklogHeadAsync(string? key = null)
    {
        if (string.IsNullOrWhiteSpace(key)) return [];
        var view = await tasks.GetSpaceViewAsync(key, await ActorAsync());
        if (view == null) return [];
        var space = view.Space;
        return [new TaskBacklogHeadRow(space.Id, space.Key, TaskManagerUi.Name(space.Name, space.NameFa), space.IsLead ? localizer["Tm_Role_Lead"].Value : null, space.Description, localizer["Tm_BulkHint"].Value, view.CanCreate, view.CanManageSprints, view.CanRank)];
    }

    public async Task<List<TaskSprintRow>> BacklogSprintsAsync(string? key = null)
    {
        if (string.IsNullOrWhiteSpace(key)) return [];
        var backlog = await tasks.GetBacklogAsync(key, await ActorAsync());
        if (backlog == null) return [];
        var hasActive = backlog.View.Sprints.Any(s => s.State == SprintStates.Active);
        return backlog.SprintPanels.Select(p =>
        {
            var sp = p.Sprint;
            var active = sp.State == SprintStates.Active;
            var period = (TaskManagerUi.Date(sp.StartDate) + (sp.StartDate != null || sp.EndDate != null ? " - " : "") + TaskManagerUi.Date(sp.EndDate)).Trim();
            return new TaskSprintRow(sp.Id, sp.Name, localizer["Tm_SprintState_" + sp.State].Value, sp.State, period, localizer["Tm_SprintCounts", sp.ToDoCount, sp.InProgressCount, sp.DoneCount].Value, sp.Goal,
                backlog.View.CanManageSprints && !active && !hasActive, backlog.View.CanManageSprints && active, backlog.View.CanManageSprints);
        }).ToList();
    }

    public async Task<List<TaskBacklogItemRow>> BacklogItemsAsync(string? key = null)
    {
        if (string.IsNullOrWhiteSpace(key)) return [];
        var backlog = await tasks.GetBacklogAsync(key, await ActorAsync());
        if (backlog == null) return [];
        var canRank = backlog.View.CanRank;
        TaskBacklogItemRow Row(WorkItemListItemDto i, string sprint) => new(i.Id, i.Key ?? "#" + i.Id, i.Title, i.TypeName == null ? null : TaskManagerUi.Name(i.TypeName, i.TypeNameFa), TaskManagerUi.StatusLabel(localizer, i),
            TaskManagerUi.Priority(localizer, i.Priority), i.AssigneeName ?? localizer["Tm_Unassigned"].Value, i.DueDate, i.IsOverdue, i.StoryPoints, sprint, i.SprintId ?? 0, canRank, !i.IsActive);
        return backlog.SprintPanels.SelectMany(p => p.Items.Select(i => Row(i, p.Sprint.Name))).Concat(backlog.BacklogItems.Select(i => Row(i, localizer["Tm_Backlog"].Value))).ToList();
    }

    public async Task<List<TaskChoice>> StatusOptionsByKeyAsync(string? key = null)
    {
        var view = string.IsNullOrWhiteSpace(key) ? null : await tasks.GetSpaceViewAsync(key, await ActorAsync());
        return view?.Statuses.Select(s => new TaskChoice(s.Id.ToString(), TaskManagerUi.Name(s.Name, s.NameFa))).ToList() ?? [];
    }

    public async Task<List<TaskChoice>> AssigneeOptionsByKeyAsync(string? key = null)
    {
        var actor = await ActorAsync();
        var view = string.IsNullOrWhiteSpace(key) ? null : await tasks.GetSpaceViewAsync(key, actor);
        if (view == null) return [];
        return [new TaskChoice("", localizer["Tm_Unassigned"].Value), .. (await tasks.GetEligibleAssigneesAsync(view.Space.Id, actor)).Candidates.Select(e => new TaskChoice(e.EmployeeId.ToString(), e.Name))];
    }

    public async Task<List<TaskChoice>> SprintOptionsByKeyAsync(string? key = null)
    {
        var view = string.IsNullOrWhiteSpace(key) ? null : await tasks.GetSpaceViewAsync(key, await ActorAsync());
        return view == null ? [] : [new TaskChoice("0", localizer["Tm_Backlog"].Value), .. view.Sprints.Select(s => new TaskChoice(s.Id.ToString(), s.Name))];
    }

    public async Task<List<TaskChoice>> FutureSprintOptionsAsync(string? key = null)
    {
        var view = string.IsNullOrWhiteSpace(key) ? null : await tasks.GetSpaceViewAsync(key, await ActorAsync());
        return view == null ? [] : [new TaskChoice("0", localizer["Tm_Backlog"].Value), .. view.Sprints.Where(s => s.State == SprintStates.Future).Select(s => new TaskChoice(s.Id.ToString(), s.Name))];
    }
y(localizer, p))).ToList());

    public async Task<List<TaskWorkRow>> AllWorkAsync(string? spaceKey = null, string? text = null, string? assignee = null, string? statusCategory = null, string? priority = null, string? includeArchived = null)
    {
        var actor = await ActorAsync();
        int? spaceId = null;
        if (!string.IsNullOrWhiteSpace(spaceKey)) spaceId = (await tasks.GetAccessibleSpacesAsync(actor)).FirstOrDefault(x => string.Equals(x.Key, spaceKey, StringComparison.OrdinalIgnoreCase))?.Id;
        if (!string.IsNullOrWhiteSpace(spaceKey) && spaceId == null) return [];
        var query = new WorkItemQuery
        {
            WorkSpaceId = spaceId, Text = string.IsNullOrWhiteSpace(text) ? null : text, Assignee = string.IsNullOrWhiteSpace(assignee) ? null : assignee, StatusCategory = string.IsNullOrWhiteSpace(statusCategory) ? null : statusCategory,
            Priority = string.IsNullOrWhiteSpace(priority) ? null : priority, IncludeArchived = string.Equals(includeArchived, "true", StringComparison.OrdinalIgnoreCase), Page = 1, PageSize = 500
        };
        var result = await tasks.QueryAsync(query, actor);
        return result.Items.Select(i => new TaskWorkRow(i.Id, i.Key ?? "#" + i.Id, i.WorkSpaceKey ?? localizer["Tm_Personal"].Value, i.Title, i.TypeName == null ? null : TaskManagerUi.Name(i.TypeName, i.TypeNameFa), TaskManagerUi.StatusLabel(localizer, i),
            TaskManagerUi.Priority(localizer, i.Priority), i.AssigneeName ?? localizer["Tm_Unassigned"].Value, i.DueDate, i.IsOverdue, i.StoryPoints, !i.IsActive)).ToList();
    }

    public async Task<List<TaskChoice>> SpaceOptionsAsync()
        => [new TaskChoice("", localizer["Tm_Any"].Value), .. (await tasks.GetAccessibleSpacesAsync(await ActorAsync())).Select(s => new TaskChoice(s.Key, s.Key + " - " + TaskManagerUi.Name(s.Name, s.NameFa)))];

    public async Task<List<TaskChoice>> AllWorkAssigneeOptionsAsync()
        => [new TaskChoice("", localizer["Tm_Any"].Value), new TaskChoice("me", localizer["Tm_Me"].Value), new TaskChoice("unassigned", localizer["Tm_Unassigned"].Value),
            .. (await tasks.GetEligibleAssigneesAsync(null, await ActorAsync())).Candidates.Select(e => new TaskChoice(e.EmployeeId.ToString(), e.Name))];

    public Task<List<TaskChoice>> StatusCategoryOptionsAsync()
        => Task.FromResult(new[] { "", "NotDone", "ToDo", "InProgress", "Done" }.Select(c => new TaskChoice(c, c == "" ? localizer["Tm_Any"].Value : localizer["Tm_Category_" + c].Value)).ToList());

    // the admin pages: system administrators only (the old controller answered Forbid)
    private async Task<TaskActor> AdminActorAsync()
    {
        var actor = await ActorAsync();
        if (!actor.IsSystemAdmin) throw new UnauthorizedAccessException();
        return actor;
    }

    public async Task<List<TaskAdminSpaceRow>> AdminSpacesAsync()
        => (await spaces.GetAllForAdminAsync(await AdminActorAsync())).Select(s => new TaskAdminSpaceRow(s.Id, s.Key, TaskManagerUi.Name(s.Name, s.NameFa), s.OwnerDepartmentName, s.MemberCount, s.OpenItemCount, s.IsActive)).ToList();

    public async Task<List<TaskAdminSpaceHeadRow>> AdminSpaceHeadAsync(int id)
    {
        var m = await spaces.GetAdminAsync(id, await AdminActorAsync());
        return m == null ? [] : [new TaskAdminSpaceHeadRow(m.Space.Id, m.Space.Key, m.Space.Name, m.Space.NameFa, m.Space.Description, m.Space.OwnerDepartmentId, m.Space.IsActive)];
    }

    public async Task<List<TaskAdminMemberRow>> AdminMembersAsync(int id)
        => (await spaces.GetAdminAsync(id, await AdminActorAsync()))?.Members.Select(m => new TaskAdminMemberRow(m.Id, m.Name, localizer["Tm_Role_" + m.Role].Value, m.Role, m.IsActive)).ToList() ?? [];

    public async Task<List<TaskAdminTypeRow>> AdminTypesAsync(int id)
        => (await spaces.GetAdminAsync(id, await AdminActorAsync()))?.Types.Select(t => new TaskAdminTypeRow(t.Id, TaskManagerUi.Name(t.Name, t.NameFa), t.Icon, t.IsSubtask, t.IsActive, !t.IsActive)).ToList() ?? [];

    public async Task<List<TaskAdminStatusRow>> AdminStatusesAsync(int id)
        => (await spaces.GetAdminAsync(id, await AdminActorAsync()))?.Statuses.Select(s => new TaskAdminStatusRow(s.Id, s.Name, s.NameFa, localizer["Tm_Category_" + s.Category].Value, s.Category, s.SortOrder, s.IsActive)).ToList() ?? [];

    // every pair of active statuses (not a status with itself) with "true" when the move is allowed: the cells of the transition matrix; the id of a cell is the space
    public async Task<List<TaskAdminTransitionRow>> AdminTransitionsAsync(int id)
    {
        var m = await spaces.GetAdminAsync(id, await AdminActorAsync());
        if (m == null) return [];
        var active = m.Statuses.Where(x => x.IsActive).ToList();
        return active.SelectMany(from => active.Where(to => to.Id != from.Id).Select(to => new TaskAdminTransitionRow(id, from.Id.ToString(), TaskManagerUi.Name(from.Name, from.NameFa), to.Id.ToString(), TaskManagerUi.Name(to.Name, to.NameFa),
            m.Transitions.Contains((from.Id, to.Id)) ? "true" : "false"))).ToList();
    }

    public async Task<List<TaskChoice>> DepartmentOptionsAsync()
    {
        await AdminActorAsync();
        return [new TaskChoice("", localizer["Tm_NoDepartment"].Value), .. (await spaces.GetDepartmentsAsync()).Select(d => new TaskChoice(d.Id.ToString(), d.Name))];
    }

    public async Task<List<TaskChoice>> NewMemberOptionsAsync(int id)
    {
        var actor = await AdminActorAsync();
        var members = (await spaces.GetAdminAsync(id, actor))?.Members.Select(m => m.EmployeeId).ToHashSet() ?? [];
        return (await spaces.GetActiveEmployeesAsync()).Where(e => !members.Contains(e.EmployeeId)).Select(e => new TaskChoice(e.EmployeeId.ToString(), e.Name)).ToList();
    }

    public Task<List<TaskChoice>> RoleOptionsAsync() => Task.FromResult(WorkSpaceMemberRoles.All.Select(r => new TaskChoice(r, localizer["Tm_Role_" + r].Value)).ToList());

    public Task<List<TaskChoice>> CategoryOptionsAsync() => Task.FromResult(WorkItemStatusCategories.All.Select(c => new TaskChoice(c, localizer["Tm_Category_" + c].Value)).ToList());
}
