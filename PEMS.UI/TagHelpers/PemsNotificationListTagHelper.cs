using Microsoft.AspNetCore.Razor.TagHelpers;
using PEMS.UI.Components;
using PEMS.UI.Rendering;

namespace PEMS.UI.TagHelpers;

/// <summary>MVC facade for <see cref="PemsNotificationList"/>: <c>&lt;pems-notification-list /&gt;</c> in the layout writes the list of the bell dropdown (SCRUM-841).</summary>
[HtmlTargetElement("pems-notification-list")]
[PemsFacade(UiKeys.NotificationList)]
public sealed class PemsNotificationListTagHelper(IPemsComponentRenderer renderer) : TagHelper
{
    [HtmlAttributeName("source")] public string Source { get; set; } = "/x/notification-dropdown";
    [HtmlAttributeName("view-all-url")] public string ViewAllUrl { get; set; } = "/p/notifications";

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        BlockTagHelper.Replace(output, await renderer.RenderAsync<PemsNotificationList>(new Dictionary<string, object?>
        {
            [nameof(PemsNotificationList.Options)] = new NotificationListOptions { Source = Source, ViewAllUrl = ViewAllUrl }
        }));
    }
}
