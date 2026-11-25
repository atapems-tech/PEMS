using System.Globalization;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;

namespace PEMS.Web.Services;

/// <summary>SCRUM-679: the lookups of the incoming registration form over the incoming correspondence service (what DCCController answered as JSON).</summary>
public sealed class DccOperations(IIncomingCorrespondenceService incoming, ICurrentUserService currentUser, IPermissionService permissions) : IDccOperations
{
    private async Task<bool> CanViewConfidentialAsync()
        => currentUser.Roles.Contains("SystemAdmin") || await permissions.HasPermissionAsync(currentUser.Roles, "Correspondence", "Approve");

    private static DateTime? TryParseIsoDate(string? text)
        => DateTime.TryParseExact(text?.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;

    public Task<List<PartySearchItemDto>> PartiesAsync(string? term = null, int? projectId = null) => incoming.SearchPartiesAsync(term, projectId);

    public async Task<PartyThreadDto?> PartyThreadAsync(int partyId) => await incoming.GetPartyThreadAsync(partyId, await CanViewConfidentialAsync(), currentUser.UserId);

    public async Task<List<OpenOutgoingDto>> OutgoingForReplyAsync(int partyId) => await incoming.GetOutgoingForReplyAsync(partyId, await CanViewConfidentialAsync(), currentUser.UserId);

    public Task<List<LinkSearchItemDto>> LinksAsync(string type, string? term = null, int? projectId = null) => incoming.SearchLinksAsync(type, term, projectId);

    public Task<List<PersonSearchItemDto>> PeopleAsync(string? term = null) => incoming.SearchPeopleAsync(term);

    public async Task<DccNumber> NextNumberAsync(int? projectId = null, string? receivedDate = null)
        => new(await incoming.PeekNextNumberAsync(projectId, TryParseIsoDate(receivedDate) ?? DateTime.Today));

    public async Task<DccDuplicate?> CheckDuplicateAsync(int partyId, string? reference = null)
    {
        var hit = await incoming.FindDuplicateAsync(partyId, reference ?? "");
        return hit is null ? null : new DccDuplicate(hit.Value.LetterId, hit.Value.LetterNumber, "/Letter/Details/" + hit.Value.LetterId);
    }
}
