using Microsoft.EntityFrameworkCore;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;
using PEMS.Persistence.Definitions;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Definitions;

/// <summary>
/// SCRUM-803: the applier writes a shipped list into its lookup table by the update rule. Clean install gives exactly the rows of the old seeder; a customer's edit survives an update; a row the product stopped
/// shipping is retired, never deleted; a row that exists with the shipped values is adopted.
/// </summary>
public sealed class ReferenceDataApplierTests
{
    private static PEMSDbContext Db() => PemsDbContextFactory.CreateInMemory("refdata-" + Guid.NewGuid());

    private static string Uom(params (string Code, string Name)[] rows)
        => "{ \"list\": \"material-uom\", \"owner\": \"platform\", \"rows\": [" + string.Join(",", rows.Select(r => $"{{ \"key\": \"{r.Code}\", \"values\": {{ \"name\": \"{r.Name}\" }} }}")) + "] }";

    private static ReferenceListBinding<MaterialUOM> Binding => new("material-uom", nameof(MaterialUOM.Code), nameof(MaterialUOM.IsActive), e => e.CreatedDate = DateTime.Now);

    public static IEnumerable<object[]> Lists() => ReferenceDataCatalog.ListKeys.Select(l => new object[] { l });

    [Fact]
    public void Four_lists_ship_one_per_module_family()
        => Assert.Equal(["failure-mechanism", "maintenance-type", "material-uom", "qc-discipline"], ReferenceDataCatalog.ListKeys.OrderBy(k => k, StringComparer.Ordinal));

    [Theory]
    [MemberData(nameof(Lists))]
    public void A_shipped_list_is_a_valid_definition_with_unique_rows(string list)
        => Assert.Empty(new PEMS.Application.Definitions.ReferenceDataKind().Validate(ReferenceDataCatalog.LoadPayload(list), new PEMS.Application.Definitions.DefinitionLimits()));

    [Fact]
    public async Task A_clean_install_gets_the_rows_of_the_old_seeder_and_applying_again_changes_nothing()
    {
        var db = Db();
        var first = await ReferenceDataCatalog.ApplyAsync(db, "material-uom");
        var again = await ReferenceDataCatalog.ApplyAsync(db, "material-uom");

        Assert.Equal(12, first.Inserted);
        Assert.Equal((0, 0, 0, 0), (again.Inserted, again.Updated, again.Adopted, again.Retired));
        var codes = await db.MaterialUOMs.OrderBy(x => x.Code).Select(x => x.Code).ToListAsync();
        Assert.Equal(["BAG", "BOX", "DRUM", "KG", "LT", "MT", "MTR", "NO", "PAIR", "PCS", "ROLL", "SET"], codes);
        Assert.All(await db.MaterialUOMs.ToListAsync(), x => Assert.True(x.IsActive));
        Assert.Equal(12, await db.ReferenceDataRowStates.CountAsync());
    }

    [Fact]
    public async Task The_maintenance_types_keep_their_order_and_the_system_default_mark()
    {
        var db = Db();
        await ReferenceDataCatalog.ApplyAsync(db, "maintenance-type");

        var corrective = await db.MaintenanceTypes.SingleAsync(x => x.Code == "CORRECTIVE");
        Assert.Equal(("Corrective", "اصلاحی", 10, true, true), (corrective.Name, corrective.NameFa, corrective.DisplayOrder, corrective.IsSystemDefault, corrective.IsActive));
        Assert.Equal(18, await db.MaintenanceTypes.CountAsync());
    }

    [Fact]
    public async Task A_customer_edit_survives_an_update_and_is_reported_as_upstream_changed()
    {
        var db = Db();
        var applier = new ReferenceDataApplier(db);
        await applier.ApplyAsync(Uom(("KG", "Kilogram"), ("LT", "Liter")), Binding);
        (await db.MaterialUOMs.SingleAsync(x => x.Code == "KG")).Name = "Kilo (our name)";
        await db.SaveChangesAsync();

        var result = await applier.ApplyAsync(Uom(("KG", "Kilogramme"), ("LT", "Litre")), Binding);

        Assert.Equal("Kilo (our name)", (await db.MaterialUOMs.SingleAsync(x => x.Code == "KG")).Name);
        Assert.Equal("Litre", (await db.MaterialUOMs.SingleAsync(x => x.Code == "LT")).Name);   // unedited: updated by us
        Assert.Equal(1, result.Updated);
        Assert.Equal(["KG: name"], result.UpstreamChanged);
    }

    [Fact]
    public async Task A_row_the_customer_created_is_never_touched_even_with_the_same_key()
    {
        var db = Db();
        db.MaterialUOMs.Add(new MaterialUOM { Code = "KG", Name = "Our kilo", IsActive = true });
        await db.SaveChangesAsync();

        var result = await new ReferenceDataApplier(db).ApplyAsync(Uom(("KG", "Kilogram")), Binding);

        Assert.Equal("Our kilo", (await db.MaterialUOMs.SingleAsync()).Name);
        Assert.Equal((0, 0, 0), (result.Inserted, result.Updated, result.Adopted));
        Assert.Equal(["KG: name"], result.UpstreamChanged);
    }

    [Fact]
    public async Task A_row_that_already_has_the_shipped_values_is_adopted_and_then_updated_by_the_product()
    {
        var db = Db();
        db.MaterialUOMs.Add(new MaterialUOM { Code = "kg", Name = "Kilogram", IsActive = true });   // an install made by the old seeder, other case of the key
        await db.SaveChangesAsync();
        var applier = new ReferenceDataApplier(db);

        var first = await applier.ApplyAsync(Uom(("KG", "Kilogram")), Binding);
        var second = await applier.ApplyAsync(Uom(("KG", "Kilogramme")), Binding);

        Assert.Equal((0, 1), (first.Inserted, first.Adopted));
        Assert.Equal(1, second.Updated);
        Assert.Equal("Kilogramme", (await db.MaterialUOMs.SingleAsync()).Name);
    }

    [Fact]
    public async Task A_row_the_product_stops_shipping_is_retired_not_deleted_and_a_customer_row_stays()
    {
        var db = Db();
        var applier = new ReferenceDataApplier(db);
        await applier.ApplyAsync(Uom(("KG", "Kilogram"), ("LT", "Liter")), Binding);
        db.MaterialUOMs.Add(new MaterialUOM { Code = "OURS", Name = "Ours", IsActive = true });
        await db.SaveChangesAsync();

        var result = await applier.ApplyAsync(Uom(("KG", "Kilogram")), Binding);

        Assert.Equal(1, result.Retired);
        Assert.False((await db.MaterialUOMs.SingleAsync(x => x.Code == "LT")).IsActive);
        Assert.True((await db.MaterialUOMs.SingleAsync(x => x.Code == "OURS")).IsActive);
        Assert.Equal(3, await db.MaterialUOMs.CountAsync());
    }

    [Fact]
    public async Task A_list_with_an_unknown_property_is_refused()
    {
        var bad = "{ \"list\": \"material-uom\", \"owner\": \"platform\", \"rows\": [ { \"key\": \"KG\", \"values\": { \"colour\": \"red\" } } ] }";
        await Assert.ThrowsAsync<InvalidOperationException>(() => new ReferenceDataApplier(Db()).ApplyAsync(bad, Binding));
    }
}
