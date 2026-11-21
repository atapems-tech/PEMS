using Microsoft.Extensions.Localization;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Web.Resources;
using PEMS.Web.Security;

namespace PEMS.Web.Services;

/// <summary>SCRUM-756: the scalar commands of TaskManagerController (same services, same messages). The permission floor (Task.Edit) is the operation's; the object-level rules are the services'.</summary>
public sealed class TaskManagerOperations(ITaskManagerService tasks, IWorkSpaceService spaces, ICurrentUserService currentUser, IStringLocalizer<SharedResource> localizer) : ITaskManagerOperations
{
    private Task<TaskActor> ActorAsync() => tasks.ResolveActorAsync(currentUser.UserId, currentUser.Roles, currentUser.FullName);

    private (bool Success, string? Error) Outcome(TmResult r) => r.Success ? (true, null) : (false, localizer[r.ErrorKey!].Value);

    // a space administration command: system administrators only
    private async Task<(bool Success, string? Error)> AdminAsync(Func<TaskActor, Task<TmResult>> run)
    {
        var actor = await ActorAsync();
        if (!actor.IsSystemAdmin) throw new UnauthorizedAccessException();
        return Outcome(await run(actor));
    }

    public async Task<(bool Success, string? Error)> AssignAsync(int id, int? assigneeEmployeeId = null, string? rowVersion = null) => Outcome(await tasks.AssignAsync(id, assigneeEmployeeId, rowVersion, await ActorAsync()));
    public async Task<(bool Success, string? Error)> TransitionAsync(int id, int? statusId = null, string? legacyStatus = null, string? note = null, string? rowVersion = null) => Outcome(await tasks.TransitionAsync(id, statusId, legacyStatus, note, rowVersion, await ActorAsync()));
    public async Task<(bool Success, string? Error)> ArchiveAsync(int id, bool archived = true) => Outcome(await tasks.SetArchivedAsync(id, archived, await ActorAsync()));
    public async Task<(bool Success, string? Error)> RankAsync(int id, bool up) => Outcome(await tasks.MoveRankAsync(id, up, await ActorAsync()));
    public async Task<(bool Success, string? Error)> ReorderAsync(int workSpaceId, int? sprintId, List<int> ids) => Outcome(await tasks.ReorderAsync(workSpaceId, sprintId, ids ?? [], await ActorAsync()));
    public async Task<(bool Success, string? Error)> SetSprintAsync(int id, int? sprintId = null) => Outcome(await tasks.SetSprintAsync(id, sprintId > 0 ? sprintId : null, await ActorAsync()));

    public async Task<(bool Success, string? Error)> BulkAsync(List<int> ids, string bulkAction, string? value = null)
    {
        if (ids == null || ids.Count == 0) return (false, localizer["Tm_Err_NothingSelected"].Value);
        var r = await tasks.BulkAsync(ids, bulkAction, value, await ActorAsync());
        if (r.Failed.Count == 0) return (true, null);
        var failed = string.Format(localizer["Tm_Msg_BulkFailed"].Value, r.Failed.Count) + " " +
            string.Join("; ", r.Failed.Take(5).Select(f => { var parts = f.Split(": ", 2); return parts.Length == 2 ? parts[0] + ": " + localizer[parts[1]].Value : f; }));
        return (false, r.Succeeded > 0 ? string.Format(localizer["Tm_Msg_BulkDone"].Value, r.Succeeded) + " " + failed : failed);
    }

    public async Task<(bool Success, string? Error)> CommentAsync(int id, string body) => Outcome(await tasks.AddCommentAsync(id, body ?? "", await ActorAsync()));
    public async Task<(bool Success, string? Error)> DeleteCommentAsync(int commentId) => Outcome(await tasks.DeleteCommentAsync(commentId, await ActorAsync()));
    public async Task<(bool Success, string? Error)> AddLinkAsync(int id, string linkType, string target) => Outcome(await tasks.AddLinkAsync(id, linkType ?? "", target ?? "", await ActorAsync()));
    public async Task<(bool Success, string? Error)> RemoveLinkAsync(int linkId) => Outcome(await tasks.RemoveLinkAsync(linkId, await ActorAsync()));
    public async Task<(bool Success, string? Error)> WatchAsync(int id, bool watch) => Outcome(await tasks.SetWatchingAsync(id, watch, await ActorAsync()));

    public async Task<(bool Success, string? Error)> CreateSprintAsync(int workSpaceId, string name, string? goal = null, DateTime? startDate = null, DateTime? endDate = null) => Outcome(await spaces.CreateSprintAsync(workSpaceId, name ?? "", goal, startDate, endDate, await ActorAsync()));
    public async Task<(bool Success, string? Error)> UpdateSprintAsync(int sprintId, string name, string? goal = null, DateTime? startDate = null, DateTime? endDate = null) => Outcome(await spaces.UpdateSprintAsync(sprintId, name ?? "", goal, startDate, endDate, await ActorAsync()));
    public async Task<(bool Success, string? Error)> StartSprintAsync(int sprintId) => Outcome(await spaces.StartSprintAsync(sprintId, await ActorAsync()));
    public async Task<(bool Success, string? Error)> CompleteSprintAsync(int sprintId, int? moveTo = null) => Outcome(await spaces.CompleteSprintAsync(sprintId, moveTo > 0 ? moveTo : null, await ActorAsync()));

    public Task<(bool Success, string? Error)> AddMemberAsync(int workSpaceId, int employeeId, string role) => AdminAsync(a => spaces.AddMemberAsync(workSpaceId, employeeId, role ?? "", a));
    public Task<(bool Success, string? Error)> SetMemberAsync(int memberId, string role, bool isActive = false) => AdminAsync(a => spaces.SetMemberAsync(memberId, role ?? "", isActive, a));
    public Task<(bool Success, string? Error)> AddStatusAsync(int workSpaceId, string name, string category, string? nameFa = null) => AdminAsync(a => spaces.AddStatusAsync(workSpaceId, name ?? "", nameFa, category ?? "", a));
    public Task<(bool Success, string? Error)> EditStatusAsync(int statusId, string name, string category, int sortOrder, string? nameFa = null, bool isActive = false) => AdminAsync(a => spaces.UpdateStatusAsync(statusId, name ?? "", nameFa, category ?? "", sortOrder, isActive, a));
    public Task<(bool Success, string? Error)> SetTransitionAsync(int workSpaceId, int fromStatusId, int toStatusId, bool allowed = false) => AdminAsync(a => spaces.SetTransitionAsync(workSpaceId, fromStatusId, toStatusId, allowed, a));
    public Task<(bool Success, string? Error)> AddTypeAsync(int workSpaceId, string name, string icon, string? nameFa = null, bool isSubtask = false) => AdminAsync(a => spaces.AddTypeAsync(workSpaceId, name ?? "", nameFa, icon ?? "", isSubtask, a));
    public Task<(bool Success, string? Error)> SetTypeActiveAsync(int typeId, bool isActive = false) => AdminAsync(a => spaces.SetTypeActiveAsync(typeId, isActive, a));
}
