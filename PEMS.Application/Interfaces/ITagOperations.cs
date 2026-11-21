namespace PEMS.Application.Interfaces;

/// <summary>One tag of the tags page: the colour is a Bootstrap colour name, <c>LetterCount</c> how many letters carry it.</summary>
public sealed record TagRow(int TagId, string Name, string Color, int LetterCount);

/// <summary>
/// The tags page as operations (SCRUM-754): the list, the new tag and the delete of TagController over the same ITagService; the viewer and the actor are the current user. A refused delete says why
/// (sub-tags, someone else's personal tag, not found: SCRUM-550).
/// </summary>
public interface ITagOperations
{
    [PEMS.Application.Common.PemsOperation("Tag", "List")]
    Task<List<TagRow>> ListAsync();

    [PEMS.Application.Common.PemsOperation("Tag", "Create")]
    Task<(bool Success, string? Error)> CreateAsync(string name, string? color = null);

    [PEMS.Application.Common.PemsOperation("Tag", "Delete")]
    Task<(bool Success, string? Error)> DeleteAsync(int tagId);
}
