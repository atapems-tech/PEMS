using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace PEMS.UI.Tests;

/// <summary>
/// SCRUM-341 / #327: the adoption ratchet. Counts the hand-written Bootstrap patterns that a PEMS.UI component replaces, per view and per script, and
/// compares them with <c>tools/UiAudit/adoption-baseline.json</c>. A count may never rise (a new file with a pattern counts as a rise); when an adoption
/// task lowers a count, the baseline is lowered in the same change (<c>PEMS_RATCHET_RECORD=1 dotnet test --filter AdoptionRatchet</c> rewrites it).
/// </summary>
public sealed class AdoptionRatchetTests
{
    private const string BaselinePath = "tools/UiAudit/adoption-baseline.json";

    // One entry per hand-written block that has (or will have) a PEMS.UI component. Views are scanned for all; scripts only for the script patterns.
    private static readonly (string Name, Regex Pattern, bool Views, bool Scripts)[] Patterns =
    [
        ("alert", new Regex(@"class=""alert(?![\w-])|class=""[^""]*\balert alert-", RegexOptions.Compiled), true, true),
        ("badge", new Regex(@"class=""[^""]*(?<![\w-])badge(?![\w-])|\btext-bg-[a-z]+", RegexOptions.Compiled), true, true),
        ("button", new Regex(@"\bbtn btn-[a-z-]+", RegexOptions.Compiled), true, true),
        ("text-input", new Regex(@"\bform-control\b", RegexOptions.Compiled), true, true),
        ("select", new Regex(@"\bform-select\b", RegexOptions.Compiled), true, true),
        ("date-input", new Regex(@"<input\b[^>]*\btype=""date""", RegexOptions.Compiled), true, true),   // SCRUM-225: <pems-date> (Jalali or Gregorian, posts the ISO date)
        ("form-label", new Regex(@"class=""[^""]*(?<![\w-])form-label(?![\w-])", RegexOptions.Compiled), true, true),
        ("textarea", new Regex(@"<textarea\b", RegexOptions.Compiled), true, true),
        ("check-radio-switch", new Regex(@"\bform-check-input\b|\bform-switch\b", RegexOptions.Compiled), true, true),
        ("modal", new Regex(@"class=""modal(?![\w-])", RegexOptions.Compiled), true, true),
        ("offcanvas", new Regex(@"class=""offcanvas(?![\w-])", RegexOptions.Compiled), true, true),
        ("tabs", new Regex(@"\bnav-tabs\b", RegexOptions.Compiled), true, true),
        ("breadcrumb", new Regex(@"class=""breadcrumb(?![\w-])", RegexOptions.Compiled), true, true),
        ("dropdown", new Regex(@"class=""dropdown-menu\b", RegexOptions.Compiled), true, true),
        ("progress", new Regex(@"class=""progress(?![\w-])", RegexOptions.Compiled), true, true),
        ("accordion", new Regex(@"(?<![\w-])accordion-item(?![\w-])", RegexOptions.Compiled), true, true),
        ("list-group", new Regex(@"class=""list-group(?![\w-])", RegexOptions.Compiled), true, true),
        ("card", new Regex(@"class=""card(?![\w-])", RegexOptions.Compiled), true, true),
        ("spinner", new Regex(@"\bspinner-(border|grow)\b", RegexOptions.Compiled), true, true),
        ("table", new Regex(@"<table\b", RegexOptions.Compiled), true, true),
        ("info-grid", new Regex(@"\bvs-info__label\b|<dt\b", RegexOptions.Compiled), true, true),   // SCRUM-337 InfoGrid: <pems-info-grid>
        ("kpi-tile", new Regex(@"class=""kpi-card\b", RegexOptions.Compiled), true, true),   // SCRUM-337 KpiTile: <pems-kpi-tile>
        // SCRUM-337 Skeleton: "Loading..." text as a placeholder (<pems-skeleton> / PemsSkeleton.show); the skeleton's own label="..." is not counted
        ("loading-text", new Regex(@"(?<!label=""[^>]{0,80})(Loading\.\.\.|Loading…|در حال بارگذاری|در حال جستجو)", RegexOptions.Compiled), true, true),
        // SCRUM-337 Chart: a chart drawn by hand (Chart.js, a chart canvas, a hand-written Bklit host) instead of <pems-chart spec="...">
        ("chart", new Regex(@"new Chart\(|data-bk-chart=|<canvas[^>]*[Cc]hart", RegexOptions.Compiled), true, true),
        ("confirm-call", new Regex(@"(?<![\w.])confirm\(|window\.confirm\(", RegexOptions.Compiled), true, true),
        ("alert-call", new Regex(@"(?<![\w.])alert\(", RegexOptions.Compiled), true, true),
        // SCRUM-508 (owner decision 2026-11-05: every piece of the UI is a component): the patterns that had no counter yet. A hand-written block of these is UI
        // that is not a registered component; the baseline starts at today's size and only goes down.
        ("inline-style", new Regex(@"\sstyle=""", RegexOptions.Compiled), true, false),
        ("icon", new Regex(@"<i\b[^>]*\bclass=""[^""]*(?<![\w-])bi(?![\w-])", RegexOptions.Compiled), true, true),
        ("layout-grid", new Regex(@"class=""[^""]*(?<![\w@(.-])(row|col(-[a-z]+)?(-\d+)?)(?![\w.-])", RegexOptions.Compiled), true, false),   // not a Razor variable called row (row.Allowed, @row)
        ("pagination", new Regex(@"class=""[^""]*(?<![\w-])(pagination|page-link)(?![\w-])", RegexOptions.Compiled), true, true),
        ("bare-button", new Regex(@"class=""btn(?![\w-])", RegexOptions.Compiled), true, true),
        ("inline-script", new Regex(@"<script>", RegexOptions.Compiled), true, false),
        ("js-built-markup", new Regex(@"\.innerHTML\s*\+?=|insertAdjacentHTML\(", RegexOptions.Compiled), false, true),
    ];

