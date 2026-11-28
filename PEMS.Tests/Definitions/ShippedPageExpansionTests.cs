using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using PEMS.UI.Manifest;
using PEMS.Web.Definitions;

namespace PEMS.Tests.Definitions;

/// <summary>
/// SCRUM-836: the file check of a shipped page does not look inside a component definition (def:related-table and the others expand at render time), so a related table whose columns lacked a header was valid as a file and hidden at run time.
/// This test expands every use of a component definition in every shipped page and validates the instances that come out with the manifest of their component, as the page renderer does.
/// </summary>
public class ShippedPageExpansionTests
{
    [Fact]
    public void Every_component_definition_use_of_every_shipped_page_expands_to_valid_instances()
    {
        var provider = PEMS.UI.ServiceCollectionExtensions.AddPemsUi(new ServiceCollection()).BuildServiceProvider().CreateScope().ServiceProvider;
        var catalog = provider.GetRequiredService<ComponentManifestCatalog>();
        var root = Path.Combine(AppContext.BaseDirectory, "definitions-system");
        var problems = new List<string>();
        foreach (var file in Directory.GetFiles(Path.Combine(root, "page", "core"), "*.json"))
        {
            if (JsonNode.Parse(File.ReadAllText(file))?["payload"]?["regions"] is not JsonObject regions) continue;
            foreach (var (_, list) in regions)
                foreach (var instance in (list as JsonArray) ?? [])
                {
                    var component = (string?)instance?["component"];
                    if (component is null || !ComponentTemplate.IsDefinitionRef(component)) continue;
                    var definitionFile = Path.Combine(root, "component", "core", ComponentTemplate.KeyOf(component) + ".json");
                    if (!File.Exists(definitionFile)) { problems.Add($"{Path.GetFileName(file)}: no component definition {component}"); continue; }
                    var payload = (JsonObject)JsonNode.Parse(File.ReadAllText(definitionFile))!["payload"]!;
                    JsonArray expanded;
                    try { expanded = ComponentTemplate.ExpandInstances(payload, instance!["options"] as JsonObject, (string?)instance["id"] ?? "x"); }
                    catch (Exception ex) { problems.Add($"{Path.GetFileName(file)} {instance!["id"]}: {ex.Message}"); continue; }
                    foreach (var child in expanded.OfType<JsonObject>())
                    {
                        var key = (string?)child["component"];
                        if (key is null || ComponentTemplate.IsDefinitionRef(key)) continue;
                        var supplied = new List<string>();
                        if (child["bindings"] is JsonObject b) { if (b.ContainsKey("steps")) supplied.Add("steps"); if (b.ContainsKey("matrix")) { supplied.Add("rows"); supplied.Add("columns"); } }
                        foreach (var e in catalog.Validate(key, child["options"]?.ToJsonString() ?? "{}", supplied).Errors.Where(e => e.Code is "required" or "unknown-property"))
                            problems.Add($"{Path.GetFileName(file)} {instance!["id"]}.{child["id"]}: {e.Pointer} {e.Code}");
                    }
                }
        }
        Assert.True(problems.Count == 0, "invalid component definition uses: " + string.Join(" || ", problems.Take(40)));
    }
}
