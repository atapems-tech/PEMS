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
        ("alert", new Regex(@"class=""alert(?![\w-])|class=""[^""]*\balert alert-", RegexOptions.Compiled), true, false),
        ("badge", new Regex(@"class=""[^""]*\bbadge\b|\btext-bg-[a-z]+", RegexOptions.Compiled), true, false),
        ("button", new Regex(@"\bbtn btn-[a-z-]+", RegexOptions.Compiled), true, false),
        ("text-input", new Regex(@"\bform-control\b", RegexOptions.Compiled), true, false),
        ("select", new Regex(@"\bform-select\b", RegexOptions.Compiled), true, false),
        ("textarea", new Regex(@"<textarea\b", RegexOptions.Compiled), true, false),
        ("check-radio-switch", new Regex(@"\bform-check-input\b|\bform-switch\b", RegexOptions.Compiled), true, false),
        ("modal", new Regex(@"class=""modal(?![\w-])", RegexOptions.Compiled), true, false),
        ("offcanvas", new Regex(@"class=""offcanvas(?![\w-])", RegexOptions.Compiled), true, false),
        ("tabs", new Regex(@"\bnav-tabs\b", RegexOptions.Compiled), true, false),
        ("breadcrumb", new Regex(@"class=""breadcrumb(?![\w-])", RegexOptions.Compiled), true, false),
        ("dropdown", new Regex(@"class=""dropdown-menu\b", RegexOptions.Compiled), true, false),
        ("progress", new Regex(@"class=""progress(?![\w-])", RegexOptions.Compiled), true, false),
        ("accordion", new Regex(@"\baccordion-item\b", RegexOptions.Compiled), true, false),
        ("list-group", new Regex(@"class=""list-group(?![\w-])", RegexOptions.Compiled), true, false),
        ("card", new Regex(@"class=""card(?![\w-])", RegexOptions.Compiled), true, false),
        ("spinner", new Regex(@"\bspinner-(border|grow)\b", RegexOptions.Compiled), true, false),
        ("table", new Regex(@"<table\b", RegexOptions.Compiled), true, false),
        ("confirm-call", new Regex(@"\bconfirm\(", RegexOptions.Compiled), true, true),
        ("alert-call", new Regex(@"(?<![\w.])alert\(", RegexOptions.Compiled), true, true),
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
        Assert.Equal(1, Hits("button", "<button class=\"btn btn-primary btn-sm\">x</button>"));
        Assert.Equal(2, Hits("badge", "<span class=\"badge text-bg-success\">x</span>"));
        Assert.Equal(1, Hits("alert-call", "alert('x');"));
        Assert.Equal(0, Hits("alert-call", "window.alert2(); PemsAlert(x);"));
        Assert.Equal(1, Hits("js-built-markup", "el.innerHTML = '<b>' + t + '</b>';"));
        Assert.Equal(0, Hits("js-built-markup", "el.textContent = t;"));
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
