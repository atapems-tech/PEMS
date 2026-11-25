using System.Globalization;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Application.Definitions;
using PEMS.Application.Grids;
using PEMS.Persistence.Seed;
using PEMS.Tests.TestSupport;
using PEMS.Web.Definitions;

namespace PEMS.Tests.Definitions;

/// <summary>
/// SCRUM-805 (gap of SCRUM-591): the administration of definition grants is a page, a form and two actions over the DefinitionGrant entity, made from the generic entity layer. A SystemAdmin lists, creates and removes
/// grants per kind, definition key and role through the real handlers; a second grant for the same role and definition is refused.
/// </summary>
public sealed class DefinitionGrantPagesTests
{
    private static readonly EntityCatalog Catalog = EntityCatalog.Default();

    private sealed class Access : IDefinitionAccess
    {
        public Task<bool> CanOpenAsync(string? permission, IReadOnlyCollection<string> roles, CancellationToken ct = default) => Task.FromResult(true);
    }

    private static (IServiceScopeFactory Scopes, string Db) Setup()
    {
        var db = "grants-" + Guid.NewGuid();
        var sp = new ServiceCollection().AddScoped(_ => PemsDbContextFactory.CreateInMemory(db)).AddSingleton<IDefinitionAccess>(new Access()).BuildServiceProvider();
        return (sp.GetRequiredService<IServiceScopeFactory>(), db);
    }

    private static JsonObject Shipped(string kind, string key)
        => (JsonObject)JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, SystemDefinitionSeeder.FolderName, kind, "core", key + ".json")))!["payload"]!;

    private static ActionInvocation Invoke(string action, params (string, string?)[] values)
        => new(values.ToDictionary(v => v.Item1, v => v.Item2), "en", ["SystemAdmin"], "hamid", Shipped("action", action)["parameters"]!.AsObject());

    private static GridDataRequest Grid()
    {
        var p = Shipped("data-source", "definition-grants-grid")["parameters"]!.AsObject();
        return new GridDataRequest(GridRequest.FromQuery([]), "en", ["SystemAdmin"], "x",
            p.ToDictionary(x => x.Key, x => (string?)(x.Value is JsonValue v && v.TryGetValue<string>(out var s) ? s : x.Value!.ToJsonString())));
    }

    private static string En(PEMS.UI.Abstractions.TranslatableText t) => t.Resolve(new CultureInfo("en"));

    private static (string, string?)[] Grant(string kind, string key, string role, string effect = "allow")
        => [("kind", kind), ("definitionKey", key), ("roleName", role), ("effect", effect), ("reason", "test")];

    [Fact]
    public async Task A_system_admin_grants_lists_and_removes_a_role_for_a_definition()
    {
        var (scopes, name) = Setup();
        var create = new EntityCreateAction(scopes, Catalog);
        var grid = new EntityGridProvider(scopes, Catalog);

        var made = await create.ExecuteAsync(Invoke("definition-grant-create", Grant("page", "work-orders", "PRC.Buyer")), CancellationToken.None);
        Assert.True(made.Ok, made.FormError?.ToString());
        await create.ExecuteAsync(Invoke("definition-grant-create", Grant("page", "work-orders", "PRC.Manager", "deny")), CancellationToken.None);

        var rows = (await grid.QueryAsync(Grid(), CancellationToken.None)).Items.Cast<Dictionary<string, object?>>().ToList();
        Assert.Equal(["PRC.Buyer", "PRC.Manager"], rows.Select(r => (string)r["roleName"]!).OrderBy(x => x));
        Assert.Equal("deny", (string)rows.Single(r => (string)r["roleName"]! == "PRC.Manager")["effect"]!);

        int id;
        using (var db = PemsDbContextFactory.CreateInMemory(name)) id = (await db.DefinitionGrants.SingleAsync(g => g.RoleName == "PRC.Buyer")).DefinitionGrantId;
        var removed = await new EntityDeleteAction(scopes, Catalog).ExecuteAsync(Invoke("definition-grant-delete", ("id", id.ToString())), CancellationToken.None);
        Assert.True(removed.Ok, removed.FormError?.ToString());
        using (var db = PemsDbContextFactory.CreateInMemory(name)) Assert.Equal(["PRC.Manager"], await db.DefinitionGrants.Select(g => g.RoleName).ToListAsync());
    }

    [Fact]
    public async Task A_second_grant_for_the_same_role_and_definition_is_refused_with_the_message_of_the_action()
    {
        var (scopes, _) = Setup();
        var create = new EntityCreateAction(scopes, Catalog);
        Assert.True((await create.ExecuteAsync(Invoke("definition-grant-create", Grant("form", "f1", "R1")), CancellationToken.None)).Ok);

        var again = await create.ExecuteAsync(Invoke("definition-grant-create", Grant("form", "f1", "R1", "deny")), CancellationToken.None);

        Assert.False(again.Ok);
        Assert.Equal("This role already has a grant for this definition.", En(again.FieldErrors?.Values.FirstOrDefault() ?? again.FormError!));
        Assert.True((await create.ExecuteAsync(Invoke("definition-grant-create", Grant("form", "f1", "R2")), CancellationToken.None)).Ok);   // another role
    }

    [Fact]
    public void Only_the_system_admin_reads_or_writes_grants_and_the_pieces_of_the_page_fit_together()
    {
        var entity = Catalog.Get("DefinitionGrant")!;
        Assert.Equal(new[] { "SystemAdmin" }, entity.ReadRoles!.ToArray());
        Assert.Equal(new[] { "SystemAdmin" }, entity.WriteRoles!.ToArray());
        Assert.Empty(new EntityGridProvider(null!, Catalog).ValidateParameters((JsonObject)Shipped("data-source", "definition-grants-grid")["parameters"]!));

        var page = Shipped("page", "definition-grants")["args"]!.AsObject();
        Assert.Equal("/d/definition-grants-grid", (string)page["url"]!);
        Assert.Equal("/f/definition-grant-create", (string)page["newUrl"]!);
        var remove = page["columns"]!.AsArray().Select(c => c!["actions"]).OfType<JsonArray>().SelectMany(a => a).Single()!;
        Assert.Equal("/a/definition-grant-delete", (string)remove["url"]!);
        Assert.Equal("definition-grant-create", (string)Shipped("form", "definition-grant-create")["actionKey"]!);
        Assert.Equal("DefinitionGrant", (string)Shipped("action", "definition-grant-delete")["parameters"]!["entity"]!);
    }
}
