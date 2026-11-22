using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using PEMS.UI.Abstractions;
using PEMS.UI.Components;
using PEMS.UI.Manifest;

namespace PEMS.UI.Tests;

/// <summary>SCRUM-778: the chat panel writes one element with the keys of a data source and two actions; the script builds everything else from registered templates and posts like the other engines.</summary>
public sealed class ChatPanelTests
{
    private static string Here([CallerFilePath] string p = "") => p;
    private static string Read(params string[] parts) => File.ReadAllText(Path.Combine(new[] { Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Here())!, "..")) }.Concat(parts).ToArray()));

    private static string Render(string culture = "en", string? json = null)
    {
        var options = JsonSerializer.Deserialize<ChatPanelOptions>(json ?? ComponentSamples.Get("chat-panel")!.ToJsonString(), PemsJson.Options);
        return UiTestRenderer.Instance.RenderAsync("chat-panel", new Dictionary<string, object?> { ["Options"] = options }, new CultureInfo(culture)).GetAwaiter().GetResult();
    }

    [Fact]
    public void The_element_carries_the_source_and_the_actions_as_paths_of_this_site()
    {
        var html = System.Net.WebUtility.HtmlDecode(Render());
        Assert.Contains("data-pems-chat=\"\"", html);
        Assert.Contains("id=\"chat\"", html);
        Assert.Contains("\"history\":\"/d/chat-history-grid\"", html);
        Assert.Contains("\"ask\":\"/a/chat-ask\"", html);
        Assert.Contains("\"clear\":\"/a/chat-clear\"", html);
        Assert.Contains("\"askField\":\"message\"", html);
    }

    [Fact]
    public void Without_a_clear_action_there_is_no_clear_path_and_the_texts_follow_the_language()
    {
        var html = System.Net.WebUtility.HtmlDecode(Render("fa", """{"id":"c","historySource":"h","askAction":"a"}"""));
        Assert.DoesNotContain("\"clear\":\"/a/", html);
        Assert.Contains("ارسال", html);
        Assert.Contains("\"send\":\"", html);
    }

    [Fact]
    public void The_script_posts_json_with_the_token_builds_from_templates_and_never_writes_markup()
    {
        var js = Read("PEMS.UI", "wwwroot", "pems-chat.js");
        Assert.DoesNotContain("createElement", js);
        Assert.DoesNotContain("innerHTML", js);
        Assert.DoesNotMatch("https?://", js);
        Assert.Contains("RequestVerificationToken", js);
        var razor = Read("PEMS.UI", "Components", "PemsScriptTemplates.razor");
        foreach (var name in new[] { "chat-root", "chat-msg" })
        {
            Assert.Contains("'" + name + "'", js);
            Assert.Contains($"data-pems-template=\"{name}\"", razor);
        }
        Assert.Contains("pems-chat.js", Read("PEMS.Web", "Views", "Shared", "_Layout.cshtml"));
        var css = Read("PEMS.UI", "wwwroot", "pems-ui.css");
        Assert.Contains(".pems-chat__msg.is-user { align-self: flex-end;", css);
    }

    [Fact]
    public void The_chat_assistant_page_is_this_component_and_names_shipped_definitions()
    {
        var page = Read("PEMS.Persistence", "Definitions", "System", "page", "core", "chat-assistant.json");
        Assert.Contains("\"component\": \"chat-panel\"", page);
        foreach (var (kind, key) in new[] { ("data-source", "chat-history-grid"), ("action", "chat-ask"), ("action", "chat-clear") })
        {
            Assert.Contains($"\"{key}\"", page);
            Assert.True(File.Exists(Path.Combine(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Here())!, "..")), "PEMS.Persistence", "Definitions", "System", kind, "core", key + ".json")));
        }
    }
}
