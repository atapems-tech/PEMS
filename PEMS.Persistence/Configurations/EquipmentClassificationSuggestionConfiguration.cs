using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PEMS.Domain.Entities;

namespace PEMS.Persistence.Configurations;

/// <summary>
/// طبق «PEMS Phase 3 — Equipment Classification Completion» بند ۹/۲۵: ایندکس روی
/// (EquipmentId, Status) چون Backfill به‌ازای هر Equipment قبل از ساخت یک Suggestion جدید،
/// وجود یک Suggestion PENDING موجود را بررسی می‌کند (Idempotency، بند ۱۳)؛ ایندکس روی Status
/// چون صف بازبینی همیشه با فیلتر Status='PENDING' Query می‌شود.
/// </summary>
public class EquipmentClassificationSuggestionConfiguration : IEntityTypeConfiguration<EquipmentClassificationSuggestion>
{
    public void Configure(EntityTypeBuilder<EquipmentClassificationSuggestion> builder)
    {
        builder.ToTable("EquipmentClassificationSuggestions");
        builder.HasKey(x => x.EquipmentClassificationSuggestionId);

        builder.Property(x => x.Confidence).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Source).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Reason).HasMaxLength(500).IsRequired();
        builder.Property(x => x.ReviewedBy).HasMaxLength(200);
        builder.Property(x => x.ReviewComment).HasMaxLength(500);

        builder.HasIndex(x => new { x.EquipmentId, x.Status });
        builder.HasIndex(x => x.Status);

        builder.HasOne(x => x.Equipment)
            .WithMany()
            .HasForeignKey(x => x.EquipmentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.SuggestedEquipmentClass)
            .WithMany()
            .HasForeignKey(x => x.SuggestedEquipmentClassId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
