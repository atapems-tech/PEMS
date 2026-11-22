using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Persistence.Context;
using PEMS.WorkflowPlatform.Interfaces;
using PEMS.WorkflowPlatform.Services;

namespace PEMS.Persistence.Workflow;

// SCRUM-804: the shipped workflow documents live in Workflow/Shipped/*.workflow.json (embedded). The old C# seeders of the converted families only call this catalog, so startup, tests and the constants other code uses stay as they were.
public static class ShippedWorkflowCatalog
{
    private const string Prefix = "PEMS.Workflow.Shipped.";
    public static readonly WorkflowActor SystemActor = new(null, "System (shipped workflow)");

    /// <summary>The names of the shipped documents (file name without the extension).</summary>
    public static IReadOnlyList<string> Names()
        => typeof(ShippedWorkflowCatalog).Assembly.GetManifestResourceNames()
            .Where(n => n.StartsWith(Prefix, StringComparison.Ordinal) && n.EndsWith(".workflow.json", StringComparison.Ordinal))
            .Select(n => n[Prefix.Length..^".workflow.json".Length]).OrderBy(n => n, StringComparer.Ordinal).ToList();

    /// <summary>Reads one shipped document; a missing or invalid document is a build mistake and throws.</summary>
    public static ShippedWorkflow Load(string name)
    {
        using var stream = typeof(ShippedWorkflowCatalog).Assembly.GetManifestResourceStream(Prefix + name + ".workflow.json")
            ?? throw new InvalidOperationException($"The shipped workflow document '{name}' is not embedded.");
        using var reader = new StreamReader(stream);
        var (workflow, problems) = ShippedWorkflow.Parse(reader.ReadToEnd());
        return workflow ?? throw new InvalidOperationException($"The shipped workflow document '{name}' is invalid: " + string.Join(" ", problems));
    }

    /// <summary>
    /// What a converted seeder runs at startup. A workflow of this module and context that the customer renamed is left alone (as the old seeder did); otherwise the shipper creates it, or stores an upstream-changed
    /// draft when the shipped structure differs from the active version.
    /// </summary>
    public static async Task ShipAsync(IServiceProvider services, string name)
    {
        var db = services.GetRequiredService<PEMSDbContext>();
        var shipped = Load(name);
        if (await db.WorkflowDefinitions.AnyAsync(d => d.Module == shipped.Module && d.Category == shipped.Context && d.Name != shipped.Name)) return;
        await new WorkflowShipper(db, new WorkflowDefinitionService(db)).ShipAsync(shipped, SystemActor);
    }
}
