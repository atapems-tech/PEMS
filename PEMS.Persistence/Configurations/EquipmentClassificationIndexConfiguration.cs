using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PEMS.Domain.Entities;

namespace PEMS.Persistence.Configurations;

/// <summary>
/// طبق «PEMS Phase 3 — Equipment Classification Completion» بند ۲۵: Dashboard/Backfill/Review
/// Queue همه روی (IsActive, EquipmentClassId) فیلتر می‌کنند (چه IS NULL چه IS NOT NULL روی
/// ۲۱٬۰۰۰+ ردیف) - بدون ایندکس این یک Full Table Scan مکرر می‌شود. کانفیگ افزودنی مجزا (نه
/// تغییر یک کلاس کانفیگ موجود Equipment) طبق الگوی چندکلاسه‌ی همین پروژه.
/// </summary>
public class EquipmentClassificationIndexConfiguration : IEntityTypeConfiguration<Equipment>
{
    public void Configure(EntityTypeBuilder<Equipment> builder)
    {
        builder.HasIndex(x => new { x.IsActive, x.EquipmentClassId })
            .HasDatabaseName("IX_Equipments_IsActive_EquipmentClassId");

        builder.HasIndex(x => x.ClassificationConfidence)
            .HasDatabaseName("IX_Equipments_ClassificationConfidence");

        builder.HasIndex(x => x.ClassificationSource)
            .HasDatabaseName("IX_Equipments_ClassificationSource");
    }
}
