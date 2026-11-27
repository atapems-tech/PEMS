using Microsoft.Extensions.Localization;
using PEMS.Application.Interfaces;
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
}