    [Fact]
    public void No_count_of_a_hand_written_pattern_rises_above_the_baseline()
    {
        var root = RepoRoot();
        var current = Count(root);
        var path = Path.Combine(root, BaselinePath);

        if (Environment.GetEnvironmentVariable("PEMS_RATCHET_RECORD") == "1")
        {
            File.WriteAllText(path, Serialize(current), new UTF8Encoding(false));
            return;
        }

        Assert.True(File.Exists(path), $"{BaselinePath} is missing: record it with PEMS_RATCHET_RECORD=1.");
        var baseline = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, int>>>(File.ReadAllText(path))!;

        var rose = new List<string>();
        var fell = new List<string>();
        foreach (var name in current.Keys.Union(baseline.Keys))
        {
            var now = current.GetValueOrDefault(name) ?? [];
            var was = baseline.GetValueOrDefault(name) ?? [];
            foreach (var file in now.Keys.Union(was.Keys))
            {
                var a = now.GetValueOrDefault(file);
                var b = was.GetValueOrDefault(file);
                if (a > b) rose.Add($"{name}: {file} has {a}, baseline {b}");
                else if (a < b) fell.Add($"{name}: {file} has {a}, baseline {b}");
            }
        }

        Assert.True(rose.Count == 0,
            "Hand-written markup that a PEMS.UI component replaces was added. Use the component (docs/guidelines/02-ui-and-components.md):\n" + string.Join("\n", rose.Take(40)));
        Assert.True(fell.Count == 0,
            "Good: fewer hand-written blocks. Lower the baseline in the same change with PEMS_RATCHET_RECORD=1 dotnet test PEMS.UI.Tests --filter AdoptionRatchet:\n" + string.Join("\n", fell.Take(40)));
    }

