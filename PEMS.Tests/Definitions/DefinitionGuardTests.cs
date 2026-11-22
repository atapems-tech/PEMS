using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using PEMS.Web.Definitions;

namespace PEMS.Tests.Definitions;

/// <summary>SCRUM-819: what the controller authorization tests of batch 7 proved, over the shipped definitions: a write action names a permission (on the definition or on its operation), and every link points to a definition that exists.</summary>
public class DefinitionGuardTests
{
    private static string Root()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "PEMS.Persistence", "Definitions", "System"))) dir = dir.Parent;
        return Path.Combine(dir!.FullName, "PEMS.Persistence", "Definitions", "System");
    }

    private static List<(string Kind, string Key, string Text)> All()
    {
        var list = new List<(string, string, string)>();
        foreach (var kindDir in Directory.GetDirectories(Root()))
        {
            var core = Path.Combine(kindDir, "core");
            if (!Directory.Exists(core)) continue;
            foreach (var file in Directory.GetFiles(core, "*.json", SearchOption.TopDirectoryOnly))
                list.Add((Path.GetFileName(kindDir), Path.GetFileNameWithoutExtension(file), File.ReadAllText(file)));
        }
        return list;
    }

    // guarded inside the service (the roles go in as an argument) or open to any signed-in user, exactly as the controllers they replaced
    private static readonly HashSet<string> GuardedInTheService = new(StringComparer.Ordinal)
    {
        "Meeting.Create", "Meeting.UpdateMinutes", "Meeting.AddAttendee", "Meeting.MarkAttended", "Meeting.AddActionItem",
        "TechnicalQuery.Raise", "TechnicalQuery.Respond", "TechnicalQuery.Confirm", "TechnicalQuery.Reopen"
    };

    private static readonly string[] Models = ["JobPlan", "Crew", "MaintenancePlan", "Contract", "Tender", "Meeting", "Inspection", "Finding", "ShiftHandover", "Permit", "WorkRequest", "TechnicalQuery"];

    [Fact]
    public void A_write_action_of_the_converted_work_areas_names_a_permission_or_its_operation_does()
    {
        var operations = OperationCatalog.Scan(typeof(PEMS.Application.Common.PemsOperationAttribute).Assembly);
        var open = new List<string>();
        foreach (var (kind, key, text) in All().Where(x => x.Kind == "action"))
        {
            var payload = JsonNode.Parse(text)!["payload"]!.AsObject();
            if ((string?)payload["handler"] != "operation.invoke") continue;
            var p = payload["parameters"]!.AsObject();
            var model = (string)p["model"]!;
            var name = model + "." + (string)p["action"]!;
            if (!Models.Contains(model) || GuardedInTheService.Contains(name)) continue;
            var op = operations.Get(model, (string)p["action"]!);
            if (op is null) { open.Add(key + ": unknown operation " + name); continue; }
            if (string.IsNullOrWhiteSpace((string?)payload["permission"]) && string.IsNullOrWhiteSpace(op.Permission) && op.Roles.Count == 0) open.Add(key + " -> " + name);
        }
        Assert.True(open.Count == 0, "Write actions with no permission signal: " + string.Join(", ", open));
    }

    [Fact]
    public void Every_menu_entry_that_opens_a_definition_page_points_to_a_page_that_exists_and_keeps_its_gate()
    {
        var pages = All().Where(x => x.Kind == "page").Select(x => x.Key).ToHashSet();
        var broken = new List<string>();
        foreach (var item in PEMS.Web.Navigation.NavigationMenuDefaults.All.Where(i => i.ExternalPath != null && i.ExternalPath.StartsWith("/p/", StringComparison.Ordinal)))
        {
            var key = item.ExternalPath!["/p/".Length..].Split('?', '/')[0];
            if (!pages.Contains(key)) broken.Add(item.Key + " -> " + item.ExternalPath);
        }
        Assert.True(broken.Count == 0, "Menu entries that point to a page that does not exist: " + string.Join(", ", broken));
        // the shift handover entry stays gated by its CMMS item permission (SCRUM-749), never open to everyone
        var handover = PEMS.Web.Navigation.NavigationMenuDefaults.All.Single(i => i.Key == "cmms.shifthandover");
        Assert.Contains("ShiftHandover", handover.VisibilityRule);
    }

    [Fact]
    public void Every_link_to_a_page_form_action_or_data_source_points_to_a_definition_that_exists()
    {
        var all = All();
        var keys = new Dictionary<string, HashSet<string>>
        {
            ["/p/"] = all.Where(x => x.Kind == "page").Select(x => x.Key).ToHashSet(),
            ["/f/"] = all.Where(x => x.Kind == "form").Select(x => x.Key).ToHashSet(),
            ["/a/"] = all.Where(x => x.Kind == "action").Select(x => x.Key).ToHashSet(),
            ["/d/"] = all.Where(x => x.Kind == "data-source").Select(x => x.Key).ToHashSet()
        };
        var broken = new List<string>();
        foreach (var (kind, key, text) in all)
            foreach (Match m in Regex.Matches(text, "\"(/[pfad]/)([A-Za-z0-9-]+)"))
                if (!keys[m.Groups[1].Value].Contains(m.Groups[2].Value)) broken.Add($"{kind}/{key}: {m.Groups[1].Value}{m.Groups[2].Value}");
        Assert.True(broken.Count == 0, "Links to a definition that does not exist: " + string.Join(", ", broken.Distinct()));
    }
}
