using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PEMS.Domain.Entities;

namespace PEMS.Persistence.Configurations;

/// <summary>
/// طبق «PEMS Phase 9» و تجربه‌ی مستقیم فاز ۷.۶ (CriticalityAssessmentConfiguration):
/// FK به Equipment عمداً Restrict است - رکورد پایش وضعیت هرگز نباید با حذف زنجیره‌ای Equipment
/// بی‌صدا ناپدید شود. FK از ConditionReading به ConditionMonitoringPoint یک مسیر ساده‌ی
/// والد-فرزند است، پس Cascade امن است (بدون ریسک چرخه/مسیر چندگانه).
/// </summary>
public class ConditionMonitoringPointConfiguration : IEntityTypeConfiguration<ConditionMonitoringPoint>
{
    public void Configure(EntityTypeBuilder<ConditionMonitoringPoint> builder)
    {
        builder.HasOne(x => x.Equipment)
            .WithMany()
            .HasForeignKey(x => x.EquipmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.EquipmentId)
            .HasDatabaseName("IX_ConditionMonitoringPoints_EquipmentId");
    }
}

public class ConditionReadingConfiguration : IEntityTypeConfiguration<ConditionReading>
{
    public void Configure(EntityTypeBuilder<ConditionReading> builder)
    {
        builder.HasOne(x => x.ConditionMonitoringPoint)
            .WithMany(x => x.Readings)
            .HasForeignKey(x => x.ConditionMonitoringPointId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.ConditionMonitoringPointId, x.ReadingDate })
            .HasDatabaseName("IX_ConditionReadings_PointId_ReadingDate");
    }
}
