using Microsoft.Extensions.Localization;
using PEMS.Application.Interfaces;
using PEMS.Web.Resources;
using PEMS.Web.Security;

namespace PEMS.Web.Services;

/// <summary>SCRUM-754: the tags page of TagController over ITagService (same messages, same ownership rules).</summary>
public sealed class TagOperations(ITagService service, ICurrentUserService currentUser, IStringLocalizer<SharedResource> localizer) : ITagOperations
{
    private static readonly string[] Colors = ["secondary", "primary", "success", "danger", "warning", "info", "dark"];

    public async Task<List<TagRow>> ListAsync()
        => (await service.GetAllAsync(currentUser.UserId)).Select(t => new TagRow(t.TagId, t.Name, t.Color, t.LetterCount)).ToList();

    public async Task<(bool Success, string? Error)> CreateAsync(string name, string? color = null)
    {
        if (string.IsNullOrWhiteSpace(name)) return (false, "Name is required.");
        await service.CreateAsync(name.Trim(), Colors.Contains(color ?? "") ? color! : "secondary", currentUser.UserId);
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> DeleteAsync(int tagId)
    {
        var outcome = await service.DeleteAsync(tagId, currentUser.UserId);
        if (outcome == TagDeleteOutcome.Deleted) return (true, null);
        var key = outcome switch { TagDeleteOutcome.HasSubTags => "Tg_Err_HasSubTags", TagDeleteOutcome.NotOwner => "Tg_Err_NotOwner", _ => "Tg_Err_NotFound" };
        return (false, localizer[key].Value);
    }
}
