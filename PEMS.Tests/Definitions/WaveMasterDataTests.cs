using System.Globalization;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Application.Definitions;
using PEMS.Application.Grids;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;
using PEMS.Persistence.Seed;
using PEMS.Tests.TestSupport;
using PEMS.Web.Definitions;

namespace PEMS.Tests.Definitions;

/// <summary>The master-data pages of the waves (maintenance groups, skills, storage locations ...) run on the generic layer: lookups in the grid, selects from entities, composite unique rules, the catalog's permissions.</summary>
public sealed class WaveMasterDataTests
{
    private static readonly EntityCatalog Catalog = EntityCatalog.Default();

    private sealed class Access(Func<string?, bool> allow) : IDefinitionAccess
    {
        public Task<bool> CanOpenAsync(string? permission, IReadOnlyCollection<string> roles, CancellationToken ct = default) => Task.FromResult(allow(permission));
    }

    private static (IServiceScopeFactory Scopes, string Db) Setup(Func<string?, bool>? allow = null)
    {
        var db = "wave-" + Guid.NewGuid();
        var sp = new ServiceCollection().AddScoped(_ => PemsDbContextFactory.CreateInMemory(db)).AddSingleton<IDefinitionAccess>(new Access(allow ?? (_ => true))).BuildServiceProvider();
        return (sp.GetRequiredService<IServiceScopeFactory>(), db);
    }

