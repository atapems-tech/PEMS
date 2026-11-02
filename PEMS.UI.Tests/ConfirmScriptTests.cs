using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace PEMS.UI.Tests;

/// <summary>SCRUM-483 / #483: the shared confirm dialog (pems-confirm.js) is loaded once by both layouts, its texts are inputs from the layout, and it stays inline-handler free.</summary>
public sealed class ConfirmScriptTests
{
    private static string Here([CallerFilePath] string p = "") => p;
    private static string Root() => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Here())!, ".."));
    private static string Read(params string[] parts) => File.ReadAllText(Path.Combine(new[] { Root() }.Concat(parts).ToArray()));

    [Theory]
    [InlineData("_Layout.cshtml")]
    [InlineData("_LayoutEmbed.cshtml")]
    public void The_layouts_load_the_script_once_after_the_overlay_script_and_carry_the_texts(string layout)
    {
        var html = Read("PEMS.Web", "Views", "Shared", layout);
        Assert.Equal(1, Regex.Matches(html, Regex.Escape("_content/PEMS.UI/pems-confirm.js")).Count);
        Assert.True(html.IndexOf("pems-overlay.js", StringComparison.Ordinal) < html.IndexOf("pems-confirm.js", StringComparison.Ordinal));
        foreach (var name in new[] { "title", "ok", "cancel" }) Assert.Contains($"data-pems-confirm-{name}=\"@Localizer[\"Confirm_", html);
    }

    [Theory]
    [InlineData("SharedResource.resx")]
    [InlineData("SharedResource.fa.resx")]
    public void The_dialog_texts_exist_in_both_languages(string resource)
    {
        var xml = Read("PEMS.Web", "Resources", resource);
        foreach (var key in new[] { "Confirm_Title", "Confirm_Ok", "Confirm_Cancel" }) Assert.Contains($"name=\"{key}\"", xml);
    }

    [Fact]
    public void The_script_asks_before_a_click_or_a_submit_and_exposes_a_promise_api()
    {
        var js = Read("PEMS.UI", "wwwroot", "pems-confirm.js");
        Assert.Contains("data-pems-confirm", js);
        Assert.Contains("addEventListener('click'", js);
        Assert.Contains("addEventListener('submit'", js);
        Assert.Contains("requestSubmit", js);
        Assert.Contains("window.PemsDialog", js);
        Assert.DoesNotContain("window.confirm", js);
        Assert.DoesNotContain("onclick=", js);
    }

    [Fact]
    public void No_view_asks_with_the_browser_confirm_dialog_in_a_simple_inline_handler()
    {
        var offenders = Directory.EnumerateFiles(Path.Combine(Root(), "PEMS.Web"), "*.cshtml", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .Where(f => Regex.IsMatch(File.ReadAllText(f), @"on(click|submit)=""return confirm\('"))
            .Select(f => Path.GetRelativePath(Root(), f)).ToList();
        Assert.True(offenders.Count == 0, "Use data-pems-confirm=\"text\" instead of an inline confirm in: " + string.Join(", ", offenders));
    }
}
