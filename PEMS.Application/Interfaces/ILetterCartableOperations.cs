using PEMS.Application.DTOs;

namespace PEMS.Application.Interfaces;

/// <summary>
/// SCRUM-714: the writes of the personal correspondence cartable (display settings, tags, delegations, routing rules, contact groups, reminders) as operations, with the rules and the texts of
/// LetterCartableController: every record is the personal data of the signed-in user, the organizational ones need the Correspondence Approve permission (or a system administrator). A refused
/// command answers its text in the language of the page. The reads (settings, tag tree, delegations, rules, contacts, groups, reminders; SCRUM-8c) are operations too and are served as JSON at /x/cartable-*.
/// </summary>
public sealed record CartableDelegationsAnswer(List<LetterDelegationDto> Given, List<LetterDelegationDto> Received, List<DelegateCandidateDto> Candidates);
public sealed record CartableLookup(int Id, string Name);
public sealed record CartableRulesAnswer(List<LetterRoutingRuleDto> Rules, CartableRuleLookups Lookups);
public sealed record CartableRuleLookups(List<CartableLookup> Projects, List<CartableLookup> Disciplines);

public interface ILetterCartableOperations
{
    /// <summary>Saves the display settings; the hidden columns arrive as one comma separated text.</summary>
    [PEMS.Application.Common.PemsOperation("Cartable", "SaveSettings")]
    Task<(bool Success, string? Error)> SaveSettingsAsync(string? view, string? density, string? hiddenColumns, int frozenColumns, bool openPreviewPane, bool confirmTrash, bool markReadOnOpen, int pageSize);

    [PEMS.Application.Common.PemsOperation("Cartable", "SaveTag")]
    Task<(bool Success, string? Error)> SaveTagAsync(int? tagId, string? name, string? color, int? parentTagId, bool personal, int sortOrder);

    [PEMS.Application.Common.PemsOperation("Cartable", "DeleteTag")]
    Task<(bool Success, string? Error)> DeleteTagAsync(int id);

    [PEMS.Application.Common.PemsOperation("Cartable", "CreateDelegation")]
    Task<(bool Success, string? Error)> CreateDelegationAsync(int delegateUserId, DateTime fromDate, DateTime toDate, string? note);

    [PEMS.Application.Common.PemsOperation("Cartable", "RevokeDelegation")]
    Task<(bool Success, string? Error)> RevokeDelegationAsync(int id);

    [PEMS.Application.Common.PemsOperation("Cartable", "SaveRule")]
    Task<(bool Success, string? Error)> SaveRuleAsync(LetterRoutingRuleDto rule);

    [PEMS.Application.Common.PemsOperation("Cartable", "ToggleRule")]
    Task<(bool Success, string? Error)> ToggleRuleAsync(int id, bool active);

    [PEMS.Application.Common.PemsOperation("Cartable", "DeleteRule")]
    Task<(bool Success, string? Error)> DeleteRuleAsync(int id);

    [PEMS.Application.Common.PemsOperation("Cartable", "SaveGroup")]
    Task<(bool Success, string? Error)> SaveGroupAsync(int? groupId, string? name, bool personal, List<int> employeeIds, List<int> partyIds);

    [PEMS.Application.Common.PemsOperation("Cartable", "DeleteGroup")]
    Task<(bool Success, string? Error)> DeleteGroupAsync(int id);

    [PEMS.Application.Common.PemsOperation("Cartable", "AddReminder")]
    Task<(bool Success, string? Error)> AddReminderAsync(int letterId, DateTime remindAt, string? note);

    [PEMS.Application.Common.PemsOperation("Cartable", "CompleteReminder")]
    Task<(bool Success, string? Error)> CompleteReminderAsync(int id);

    [PEMS.Application.Common.PemsOperation("Cartable", "DeleteReminder")]
    Task<(bool Success, string? Error)> DeleteReminderAsync(int id);

    /// <summary>The display settings of the signed-in user.</summary>
    [PEMS.Application.Common.PemsOperation("Cartable", "Settings")]
    Task<PEMS.Application.Common.LetterUserSettingsRules.Settings> SettingsAsync();

    /// <summary>The tag tree with counts; the organizational tags and the confidential counts need the Correspondence Approve permission.</summary>
    [PEMS.Application.Common.PemsOperation("Cartable", "Tags")]
    Task<List<LetterTagNodeDto>> TagsAsync();

    [PEMS.Application.Common.PemsOperation("Cartable", "Delegations")]
    Task<CartableDelegationsAnswer> DelegationsAsync();

    [PEMS.Application.Common.PemsOperation("Cartable", "Rules")]
    Task<CartableRulesAnswer> RulesAsync();

    [PEMS.Application.Common.PemsOperation("Cartable", "Contacts")]
    Task<List<LetterContactDto>> ContactsAsync(string? q = null);

    [PEMS.Application.Common.PemsOperation("Cartable", "Groups")]
    Task<List<LetterContactGroupDto>> GroupsAsync();

    [PEMS.Application.Common.PemsOperation("Cartable", "Reminders")]
    Task<List<LetterReminderDto>> RemindersAsync(bool includeDone = false);
}
