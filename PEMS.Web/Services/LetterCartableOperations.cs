using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using PEMS.Application.Common;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Context;
using PEMS.Web.Security;
using PEMS.Web.Resources;

namespace PEMS.Web.Services;

/// <summary>SCRUM-714: the writes of the personal correspondence cartable over its services (the rules and texts of LetterCartableController).</summary>
public sealed class LetterCartableOperations(ILetterTagTreeService tags, ILetterUserSettingsService settings, ILetterDelegationService delegations, ILetterRoutingRuleService rules,
    ILetterContactService contacts, ILetterPersonalReminderService reminders, ICurrentUserService currentUser, IPermissionService permissions, IStringLocalizer<SharedResource> localizer, PEMSDbContext context) : ILetterCartableOperations
{
    private int UserId => currentUser.UserId ?? 0;

    private async Task<bool> CanApproveAsync() => currentUser.Roles.Contains("SystemAdmin") || await permissions.HasPermissionAsync(currentUser.Roles, "Correspondence", "Approve");

    private string T(string? key) => string.IsNullOrEmpty(key) ? "" : (localizer[key].ResourceNotFound ? key : localizer[key].Value);

    private (bool Success, string? Error) Res(CartableOpResult r) => r.Success ? (true, null) : (false, T(r.ErrorKey));

    private (bool Success, string? Error) Res(TagTreeOpResult r) => r.Success ? (true, null) : (false, T(r.ErrorKey));

    public async Task<(bool Success, string? Error)> SaveSettingsAsync(string? view, string? density, string? hiddenColumns, int frozenColumns, bool openPreviewPane, bool confirmTrash, bool markReadOnOpen, int pageSize)
    {
        await settings.SaveAsync(UserId, new LetterUserSettingsRules.Settings
        {
            View = view ?? "flat", Density = density ?? "normal",
            HiddenColumns = (hiddenColumns ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(),
            FrozenColumns = frozenColumns, OpenPreviewPane = openPreviewPane, ConfirmTrash = confirmTrash, MarkReadOnOpen = markReadOnOpen, PageSize = pageSize
        });
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> SaveTagAsync(int? tagId, string? name, string? color, int? parentTagId, bool personal, int sortOrder)
    {
        var org = await CanApproveAsync();
        return Res(tagId.HasValue
            ? await tags.UpdateAsync(tagId.Value, name ?? "", color, parentTagId, sortOrder, UserId, org)
            : await tags.CreateAsync(name ?? "", color, parentTagId, personal, UserId, org));
    }

    public async Task<(bool Success, string? Error)> DeleteTagAsync(int id) => Res(await tags.DeactivateAsync(id, UserId, await CanApproveAsync()));

    public async Task<(bool Success, string? Error)> CreateDelegationAsync(int delegateUserId, DateTime fromDate, DateTime toDate, string? note)
        => Res(await delegations.CreateAsync(UserId, delegateUserId, fromDate, toDate, note));

    public async Task<(bool Success, string? Error)> RevokeDelegationAsync(int id) => Res(await delegations.RevokeAsync(id, UserId));

    public async Task<(bool Success, string? Error)> SaveRuleAsync(LetterRoutingRuleDto rule) => Res(await rules.SaveAsync(rule, UserId));

    public async Task<(bool Success, string? Error)> ToggleRuleAsync(int id, bool active) => Res(await rules.SetActiveAsync(id, active, UserId));

    public async Task<(bool Success, string? Error)> DeleteRuleAsync(int id) => Res(await rules.DeleteAsync(id, UserId));

    public async Task<(bool Success, string? Error)> SaveGroupAsync(int? groupId, string? name, bool personal, List<int> employeeIds, List<int> partyIds)
        => Res(await contacts.SaveGroupAsync(groupId, name ?? "", personal, employeeIds, partyIds, UserId, await CanApproveAsync()));

    public async Task<(bool Success, string? Error)> DeleteGroupAsync(int id) => Res(await contacts.DeleteGroupAsync(id, UserId, await CanApproveAsync()));

    public async Task<(bool Success, string? Error)> AddReminderAsync(int letterId, DateTime remindAt, string? note)
        => Res(await reminders.AddAsync(letterId, remindAt, note, UserId, await CanApproveAsync()));

    public async Task<(bool Success, string? Error)> CompleteReminderAsync(int id) => Res(await reminders.CompleteAsync(id, UserId));

    public async Task<(bool Success, string? Error)> DeleteReminderAsync(int id) => Res(await reminders.DeleteAsync(id, UserId));

    public Task<PEMS.Application.Common.LetterUserSettingsRules.Settings> SettingsAsync() => settings.GetAsync(UserId);

    public async Task<List<LetterTagNodeDto>> TagsAsync()
    {
        var approve = await CanApproveAsync();
        return await tags.GetTreeAsync(UserId, approve, withCounts: true, approve);
    }

    public async Task<CartableDelegationsAnswer> DelegationsAsync()
    {
        var (given, received) = await delegations.GetAsync(UserId);
        return new CartableDelegationsAnswer(given, received, await delegations.GetCandidatesAsync(UserId));
    }

    public async Task<CartableRulesAnswer> RulesAsync()
    {
        var projects = await context.Projects.Where(p => p.IsActive).OrderBy(p => p.ProjectName).Select(p => new CartableLookup(p.ProjectId, p.ProjectName)).Take(300).ToListAsync();
        var disciplines = await context.Disciplines.Where(d => d.IsActive).OrderBy(d => d.Name).Select(d => new CartableLookup(d.DisciplineId, d.Name)).ToListAsync();
        return new CartableRulesAnswer(await rules.GetAsync(UserId), new CartableRuleLookups(projects, disciplines));
    }

    public Task<List<LetterContactDto>> ContactsAsync(string? q = null) => contacts.SearchAsync(q);

    public async Task<List<LetterContactGroupDto>> GroupsAsync() => await contacts.GetGroupsAsync(UserId, await CanApproveAsync());

    public Task<List<LetterReminderDto>> RemindersAsync(bool includeDone = false) => reminders.GetAsync(UserId, includeDone);
}
