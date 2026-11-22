using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using PEMS.Persistence.Definitions;
using PEMS.Persistence.Workflow;

namespace PEMS.Tests.Definitions;

/// <summary>
/// SCRUM-802: docs/architecture/REFERENCE_DATA_OWNERS.md names every seeder class with a proposed owner (platform, application or extensible). The test keeps the table complete when a seeder is added, and ties the
/// rows marked "moved" to the shipped reference-data lists and workflow documents (SCRUM-803, 804). The confirmation column is the owner's and is not checked.
/// </summary>
public sealed class ReferenceDataOwnerTableTests
{
    private static string Here([CallerFilePath] string p = "") => p;
    private static string Root() => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Here())!, "..", ".."));

    private static List<(string Seeder, string Owner, string Status)> Rows()
        => File.ReadAllLines(Path.Combine(Root(), "docs", "architecture", "REFERENCE_DATA_OWNERS.md"))
            .Select(l => Regex.Match(l, @"^\| `(?<s>[A-Za-z0-9]+)` \|.*\| (?<o>platform|application|extensible) \|[^|]*\| (?<st>[^|]*) \| [^|]* \|$"))
            .Where(m => m.Success).Select(m => (m.Groups["s"].Value, m.Groups["o"].Value, m.Groups["st"].Value.Trim())).ToList();

    [Fact]
    public void Every_seeder_class_is_a_row_of_the_table_exactly_once_with_one_of_the_three_owners()
    {
        var seeders = Directory.GetFiles(Path.Combine(Root(), "PEMS.Persistence", "Seed"), "*.cs").Select(Path.GetFileNameWithoutExtension).OfType<string>().OrderBy(n => n, StringComparer.Ordinal).ToList();
        var rows = Rows();
        Assert.Equal(seeders, rows.Select(r => r.Seeder).OrderBy(n => n, StringComparer.Ordinal));
        Assert.Equal(61, rows.Count);
    }

    [Fact]
    public void The_rows_marked_moved_match_the_shipped_lists_and_documents()
    {
        var moved = Rows().Where(r => r.Status.StartsWith("moved:", StringComparison.Ordinal)).Select(r => Regex.Match(r.Status, @"(reference-data/core|Workflow/Shipped)/([a-z-]+)\.").Groups).ToList();
        var lists = moved.Where(g => g[1].Value == "reference-data/core").Select(g => g[2].Value).OrderBy(n => n, StringComparer.Ordinal);
        var documents = moved.Where(g => g[1].Value == "Workflow/Shipped").Select(g => g[2].Value).OrderBy(n => n, StringComparer.Ordinal);
        Assert.Equal(ReferenceDataCatalog.ListKeys.OrderBy(n => n, StringComparer.Ordinal), lists);
        Assert.Equal(ShippedWorkflowCatalog.Names(), documents);
    }
}
