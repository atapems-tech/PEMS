using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Seed;

/// <summary>
/// طبق «PEMS Phase 2 — ISO 14224 Failure Taxonomy» بند ۱۲/۳۱: خط‌مبنای Failure Mechanism
/// (چگونگی فیزیکی وقوع خرابی - سطح جزئی‌تر از Cause، طبق API 571/ISO 14224).
/// </summary>
public static class FailureMechanismSeeder
{
    /// <summary>SCRUM-803: the rows are the reference-data definition reference-data/core/failure-mechanism.json, applied by ReferenceDataApplier (clean-install parity and customer-edit tests: ReferenceDataApplierTests).</summary>
    public static Task SeedAsync(IServiceProvider services)
        => PEMS.Persistence.Definitions.ReferenceDataCatalog.ApplyAsync(services.GetRequiredService<PEMSDbContext>(), "failure-mechanism");
}
