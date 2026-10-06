using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PEMS.Domain.Entities;

namespace PEMS.Persistence.Configurations;

/// <summary>
/// طبق «PEMS — Standards-Based Mechanical Equipment Passport &amp; Condition Monitoring» فاز ۱:
/// همه‌ی FKهای این پروفایل به جداول مرجع/طبقه‌بندی اختیاری‌اند (Restrict) - دقیقاً همان قرارداد
/// FailureTaxonomyConfigurations برای StandardReference/EquipmentClass. حذف یک EvaluationProfile
/// هرگز نباید زنجیره‌ای به ConditionMonitoringPoint/ConditionReading برسد؛ چون آن دو Nullable FK
/// دارند، Restrict به‌تنهایی کافی است - رکورد پایش وضعیت هرگز به‌خاطر حذف پروفایل ناپدید نمی‌شود.
/// </summary>
public class EvaluationProfileConfiguration : IEntityTypeConfiguration<EvaluationProfile>
{
    public void Configure(EntityTypeBuilder<EvaluationProfile> builder)
    {
        builder.HasIndex(x => x.Code).IsUnique();

        builder.HasOne(x => x.StandardReference)
            .WithMany()
            .HasForeignKey(x => x.StandardReferenceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.EquipmentClass)
            .WithMany()
            .HasForeignKey(x => x.EquipmentClassId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.VariableType, x.EquipmentClassId, x.IsActive })
            .HasDatabaseName("IX_EvaluationProfiles_VariableType_EquipmentClassId_IsActive");
    }
}

public class EvaluationLimitConfiguration : IEntityTypeConfiguration<EvaluationLimit>
{
    public void Configure(EntityTypeBuilder<EvaluationLimit> builder)
    {
        // فرزند مستقیم پروفایل - بدون مسیر چندگانه به Equipment، پس Cascade امن است
        // (همان الگوی ConditionReading -> ConditionMonitoringPoint).
        builder.HasOne(x => x.EvaluationProfile)
            .WithMany(x => x.Limits)
            .HasForeignKey(x => x.EvaluationProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.EvaluationProfileId, x.Status })
            .HasDatabaseName("IX_EvaluationLimits_EvaluationProfileId_Status");
    }
}
