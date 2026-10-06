using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PEMS.Domain.Entities;

namespace PEMS.Persistence.Configurations;

/// <summary>
/// طبق «PEMS — Standards-Based Mechanical Equipment Passport &amp; Condition Monitoring» فاز ۱/بند ۳۶:
/// Finding به شش جدول مستقل پیوند دارد (Equipment، ConditionReading، InspectionRecord، FailureMode،
/// FailureCause، WorkRequest) - همه Restrict، دقیقاً همان قرارداد کل کدبیس برای EquipmentId
/// (رجوع کنید به ConditionMonitoringConfiguration/CriticalityAssessmentConfiguration/...). Cascade
/// اینجا هرگز مناسب نیست چون Finding یک فرزند انحصاری هیچ‌کدام از این والدین نیست.
/// </summary>
public class FindingConfiguration : IEntityTypeConfiguration<Finding>
{
    public void Configure(EntityTypeBuilder<Finding> builder)
    {
        builder.HasOne(x => x.Equipment)
            .WithMany()
            .HasForeignKey(x => x.EquipmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ConditionReading)
            .WithMany()
            .HasForeignKey(x => x.ConditionReadingId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.InspectionRecord)
            .WithMany()
            .HasForeignKey(x => x.InspectionRecordId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.FailureMode)
            .WithMany()
            .HasForeignKey(x => x.FailureModeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.FailureCause)
            .WithMany()
            .HasForeignKey(x => x.FailureCauseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.WorkRequest)
            .WithMany()
            .HasForeignKey(x => x.WorkRequestId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.EquipmentId)
            .HasDatabaseName("IX_Findings_EquipmentId");

        builder.HasIndex(x => x.Status)
            .HasDatabaseName("IX_Findings_Status");
    }
}
