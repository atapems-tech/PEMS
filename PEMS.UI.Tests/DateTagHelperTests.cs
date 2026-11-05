using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Razor.TagHelpers;
using PEMS.UI.TagHelpers;

namespace PEMS.UI.Tests;

/// <summary>SCRUM-225 / #206: the date field posts the Gregorian ISO date whatever calendar the person sees; Jalali pages get the picker pair, the others the native input.</summary>
public sealed class DateTagHelperTests
{
    private static string Here([CallerFilePath] string p = "") => p;
    private static string Root() => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Here())!, ".."));
    private static string Read(params string[] parts) => File.ReadAllText(Path.Combine(new[] { Root() }.Concat(parts).ToArray()));

    private static string Run(PemsDateTagHelper helper, params TagHelperAttribute[] attributes)
    {
        var list = new TagHelperAttributeList(attributes);
        var context = new TagHelperContext("pems-date", new TagHelperAttributeList(), new Dictionary<object, object>(), "c");
        var output = new TagHelperOutput("pems-date", list, (_, _) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent()));
        helper.ProcessAsync(context, output).GetAwaiter().GetResult();
        using var writer = new StringWriter();
        output.WriteTo(writer, HtmlEncoder.Default);
        return writer.ToString();
    }

    private static T WithCulture<T>(string culture, Func<T> action)
    {
        var old = CultureInfo.CurrentUICulture;
        try { CultureInfo.CurrentUICulture = new CultureInfo(culture); return action(); }
        finally { CultureInfo.CurrentUICulture = old; }
    }

    [Fact]
    public void The_gregorian_field_is_the_native_date_input_the_pages_wrote()
        => Assert.Equal("<input class=\"form-control form-control-sm\" required=\"required\" type=\"date\" name=\"due\" value=\"2025-03-20\" min=\"2024-03-20\" max=\"2026-03-19\" />",
            Run(new PemsDateTagHelper(UiTestRenderer.Instance) { Calendar = "gregorian", Scale = "small", Name = "due", Value = "2025-03-20", Min = "2024-03-20", Max = "2026-03-19" }, new TagHelperAttribute("required", "required")));

    [Fact]
    public void A_time_part_or_an_empty_value_is_handled_and_nothing_else_is_written()
    {
        Assert.Contains("value=\"2025-03-20\"", Run(new PemsDateTagHelper(UiTestRenderer.Instance) { Calendar = "gregorian", Name = "d", Value = "2025-03-20T10:30:00" }));
        Assert.DoesNotContain("value=", Run(new PemsDateTagHelper(UiTestRenderer.Instance) { Calendar = "gregorian", Name = "d", Value = "" }));
        Assert.DoesNotContain("value=", Run(new PemsDateTagHelper(UiTestRenderer.Instance) { Calendar = "gregorian", Name = "d", Value = "not a date" }));
    }

    [Fact]
    public void The_jalali_field_shows_the_jalali_date_and_posts_the_iso_date_from_a_hidden_input()
    {
        var html = Run(new PemsDateTagHelper(UiTestRenderer.Instance) { Calendar = "jalali", Name = "due", Value = "2025-03-20", Min = "2024-03-20", Max = "2026-03-20" }, new TagHelperAttribute("id", "due"), new TagHelperAttribute("required", "required"));

        Assert.Contains("type=\"text\"", html);
        Assert.Contains("value=\"1403/12/30\"", html);                       // the last day of the leap year 1403
        Assert.Contains("data-jdp-min-date=\"1403/01/01\"", html);
        Assert.Contains("data-jdp-max-date=\"1404/12/29\"", html);
        Assert.Contains("data-pems-date-min=\"2024-03-20\"", html);          // for the native fallback
        Assert.Contains("data-jdp-target-value-input=\"#due-iso\"", html);
        Assert.Contains("data-jdp-target-value-type=\"gregorian\"", html);
        Assert.Contains("id=\"due\"", html);
        Assert.Contains("required=\"required\"", html);
        var hidden = Regex.Match(html, "<input[^>]*type=\"hidden\"[^>]*>").Value;   // attribute order is not part of the contract
        Assert.Contains("id=\"due-iso\"", hidden);
        Assert.Contains("name=\"due\"", hidden);
        Assert.Contains("value=\"2025-03-20\"", hidden);
        Assert.Equal(1, Regex.Matches(html, "name=\"due\"").Count);          // only the hidden input is posted
    }

    [Theory]
    [InlineData("2025-03-20", "1403/12/30")]
    [InlineData("2025-03-21", "1404/01/01")]
    [InlineData("2024-03-19", "1402/12/29")]
    [InlineData("2024-03-20", "1403/01/01")]
    [InlineData("2029-03-20", "1408/01/01")]
    public void Dates_around_nowruz_and_the_leap_year_convert_the_way_the_browser_does(string iso, string jalali)
        => Assert.Equal(jalali, PemsDateTagHelper.ToJalaliText(iso));

    [Fact]
    public void Without_a_value_the_jalali_field_is_empty_and_has_a_placeholder_and_a_generated_id()
    {
        var html = Run(new PemsDateTagHelper(UiTestRenderer.Instance) { Calendar = "jalali", Name = "due" });
        Assert.DoesNotContain("value=\"1", html);
        Assert.Contains("placeholder=\"1403/01/01\"", html);
        Assert.Matches("data-jdp-target-value-input=\"#pdt[0-9a-f]{8}-iso\"", html);
        Assert.Matches("value=\"\"", html);                                   // the hidden input is posted empty
    }

    [Fact]
    public void Auto_follows_the_page_culture()
    {
        Assert.Contains("type=\"date\"", WithCulture("en", () => Run(new PemsDateTagHelper(UiTestRenderer.Instance) { Name = "d", Value = "2025-03-20" })));
        var fa = WithCulture("fa", () => Run(new PemsDateTagHelper(UiTestRenderer.Instance) { Name = "d", Value = "2025-03-20" }));
        Assert.Contains("data-jdp", fa);
        Assert.Contains("1403/12/30", fa);
    }

    [Fact]
    public void A_name_with_markup_is_encoded()
    {
        var html = Run(new PemsDateTagHelper(UiTestRenderer.Instance) { Calendar = "jalali", Name = "a\"><script>x</script>", Value = "2025-03-20" });
        Assert.DoesNotContain("<script>", html);
    }

    [Fact]
    public void The_layouts_load_the_library_the_script_and_the_style_once_and_carry_the_invalid_text()
    {
        foreach (var layout in new[] { "_Layout.cshtml", "_LayoutEmbed.cshtml" })
        {
            var html = Read("PEMS.Web", "Views", "Shared", layout);
            foreach (var file in new[] { "lib/jalalidatepicker/jalalidatepicker.min.js", "lib/jalalidatepicker/jalalidatepicker.min.css", "_content/PEMS.UI/pems-date.js", "_content/PEMS.UI/pems-date.css" })
                Assert.Equal(1, Regex.Matches(html, Regex.Escape(file)).Count);
            Assert.True(html.IndexOf("jalalidatepicker.min.js", StringComparison.Ordinal) < html.IndexOf("pems-date.js", StringComparison.Ordinal), "the library loads before the script");
            Assert.Contains("data-pems-date-invalid=\"@Localizer[\"Date_Invalid\"]\"", html);
        }
        Assert.Contains("name=\"Date_Invalid\"", Read("PEMS.Web", "Resources", "SharedResource.resx"));
        Assert.Contains("name=\"Date_Invalid\"", Read("PEMS.Web", "Resources", "SharedResource.fa.resx"));
    }

    [Fact]
    public void The_script_is_safe_to_ship_it_falls_back_to_the_native_input_and_has_no_inline_handlers()
    {
        var js = Read("PEMS.UI", "wwwroot", "pems-date.js");
        Assert.Contains("function fallback()", js);
        Assert.Contains("setAttribute('type', 'date')", js);
        Assert.Contains("Intl.DateTimeFormat('en-u-ca-persian-nu-latn'", js);   // the browser's own calendar, no calendar code of our own
        Assert.DoesNotContain("onclick=", js);
        Assert.DoesNotContain("innerHTML", js);
        Assert.DoesNotContain("https://", js.Replace("https://github.com", ""));  // offline
        Assert.DoesNotContain("#", Regex.Replace(Read("PEMS.UI", "wwwroot", "pems-date.css"), @"/\*.*?\*/", "", RegexOptions.Singleline));   // tokens only, no literal colours
        Assert.True(File.Exists(Path.Combine(Root(), "PEMS.Web", "wwwroot", "lib", "jalalidatepicker", "LICENSE")), "the vendored library keeps its licence file");
    }
}