    [Fact]
    public void The_counter_sees_the_patterns_it_is_meant_to_count()
    {
        Assert.Equal(1, Hits("alert", "<div class=\"alert alert-danger small\">x</div>"));
        Assert.Equal(1, Hits("inline-style", "<div style=\"width:130px\">x</div>"));
        Assert.Equal(1, Hits("icon", "<i class=\"bi bi-x\" aria-hidden=\"true\"></i>"));
        Assert.Equal(0, Hits("icon", "<i class=\"fab fa-x\"></i><i class=\"bidi\"></i>"));
        Assert.Equal(2, Hits("layout-grid", "<div class=\"row g-2\"><div class=\"col-md-6\">x</div></div><div class=\"rowing\">"));
        Assert.Equal(1, Hits("pagination", "<ul class=\"pagination\"><li class=\"page-item\">x</li></ul>"));
        Assert.Equal(1, Hits("bare-button", "<a class=\"btn\">x</a><a class=\"btn-link\">y</a>"));
        Assert.Equal(1, Hits("button", "<button class=\"btn btn-primary btn-sm\">x</button>"));
        Assert.Equal(1, Hits("date-input", "<input name=\"due\" type=\"date\" class=\"form-control\" />"));
        Assert.Equal(0, Hits("date-input", "<pems-date name=\"due\" /><input type=\"text\" data-type=\"date2\">"));
        Assert.Equal(2, Hits("badge", "<span class=\"badge text-bg-success\">x</span>"));
        Assert.Equal(1, Hits("alert-call", "alert('x');"));
        Assert.Equal(1, Hits("confirm-call", "if (!confirm('x')) return;"));
        Assert.Equal(1, Hits("confirm-call", "if (!window.confirm('x')) return;"));
        Assert.Equal(0, Hits("confirm-call", "window.PemsDialog.confirm('x').then(f);"));
        Assert.Equal(0, Hits("alert-call", "window.alert2(); PemsAlert(x);"));
        Assert.Equal(1, Hits("js-built-markup", "el.innerHTML = '<b>' + t + '</b>';"));
        Assert.Equal(0, Hits("js-built-markup", "el.textContent = t;"));
        Assert.Equal(2, Hits("info-grid", "<div><p class=\"vs-info__label\">A</p></div><dl><dt>B</dt><dd>b</dd></dl>"));
        Assert.Equal(0, Hits("info-grid", "<pems-info-item label=\"A\">a</pems-info-item><dd>x</dd>"));
        Assert.Equal(2, Hits("kpi-tile", "<div class=\"kpi-card\">1</div><a class=\"kpi-card kpi-link\">2</a>"));
        Assert.Equal(0, Hits("kpi-tile", "<pems-kpi-tile label=\"A\">1</pems-kpi-tile><div class=\"pems-kpi-tile x\">"));
        Assert.Equal(2, Hits("loading-text", "<p class=\"text-muted\">Loading...</p> el.textContent = 'در حال بارگذاری';"));
        Assert.Equal(0, Hits("loading-text", "<pems-skeleton kind=\"card\" label=\"@L(\"Loading...\", \"در حال بارگذاری...\")\" /> @Localizer[\"Loading\"]"));
        Assert.Equal(3, Hits("chart", "<canvas id=\"trendChart\"></canvas> new Chart(el, {}); <div data-bk-chart=\"{}\"></div>"));
        Assert.Equal(0, Hits("chart", "<pems-chart spec=\"@trend\" /> <canvas id=\"signaturePad\"></canvas> <div class=\"bk-chart\">"));
    }

    private static int Hits(string name, string text) => Patterns.Single(p => p.Name == name).Pattern.Matches(text).Count;

    private static SortedDictionary<string, SortedDictionary<string, int>> Count(string root)
    {
        var result = new SortedDictionary<string, SortedDictionary<string, int>>(StringComparer.Ordinal);
        foreach (var p in Patterns) result[p.Name] = new SortedDictionary<string, int>(StringComparer.Ordinal);

        void Scan(string file, bool isView)
        {
            var text = File.ReadAllText(file);
            var key = Path.GetRelativePath(root, file).Replace('\\', '/');
            foreach (var p in Patterns)
            {
                if (isView ? !p.Views : !p.Scripts) continue;
                var n = p.Pattern.Matches(text).Count;
                if (n > 0) result[p.Name][key] = n;
            }
        }

        foreach (var f in Directory.EnumerateFiles(Path.Combine(root, "PEMS.Web", "Views"), "*.cshtml", SearchOption.AllDirectories)) Scan(f, true);
        foreach (var f in Directory.EnumerateFiles(Path.Combine(root, "PEMS.Web", "Areas"), "*.cshtml", SearchOption.AllDirectories)) Scan(f, true);
        foreach (var f in Directory.EnumerateFiles(Path.Combine(root, "PEMS.Web", "wwwroot", "js"), "*.js", SearchOption.AllDirectories)) Scan(f, false);
        return result;
    }

    private static string Serialize(SortedDictionary<string, SortedDictionary<string, int>> data) =>
        JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }).Replace("\r\n", "\n") + "\n";

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "PEMS.Web", "Views"))) dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException("PEMS.Web/Views");
    }
}
