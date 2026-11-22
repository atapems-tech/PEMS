using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Seed;

/// <summary>طبق ممیزی فایل‌های MRS: واحدهای واقعی دیده‌شده در ستون UNT/UOM (LIT/LT/KG/PCS/NO/MT/ROLL...).</summary>
public static class MaterialUOMSeeder
{
    /// <summary>SCRUM-803: the rows are the reference-data definition reference-data/core/material-uom.json, applied by ReferenceDataApplier (clean-install parity and customer-edit tests: ReferenceDataApplierTests).</summary>
    public static Task SeedAsync(IServiceProvider services)
        => PEMS.Persistence.Definitions.ReferenceDataCatalog.ApplyAsync(services.GetRequiredService<PEMSDbContext>(), "material-uom");
}
