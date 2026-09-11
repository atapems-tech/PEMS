using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PEMS.Domain.Entities;

namespace PEMS.Persistence.Configurations;

/// <summary>
/// طبق «PEMS Phase 9» بند ۱۷: هر سه FK (Equipment/EquipmentClass/ReviewedBy) عمداً Restrict
/// هستند - همان استدلال CriticalityAssessmentConfiguration (بند ۱۱: رکورد شواهد/بازبینی هرگز
/// نباید با حذف زنجیره‌ای یک رکورد مرتبط بی‌صدا ناپدید شود).
/// </summary>
public class AIReliabilityInsightConfiguration : IEntityTypeConfiguration<AIReliabilityInsight>
{
    public void Configure(EntityTypeBuilder<AIReliabilityInsight> builder)
    {
        builder.HasOne(x => x.Equipment)
            .WithMany()
            .HasForeignKey(x => x.EquipmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.EquipmentClass)
            .WithMany()
            .HasForeignKey(x => x.EquipmentClassId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ReviewedBy)
            .WithMany()
            .HasForeignKey(x => x.ReviewedById)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.ReviewStatus)
            .HasDatabaseName("IX_AIReliabilityInsights_ReviewStatus");
    }
}
