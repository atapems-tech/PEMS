using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.PartCatalogue;

/// <summary>
/// Regression guard for the Part Catalogue schema contract: the request entity must only map columns that exist in the committed
/// migration chain. A model property without an applied migration makes every query that loads the full entity (details, edit,
/// save, approve) fail with "Invalid column name ..." while list/summary projections keep working. The four traceability columns
/// (MaterialItemId, SourceWorkOrderId, SourceStoreRequisitionId, SourceMaterialRequisitionId) were never approved as a schema change.
/// </summary>
public class PartCatalogueSchemaContractTests : IAsyncLifetime
{
    private static readonly string[] UnapprovedColumns =
        { "MaterialItemId", "SourceWorkOrderId", "SourceStoreRequisitionId", "SourceMaterialRequisitionId" };

    private readonly string _dbName = "PEMS_TEST_PCSCHEMA_" + Guid.NewGuid().ToString("N")[..12];
    private string _connection = "";

    private sealed class CapturingInterceptor : DbCommandInterceptor
    {
        public List<string> Commands { get; } = new();
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            lock (Commands) Commands.Add(command.CommandText);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }

    public async Task InitializeAsync()
    {
        _connection = new SqlConnectionStringBuilder(PemsDbContextFactory.ConnectionString) { InitialCatalog = _dbName }.ConnectionString;
        await using var ctx = NewCtx();
        await ctx.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        SqlConnection.ClearAllPools();
        var master = new SqlConnectionStringBuilder(_connection) { InitialCatalog = "master" }.ConnectionString;
        await using var c = new SqlConnection(master);
        await c.OpenAsync();
        await using var cmd = c.CreateCommand();
        cmd.CommandText = $"IF DB_ID('{_dbName}') IS NOT NULL BEGIN ALTER DATABASE [{_dbName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{_dbName}]; END";
        await cmd.ExecuteNonQueryAsync();
    }

    private PEMSDbContext NewCtx(CapturingInterceptor? capture = null)
    {
        var b = new DbContextOptionsBuilder<PEMSDbContext>().UseSqlServer(_connection);
        if (capture != null) b.AddInterceptors(capture);
        return new PEMSDbContext(b.Options);
    }

    [Fact]
    public void RequestModel_MapsNoUnapprovedTraceabilityColumns_AndHasNoNavigationToThem()
    {
        using var ctx = new PEMSDbContext(new DbContextOptionsBuilder<PEMSDbContext>().UseSqlServer("Server=.;Database=ignored").Options);

        var entity = ctx.Model.FindEntityType(typeof(PartCatalogueRequest))!;
        var columns = entity.GetProperties().Select(p => p.GetColumnName(StoreObjectIdentifier.Table("PartCatalogueRequests", null))).ToList();

        foreach (var forbidden in UnapprovedColumns) Assert.DoesNotContain(forbidden, columns);
        Assert.DoesNotContain(entity.GetNavigations(), n => n.Name.StartsWith("Source") || n.Name == "MaterialItem");
    }

    [Fact]
    public void RequestQueries_GenerateNoSqlReferencingTheUnapprovedColumns()
    {
        using var ctx = new PEMSDbContext(new DbContextOptionsBuilder<PEMSDbContext>().UseSqlServer("Server=.;Database=ignored").Options);

        // همان شکل Queryهای GetDetailsAsync / LoadTrackedAsync (کل Entity) - بدون اجرا، فقط SQL تولیدی
        var details = ctx.PartCatalogueRequests.AsNoTracking()
            .Include(r => r.Attributes).Include(r => r.Section).Include(r => r.Manufacturer)
            .Where(r => r.PartCatalogueRequestId == 1 && r.IsActive).ToQueryString();
        var tracked = ctx.PartCatalogueRequests.Include(r => r.Attributes).Where(r => r.PartCatalogueRequestId == 1).ToQueryString();

        foreach (var sql in new[] { details, tracked })
            foreach (var forbidden in UnapprovedColumns)
                Assert.DoesNotContain(forbidden, sql);
    }

    [Fact]
    public async Task FullEntityDetailsQuery_ExecutesOnTheCommittedMigrationSchema_WithoutTheUnapprovedColumns()
    {
        await using (var seed = NewCtx())
        {
            var dep = new Department { Code = "D1", Name = "Dept" };
            var disc = new Discipline { Code = "X1", Name = "Disc" };
            seed.AddRange(dep, disc);
            await seed.SaveChangesAsync();
            var emp = new Employee { FirstName = "A", LastName = "B", DepartmentId = dep.DepartmentId, DisciplineId = disc.DisciplineId };
            seed.Employees.Add(emp);
            await seed.SaveChangesAsync();
            seed.PartCatalogueRequests.Add(new PartCatalogueRequest { RequesterEmployeeId = emp.EmployeeId, Status = PartCatalogueStatuses.Draft });
            await seed.SaveChangesAsync();
        }

        var capture = new CapturingInterceptor();
        await using var ctx = NewCtx(capture);

        // همان زنجیره‌ی Include / AsSplitQuery / FirstOrDefaultAsync مسیر GetDetailsAsync
        var e = await ctx.PartCatalogueRequests.AsNoTracking()
            .Include(r => r.Attributes).Include(r => r.Steps).Include(r => r.Approvals).Include(r => r.Notes)
            .Include(r => r.Equipment).ThenInclude(q => q.Equipment)
            .Include(r => r.Attachments)
            .Include(r => r.Section).Include(r => r.TechnicalClass).Include(r => r.CommodityGroup1).Include(r => r.CommodityGroup2)
            .Include(r => r.ProductFamily).Include(r => r.UnitOfMeasure).Include(r => r.Manufacturer).Include(r => r.Supplier)
            .Include(r => r.Currency).Include(r => r.TypeDesignation).Include(r => r.PlanningMethod).Include(r => r.StockGrade)
            .AsSplitQuery()
            .FirstOrDefaultAsync(r => r.IsActive);

        Assert.NotNull(e);
        Assert.NotEmpty(capture.Commands);
        foreach (var sql in capture.Commands)
            foreach (var forbidden in UnapprovedColumns)
                Assert.DoesNotContain(forbidden, sql);

        // و ستون‌ها واقعاً در Schema ناشی از Migrationهای Commit‌شده وجود ندارند (یعنی Model و Schema هم‌خوان‌اند)
        await using var check = NewCtx();
        var existing = await check.Database.SqlQueryRaw<string>(
            "SELECT name AS Value FROM sys.columns WHERE object_id = OBJECT_ID('PartCatalogueRequests')").ToListAsync();
        foreach (var forbidden in UnapprovedColumns) Assert.DoesNotContain(forbidden, existing);
    }
}
