using System.Runtime.CompilerServices;
using PEMS.Web.Controllers;

namespace PEMS.Tests.Web;

/// <summary>
/// طبق «PEMS Global Three-Theme Design System»: محصول اکنون دقیقاً سه تم قابل‌انتخاب دارد -
/// Light/Dark/Persian Turquoise. این تست‌ها تضمین می‌کنند: (۱) ThemeController دقیقاً همین سه
/// مقدار را می‌پذیرد و هر مقدار نامعتبر/قدیمی (ocean/warm/midnight یا هر رشته‌ی دیگر) بی‌صدا
/// به Light برمی‌گردد - نه خطا می‌دهد و نه چیزی می‌شکند؛ (۲) توکن‌های تم در site.css برای هر سه
/// تم تعریف شده‌اند؛ (۳) کلید محلی‌سازی تم تازه در هر دو زبان وجود دارد.
/// </summary>
public class ThemeSystemTests
{
    private static string Root() => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Here())!, "..", ".."));
    private static string Here([CallerFilePath] string p = "") => p;
    private static string Web(params string[] parts) => File.ReadAllText(Path.Combine(new[] { Root(), "PEMS.Web" }.Concat(parts).ToArray()));

    [Fact]
    public void AllowedThemes_IsExactlyLightDarkTurquoise()
    {
        Assert.Equal(new[] { "light", "dark", "turquoise" }, ThemeController.AllowedThemes);
    }

    [Theory]
    [InlineData("light", "light")]
    [InlineData("dark", "dark")]
    [InlineData("turquoise", "turquoise")]
    [InlineData("ocean", "light")]
    [InlineData("warm", "light")]
    [InlineData("midnight", "light")]
    [InlineData(null, "light")]
    [InlineData("not-a-theme", "light")]
    public void Normalize_MapsToAllowedTheme_OrFallsBackToLight(string? input, string expected)
    {
        Assert.Equal(expected, ThemeController.Normalize(input));
    }

    // Light/Dark token blocks moved from site.css to PEMS.UI/wwwroot/tokens.css (SCRUM-29)
    private static string Tokens() => File.ReadAllText(Path.Combine(Root(), "PEMS.UI", "wwwroot", "tokens.css"));

    [Fact]
    public void SiteCss_DefinesTokensForAllThreeThemes()
    {
        var css = Tokens() + Web("wwwroot", "css", "site.css");

        Assert.Contains(":root {", css);
        Assert.Contains("[data-theme=\"dark\"]", css);
        Assert.Contains("[data-theme=\"turquoise\"]", css);

        // --pems-primary باید در هر سه محل override/تعریف شود، نه فقط یک‌بار
        var primaryCount = System.Text.RegularExpressions.Regex.Matches(css, "--pems-primary:").Count;
        Assert.True(primaryCount >= 3, $"Expected --pems-primary to be set for root/dark/turquoise, found {primaryCount} occurrences.");
    }

    [Fact]
    public void SiteCss_ChartTokens_ExistForRootAndTurquoise()
    {
        var css = Tokens() + Web("wwwroot", "css", "site.css");
        Assert.Contains("--pems-chart-primary:", css);
        Assert.Contains("--pems-chart-positive:", css);
        Assert.Contains("--pems-chart-negative:", css);
    }

    [Fact]
    public void Layout_ThemeDropdown_OffersExactlyThreeOptions()
    {
        var layout = Web("Views", "Shared", "_Layout.cshtml");
        Assert.Contains("\"light\"", layout);
        Assert.Contains("\"dark\"", layout);
        Assert.Contains("\"turquoise\"", layout);
        Assert.DoesNotContain("\"ocean\"", layout);
        Assert.DoesNotContain("\"warm\"", layout);
        Assert.DoesNotContain("\"midnight\"", layout);
    }

    [Fact]
    public void PemsChartsJs_ReadsSemanticTokens_NotHardCodedHex()
    {
        var js = Web("wwwroot", "js", "pems-charts.js");
        Assert.Contains("--pems-chart-primary", js);
        Assert.Contains("PemsChartColors", js);
    }

    [Fact]
    public void ThemeLocalizationKey_ExistsInBothLanguages()
    {
        var en = Web("Resources", "SharedResource.resx");
        var fa = Web("Resources", "SharedResource.fa.resx");
        Assert.Contains("data name=\"Theme_Turquoise\"", en);
        Assert.Contains("data name=\"Theme_Turquoise\"", fa);
    }
}
