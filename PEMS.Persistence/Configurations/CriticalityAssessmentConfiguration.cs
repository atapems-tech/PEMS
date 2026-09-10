using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PEMS.Domain.Entities;

namespace PEMS.Persistence.Configurations;

/// <summary>
/// طبق «PEMS Phase 7.6»: طبق تجربه‌ی مستقیم فاز ۷ (RcmReviewRecommendations - خطای SQL Server
/// "may cause cycles or multiple cascade paths")، هر دو FK این‌جا عمداً Restrict هستند:
/// SupersededByAssessmentId چون خودارجاع است (Cascade روی خودارجاع همیشه توسط SQL Server رد
/// می‌شود)، و EquipmentId چون یک رکورد ارزیابی/شواهد هرگز نباید با حذف زنجیره‌ای Equipment
/// بی‌صدا ناپدید شود (همان استدلال CalibrationHistoryConfiguration/RcmReviewRecommendationConfiguration).
/// </summary>
public class CriticalityAssessmentConfiguration : IEntityTypeConfiguration<CriticalityAssessment>
{
    public void Configure(EntityTypeBuilder<CriticalityAssessment> builder)
    {
        builder.HasOne(x => x.Equipment)
            .WithMany()
            .HasForeignKey(x => x.EquipmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.SupersededByAssessment)
            .WithMany()
            .HasForeignKey(x => x.SupersededByAssessmentId)
            .OnDelete(DeleteBehavior.Restrict);

        // طبق بند ۹/۲۳: صفحه‌بندی Queue بر اساس Status/Equipment فیلتر می‌شود - همان الگوی
        // Query جدید این فاز.
        builder.HasIndex(x => new { x.EquipmentId, x.Status })
            .HasDatabaseName("IX_CriticalityAssessments_EquipmentId_Status");
    }
}

public class CriticalityAssessmentEvidenceConfiguration : IEntityTypeConfiguration<CriticalityAssessmentEvidence>
{
    public void Configure(EntityTypeBuilder<CriticalityAssessmentEvidence> builder)
    {
        builder.HasOne(x => x.CriticalityAssessment)
            .WithMany(x => x.Evidence)
            .HasForeignKey(x => x.CriticalityAssessmentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
