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

        // طبق فاز ۱ سند CM: پیوند اختیاری به پروفایل ارزیابی - Restrict، مثل Equipment بالا
        builder.HasOne(x => x.EvaluationProfile)
            .WithMany()
            .HasForeignKey(x => x.EvaluationProfileId)
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

        // طبق فاز ۱ سند CM: HasDefaultValue صریح - بدون آن، قرائت‌های موجود این ستون تازه را
        // "" می‌گیرند نه "NotEvaluated" (بند ۲۹: هرگز نباید به‌صورت خاموش Normal/خالی فرض شود).
        builder.Property(x => x.EvaluationStatus).HasMaxLength(30).IsRequired().HasDefaultValue("NotEvaluated");

        builder.HasIndex(x => new { x.ConditionMonitoringPointId, x.ReadingDate })
            .HasDatabaseName("IX_ConditionReadings_PointId_ReadingDate");

        // طبق «PEMS — Equipment Passport Audit & CBM» فاز ۱ ممیزی §3.3: پیوند اختیاری - حذف یک
        // Instrument نباید هیچ قرائت تاریخی را پاک کند (Append-only).
        builder.HasOne(x => x.Instrument)
            .WithMany()
            .HasForeignKey(x => x.InstrumentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
