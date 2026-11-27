using System.Globalization;
using System.Text.Json;
using PEMS.UI.Abstractions;
using PEMS.UI.Components;
using PEMS.UI.Manifest;

namespace PEMS.UI.Tests;

/// <summary>SCRUM-841: the status panel (the friendly page of a 4xx or 5xx answer) and the notification list (the bell dropdown) are components; their pages and scripts are definitions and component scripts.</summary>
public sealed class ShellPiecesTests
{
    private static string Render(string key, string sample, string culture = "en")
    {
        var type = key == "status-panel" ? typeof(StatusPanelOptions) : typeof(NotificationListOptions);
        var options = JsonSerializer.Deserialize(ComponentSamples.Get(key)!.ToJsonString(), type, PemsJson.Options);
        return UiTestRenderer.Instance.RenderAsync(key, new Dictionary<string, object?> { ["Options"] = options }, new CultureInfo(culture)).GetAwaiter().GetResult();
    }

    [Fact]
    public void The_status_panel_without_a_request_shows_the_fallback_with_the_back_and_home_buttons()
    {
        var html = System.Net.WebUtility.HtmlDecode(Render("status-panel", ""));
        Assert.Contains("data-status-code=\"500\"", html);
        Assert.Contains("aria-labelledby=\"statusTitle\"", html);
        Assert.Contains("id=\"statusTitle\"", html);
        Assert.Contains("data-pems-click=\"back\"", html);
        Assert.Contains("Something went wrong", html);
    }

    [Fact]
    public void The_status_panel_texts_follow_the_language()
        => Assert.Contains("خطایی رخ داد", System.Net.WebUtility.HtmlDecode(Render("status-panel", "", "fa")));

    [Fact]
    public void The_notification_list_carries_its_source_the_buttons_of_the_hub_and_the_view_all_link()
    {
        var html = System.Net.WebUtility.HtmlDecode(Render("notification-list", ""));
        Assert.Contains("data-pems-notification-list=\"\"", html);
        Assert.Contains("data-source=\"/x/notification-dropdown\"", html);
        Assert.Contains("data-notif-action=\"markAll\"", html);
        Assert.Contains("data-notification-items", html);
        Assert.Contains("href=\"/p/notifications\"", html);
    }
}
