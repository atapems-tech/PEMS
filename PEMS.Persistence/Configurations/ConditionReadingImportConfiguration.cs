using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PEMS.Domain.Entities;

namespace PEMS.Persistence.Configurations;

/// <summary>
/// طبق «PEMS — Standards-Based Mechanical Equipment Passport & Condition Monitoring» فاز ۹:
/// دقیقاً همون الگوی MaintenanceHistoryImportBatch/Row (PEMSDbContext.OnModelCreating) - Batch→
/// Employee Restrict، Row→Batch Cascade (فرزند انحصاری، بدون مسیر چندگانه).
/// </summary>
public class ConditionReadingImportBatchConfiguration : IEntityTypeConfiguration<ConditionReadingImportBatch>
{
    public void Configure(EntityTypeBuilder<ConditionReadingImportBatch> builder)
    {
        builder.HasOne(x => x.ImportedBy)
            .WithMany()
            .HasForeignKey(x => x.ImportedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class ConditionReadingImportRowConfiguration : IEntityTypeConfiguration<ConditionReadingImportRow>
{
    public void Configure(EntityTypeBuilder<ConditionReadingImportRow> builder)
    {
        builder.HasOne(x => x.Batch)
            .WithMany(x => x.Rows)
            .HasForeignKey(x => x.ConditionReadingImportBatchId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