    private static JsonObject Shipped(string kind, string key)
        => (JsonObject)JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, SystemDefinitionSeeder.FolderName, kind, "core", key + ".json")))!["payload"]!;

    private static GridDataRequest Grid(string dataSource, params (string, string)[] query)
        => new(GridRequest.FromQuery(query.Select(q => new KeyValuePair<string, string?>(q.Item1, q.Item2))), "en", ["Engineer"], "x",
            Shipped("data-source", dataSource)["parameters"]!.AsObject().ToDictionary(x => x.Key, x => (string?)(x.Value is JsonValue v && v.TryGetValue<string>(out var s) ? s : x.Value!.ToJsonString())));

    private static ActionInvocation Invoke(string action, params (string, string?)[] values)
        => new(values.ToDictionary(v => v.Item1, v => v.Item2), "en", ["Engineer"], "x", Shipped("action", action)["parameters"]!.AsObject());

    private static string En(PEMS.UI.Abstractions.TranslatableText t) => t.Resolve(new CultureInfo("en"));

    [Fact]
    public async Task A_maintenance_group_is_created_with_its_discipline_and_the_grid_shows_the_discipline_name()
    {
        var (scopes, name) = Setup();
        int discipline;
        using (var db = PemsDbContextFactory.CreateInMemory(name)) { var d = new Discipline { Code = "MECH", Name = "Mechanical", IsActive = true }; db.Disciplines.Add(d); db.SaveChanges(); discipline = d.DisciplineId; }

        var create = new EntityCreateAction(scopes, Catalog);
        Assert.True((await create.ExecuteAsync(Invoke("maintenance-groups-create", ("code", " PUMP "), ("name", "Pumps"), ("disciplineId", discipline.ToString()), ("description", "")), CancellationToken.None)).Ok);
        var dupName = await create.ExecuteAsync(Invoke("maintenance-groups-create", ("code", "X"), ("name", "pumps"), ("disciplineId", discipline.ToString())), CancellationToken.None);
        Assert.Equal("A maintenance group with this name already exists.", En(dupName.FieldErrors!["name"]));
        var dupCode = await create.ExecuteAsync(Invoke("maintenance-groups-create", ("code", "pump"), ("name", "Other"), ("disciplineId", discipline.ToString())), CancellationToken.None);
        Assert.Equal("A maintenance group with this code already exists.", En(dupCode.FieldErrors!["code"]));
        Assert.Contains("disciplineId", (await create.ExecuteAsync(Invoke("maintenance-groups-create", ("code", "Z"), ("name", "Z"), ("disciplineId", "")), CancellationToken.None)).FieldErrors!.Keys);   // the discipline is required

        var rows = (await new EntityGridProvider(scopes, Catalog).QueryAsync(Grid("maintenance-groups-grid"), CancellationToken.None)).Items.Cast<Dictionary<string, object?>>().ToList();
        Assert.Equal(("PUMP", "Pumps", "Mechanical"), ((string)rows[0]["code"]!, (string)rows[0]["name"]!, (string)rows[0]["discipline"]!));
    }

    [Fact]
    public async Task A_skill_may_have_no_discipline_and_a_location_code_is_unique_only_inside_its_warehouse()
    {
        var (scopes, name) = Setup();
        var create = new EntityCreateAction(scopes, Catalog);
        Assert.True((await create.ExecuteAsync(Invoke("skills-create", ("name", "Welding"), ("disciplineId", "")), CancellationToken.None)).Ok);
        using (var db = PemsDbContextFactory.CreateInMemory(name)) Assert.Null((await db.Skills.SingleAsync()).DisciplineId);

        int w1, w2;
        using (var db = PemsDbContextFactory.CreateInMemory(name)) { var a = new Warehouse { Code = "W1", Name = "One" }; var b = new Warehouse { Code = "W2", Name = "Two" }; db.Warehouses.AddRange(a, b); db.SaveChanges(); w1 = a.WarehouseId; w2 = b.WarehouseId; }
        Assert.True((await create.ExecuteAsync(Invoke("storage-locations-create", ("code", "A1"), ("name", "Shelf"), ("warehouseId", w1.ToString())), CancellationToken.None)).Ok);
        Assert.True((await create.ExecuteAsync(Invoke("storage-locations-create", ("code", "A1"), ("name", "Shelf"), ("warehouseId", w2.ToString())), CancellationToken.None)).Ok);   // same code, another warehouse
        var clash = await create.ExecuteAsync(Invoke("storage-locations-create", ("code", "a1"), ("name", "Other"), ("warehouseId", w1.ToString())), CancellationToken.None);
        Assert.False(clash.Ok);
        Assert.Equal("A location with this code already exists in this warehouse.", En(clash.FieldErrors?.Values.FirstOrDefault() ?? clash.FormError!));

        var rows = (await new EntityGridProvider(scopes, Catalog).QueryAsync(Grid("storage-locations-grid", ("sort", "warehouseId")), CancellationToken.None)).Items.Cast<Dictionary<string, object?>>().ToList();
        Assert.Equal(["One", "Two"], rows.Select(r => (string)r["warehouse"]!));
        Assert.All(rows, r => Assert.Null(r["parent"]));
    }

    [Fact]
    public async Task The_maintenance_master_data_permissions_of_the_catalog_decide_reads_and_writes()
    {
        var (scopes, db) = Setup(permission => permission is null || permission == "MaintenanceMasterData.View");
        Assert.NotNull((await new EntityGridProvider(scopes, Catalog).QueryAsync(Grid("skills-grid"), CancellationToken.None)).Items);          // View is allowed
        var refused = await new EntityCreateAction(scopes, Catalog).ExecuteAsync(Invoke("skills-create", ("name", "Sneaky")), CancellationToken.None);   // Create is not
        Assert.False(refused.Ok);
        using var ctx = PemsDbContextFactory.CreateInMemory(db);
        Assert.Empty(await ctx.Skills.ToListAsync());
    }

    [Fact]
    public void The_lookup_parameters_are_checked_at_load()
    {
        var grid = new EntityGridProvider(null!, Catalog);
        JsonObject P(string json) => JsonNode.Parse(json)!.AsObject();
        Assert.Empty(grid.ValidateParameters(P("""{"entity":"Skill","fields":["Name","DisciplineId"],"lookups":[{"name":"discipline","entity":"Discipline","foreignKey":"DisciplineId","label":"Name"}]}""")));
        Assert.Contains("unknown-entity", grid.ValidateParameters(P("""{"entity":"Skill","fields":["Name"],"lookups":[{"name":"x","entity":"Nope","foreignKey":"DisciplineId","label":"Name"}]}""")).Select(e => e.Code));
        Assert.Contains("not-readable", grid.ValidateParameters(P("""{"entity":"Skill","fields":["Name"],"lookups":[{"name":"x","entity":"Discipline","foreignKey":"Secret","label":"Name"}]}""")).Select(e => e.Code));
        Assert.Contains("not-readable", grid.ValidateParameters(P("""{"entity":"Skill","fields":["Name"],"lookups":[{"name":"x","entity":"Discipline","foreignKey":"DisciplineId","label":"Secret"}]}""")).Select(e => e.Code));
        Assert.Contains("not-exposed", grid.ValidateParameters(P("""{"entity":"Skill","fields":["Name"],"sort":["Description"]}""")).Select(e => e.Code));
    }

    [Fact]
    public void The_deny_rules_are_checked_at_load_and_decide_at_run_time()
    {
        var update = new EntityUpdateAction(null!, Catalog);
        JsonObject P(string json) => JsonNode.Parse(json)!.AsObject();
        Assert.Empty(update.ValidateParameters(P("""{"entity":"Skill","map":{"name":"Name"},"deny":[{"when":"row.IsActive == false","message":{"en":"Inactive","fa":"غیرفعال"}}]}""")));
        Assert.Contains("Def_Err_Expression", update.ValidateParameters(P("""{"entity":"Skill","map":{"name":"Name"},"deny":[{"when":"row.IsActive ==","message":{"en":"a","fa":"ب"}}]}""")).Select(e => e.MessageKey));
        Assert.Contains("Def_Err_Expression", update.ValidateParameters(P("""{"entity":"Skill","map":{"name":"Name"},"deny":[{"when":"page.title == 1","message":{"en":"a","fa":"ب"}}]}""")).Select(e => e.MessageKey));   // a root the rule may not read
        Assert.Contains("missing-translation", update.ValidateParameters(P("""{"entity":"Skill","map":{"name":"Name"},"deny":[{"when":"true","message":{"en":"a"}}]}""")).Select(e => e.Code));
    }
}
