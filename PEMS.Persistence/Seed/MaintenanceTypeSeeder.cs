using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Seed;

/// <summary>
/// طبق «PEMS — Enterprise Maintenance Master Data &amp; Standards Implementation» بند ۶: خط‌مبنای
/// استاندارد Maintenance Type - بررسی به‌ازای هر Code (نه AnyAsync سراسری)، Additive/Idempotent.
/// </summary>
public static class MaintenanceTypeSeeder
{
    /// <summary>SCRUM-803: the rows are the reference-data definition reference-data/core/maintenance-type.json, applied by ReferenceDataApplier (clean-install parity and customer-edit tests: ReferenceDataApplierTests).</summary>
    public static Task SeedAsync(IServiceProvider services)
        => PEMS.Persistence.Definitions.ReferenceDataCatalog.ApplyAsync(services.GetRequiredService<PEMSDbContext>(), "maintenance-type");
}
