using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Application.Definitions;
using PEMS.Application.Grids;
using PEMS.Domain.Entities;
using PEMS.Persistence.Seed;
using PEMS.Tests.TestSupport;
using PEMS.Web.Definitions;

namespace PEMS.Tests.Definitions;

/// <summary>
/// SCRUM-806: a database view registered as a read-only entity is read by the shipped data source with paging, sort, search and the entity's read roles. The in-memory provider has no SQL view, so the rows of the
/// view are added as rows of its entity (the view's SQL is in the migration and is a QA line on SQL Server).
/// </summary>
public sealed class DatabaseViewEntityTests
{
    private static readonly EntityCatalog Catalog = EntityCatalog.Default();

    private sealed class Access : IDefinitionAccess
    {
        public Task<bool> CanOpenAsync(string? permission, IReadOnlyCollection<string> roles, CancellationToken ct = default) => Task.FromResult(true);
    }

    private static EntityGridProvider Setup(int rows)
    {
        var name = "view-" + Guid.NewGuid();
        using (var db = PemsDbContextFactory.CreateInMemory(name))
        {
            for (var i = 1; i <= rows; i++)
                db.DefinitionGrantSummaries.Add(new DefinitionGrantSummary { GrantKey = $"page:p{i:00}", Kind = "page", DefinitionKey = $"p{i:00}", AllowCount = i, DenyCount = 0, RoleCount = i });
            db.SaveChanges();
        }
        var sp = new ServiceCollection().AddScoped(_ => PemsDbContextFactory.CreateInMemory(name)).AddSingleton<IDefinitionAccess>(new Access()).BuildServiceProvider();
        return new EntityGridProvider(sp.GetRequiredService<IServiceScopeFactory>(), Catalog);
    }

    private static JsonObject Shipped()
        => (JsonObject)JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, SystemDefinitionSeeder.FolderName, "data-source", "core", "definition-grant-summary-grid.json")))!["payload"]!;

    private static GridDataRequest Ask(string[] roles, params (string, string)[] query)
    {
        var p = Shipped()["parameters"]!.AsObject();
        return new GridDataRequest(GridRequest.FromQuery(query.Select(q => new KeyValuePair<string, string?>(q.Item1, q.Item2))), "en", roles, "x",
            p.ToDictionary(x => x.Key, x => (string?)(x.Value is JsonValue v && v.TryGetValue<string>(out var s) ? s : x.Value!.ToJsonString())));
    }

    [Fact]
    public void The_view_is_a_registered_read_only_entity_and_the_shipped_source_passes_the_validation()
    {
        var entity = Catalog.Get("DefinitionGrantSummary")!;
        Assert.Equal(("GrantKey", 0), (entity.KeyProperty, entity.Writable.Count));
        Assert.Empty(new EntityGridProvider(null!, Catalog).ValidateParameters((JsonObject)Shipped()["parameters"]!));
    }

    [Fact]
    public async Task The_view_is_read_with_paging_and_search()
    {
        var grid = Setup(25);
        var first = await grid.QueryAsync(Ask(["SystemAdmin"], ("page", "1"), ("pageSize", "10")), CancellationToken.None);
        Assert.Equal((25, 10), (first.Total, first.Items.Count));
        var last = await grid.QueryAsync(Ask(["SystemAdmin"], ("page", "3"), ("pageSize", "10")), CancellationToken.None);
        Assert.Equal(5, last.Items.Count);
        var found = await grid.QueryAsync(Ask(["SystemAdmin"], ("q", "p07")), CancellationToken.None);
        Assert.Equal(1, found.Total);
        Assert.Equal(7, ((Dictionary<string, object?>)found.Items[0])["allowCount"]);
    }

    [Fact]
    public async Task A_role_without_the_read_right_of_the_entity_is_refused()
    {
        var grid = Setup(1);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => grid.QueryAsync(Ask(["Buyer"]), CancellationToken.None));
    }
}
