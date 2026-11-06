namespace PEMS.Tests.Web;

/// <summary>
/// SCRUM-526, SCRUM-527: a merge (97e11c85) dropped three lines from the main layout without any test failing.
/// The stage diagrams lost their stylesheet, and every data-pems action (the notification bell, Back, print, check-all) lost its script.
/// Every layout that renders pages with these features must load them.
/// </summary>
public class MainLayoutAssetsTests
{
    [Theory]
    [InlineData("_Layout.cshtml", "~/css/pems-stage-diagram.css")]
    [InlineData("_Layout.cshtml", "~/js/pems-actions.js")]
    [InlineData("_Layout.cshtml", "<pems-script-templates set=\"shell\">")]
    [InlineData("_LayoutEmbed.cshtml", "~/js/pems-actions.js")]
    [InlineData("_LayoutEmbed.cshtml", "<pems-script-templates set=\"shell\">")]
    [InlineData("_AuthLayout.cshtml", "~/js/pems-actions.js")]
    public void Layout_LoadsWhatItsPagesUse(string layout, string asset) =>
        Assert.Contains(asset, File.ReadAllText(Path.Combine(RepoRoot(), "PEMS.Web", "Views", "Shared", layout)));

    [Fact]
    public void TheNotificationBell_RunsThroughTheActionsScript()
    {
        var layout = File.ReadAllText(Path.Combine(RepoRoot(), "PEMS.Web", "Views", "Shared", "_Layout.cshtml"));
        Assert.Contains("data-pems-click=\"call\" data-pems-fn=\"loadNotifDropdown\"", layout);
        // the actions script must come before the page scripts that rely on it
        Assert.True(layout.IndexOf("~/js/pems-actions.js", StringComparison.Ordinal) < layout.IndexOf("@await RenderSectionAsync(\"Scripts\"", StringComparison.Ordinal));
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "PEMS.Web", "PEMS.Web.csproj"))) dir = dir.Parent;
        Assert.NotNull(dir);
        return dir!.FullName;
    }
}
