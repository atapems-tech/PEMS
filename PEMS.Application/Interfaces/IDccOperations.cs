using PEMS.Application.DTOs;

namespace PEMS.Application.Interfaces;

/// <summary>The duplicate hint of the incoming registration: the letter that already carries the same sender reference.</summary>
public sealed record DccDuplicate(int LetterId, string LetterNumber, string Url);

/// <summary>The next number the registration would take.</summary>
public sealed record DccNumber(string Number);

/// <summary>
/// SCRUM-679: the lookups of the incoming correspondence registration form (parties, the thread of a party, the open outgoing letters to answer, links, people, the next number and the duplicate hint),
/// read as data sources. The old controller needed the Correspondence Create permission for each; the confidential letters of a thread need Approve (or a system administrator), as before. The
/// registration itself (several files with roles, a field by field answer with the duplicate letter) stays on the controller until an action can answer structured errors.
/// </summary>
public interface IDccOperations
{
    [PEMS.Application.Common.PemsOperation("Dcc", "Parties", "Correspondence.Create")]
    Task<List<PartySearchItemDto>> PartiesAsync(string? term = null, int? projectId = null);

    [PEMS.Application.Common.PemsOperation("Dcc", "PartyThread", "Correspondence.Create")]
    Task<PartyThreadDto?> PartyThreadAsync(int partyId);

    [PEMS.Application.Common.PemsOperation("Dcc", "OutgoingForReply", "Correspondence.Create")]
    Task<List<OpenOutgoingDto>> OutgoingForReplyAsync(int partyId);

    [PEMS.Application.Common.PemsOperation("Dcc", "Links", "Correspondence.Create")]
    Task<List<LinkSearchItemDto>> LinksAsync(string type, string? term = null, int? projectId = null);

    [PEMS.Application.Common.PemsOperation("Dcc", "People", "Correspondence.Create")]
    Task<List<PersonSearchItemDto>> PeopleAsync(string? term = null);

    [PEMS.Application.Common.PemsOperation("Dcc", "NextNumber", "Correspondence.Create")]
    Task<DccNumber> NextNumberAsync(int? projectId = null, string? receivedDate = null);

    [PEMS.Application.Common.PemsOperation("Dcc", "CheckDuplicate", "Correspondence.Create")]
    Task<DccDuplicate?> CheckDuplicateAsync(int partyId, string? reference = null);
}
