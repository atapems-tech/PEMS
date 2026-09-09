using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PEMS.Domain.Entities;

namespace PEMS.Persistence.Configurations;

/// <summary>
/// طبق «PEMS Phase 7» بند ۲۰: هر دو FK به Equipment (مستقیم) و MaintenanceStrategy (که خودش هم
/// به Equipment وصل است) یک مسیر Cascade دوگانه می‌سازند - SQL Server این را رد می‌کند
/// ("may cause cycles or multiple cascade paths"، دقیقاً همان الگویی که
/// CalibrationHistoryConfiguration از قبل با Restrict حل کرده). علاوه بر این، یک رکورد
/// شواهد/بازبینی هرگز نباید با حذف زنجیره‌ای Equipment/Strategy بی‌صدا ناپدید شود - Restrict
/// این را عمداً اجباری می‌کند.
/// </summary>
public class RcmReviewRecommendationConfiguration : IEntityTypeConfiguration<RcmReviewRecommendation>
{
    public void Configure(EntityTypeBuilder<RcmReviewRecommendation> builder)
    {
        builder.HasOne(x => x.Equipment)
            .WithMany()
            .HasForeignKey(x => x.EquipmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.MaintenanceStrategy)
            .WithMany()
            .HasForeignKey(x => x.MaintenanceStrategyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
