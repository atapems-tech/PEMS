using System.Text.Json.Nodes;

namespace PEMS.Tests.Definitions;

public sealed class EngineeringDocumentsDefinitionTests
{
    [Fact]
    public void Engineering_documents_page_has_a_real_grid_contract()
    {
        var path = FindRepoFile(Path.Combine("PEMS.Persistence", "Definitions", "System", "page", "core", "eng-docs.json"));
        var root = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        var content = root["payload"]!["regions"]!["content"]!.AsArray();
        var list = content.First(x => (string?)x!["id"] == "list")!.AsObject();
        var options = list["options"]!.AsObject();
        var columns = options["columns"]!.AsArray().Select(x => x!.AsObject()).ToList();

        Assert.Equal("def:entity-list", (string?)list["component"]);
        Assert.StartsWith("/d/eng-docs-list", (string?)options["url"]);
        Assert.StartsWith("/x/eng-docs-export", (string?)options["exportUrl"]);
        Assert.Equal(50, (int?)options["pageSize"]);
        Assert.Equal(44, (int?)options["rowHeight"]);
        Assert.Contains(columns, c => (string?)c["key"] == "documentNo");
        Assert.Contains(columns, c => (string?)c["key"] == "title");
        Assert.Contains(columns, c => (string?)c["key"] == "type");
        Assert.Contains(columns, c => (string?)c["key"] == "discipline");
        Assert.Contains(columns, c => (string?)c["key"] == "revision");
        Assert.Contains(columns, c => (string?)c["key"] == "status" && (string?)c["type"] == "badge");
        Assert.Contains(columns, c => (string?)c["key"] == "delayDays");
        Assert.Contains(columns, c => (string?)c["key"] == "created");
        Assert.Contains(columns, c => (string?)c["key"] == "_actions");
        Assert.DoesNotContain(columns, c => c["header"] is null);

        var nav = content.First(x => (string?)x!["id"] == "nav")!["options"]!["blocks"]!.AsArray();
        Assert.Contains(nav, x => (string?)x!["href"] == "/p/eng-docs");
    }

    private static string FindRepoFile(string relative)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, relative);
            if (File.Exists(candidate)) return candidate;
        }

        throw new FileNotFoundException(relative);
    }
}
