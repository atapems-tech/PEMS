using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Persistence.Context;
using PEMS.Domain.Entities;

namespace PEMS.Persistence.Seed;

/// <summary>
/// دیسیپلین‌های استاندارد QC/QA طبق رویه‌ی واقعی صنعت پتروشیمی/EPC - جدا از دیسیپلین‌های
/// مهندسی طراحی (Discipline). این دیسیپلین‌ها بر اساس روش/حوزه‌ی بازرسی تعریف می‌شوند.
/// </summary>
public static class QCDisciplineSeeder
{
    /// <summary>SCRUM-803: the rows are the reference-data definition reference-data/core/qc-discipline.json, applied by ReferenceDataApplier (clean-install parity and customer-edit tests: ReferenceDataApplierTests).</summary>
    public static Task SeedAsync(IServiceProvider services)
        => PEMS.Persistence.Definitions.ReferenceDataCatalog.ApplyAsync(services.GetRequiredService<PEMSDbContext>(), "qc-discipline");
}
