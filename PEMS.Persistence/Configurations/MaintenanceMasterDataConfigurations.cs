using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PEMS.Domain.Entities;

namespace PEMS.Persistence.Configurations;

/// <summary>
/// طبق «PEMS — Enterprise Maintenance Master Data &amp; Standards Implementation»: پیکربندی EF
/// Core برای ۵ Master Data جدید + ۳ جدول الحاقی کاربردپذیری. طبق بند ۱۹ (Data Quality Rules):
/// Code هرکدام Unique است (نه Count()+1)، و جدول‌های الحاقی با کلید ترکیبی (بدون Id مجزا) رابطه‌ی
/// چند-به-چند را داده‌محور نگه می‌دارند.
/// </summary>
public class MaintenanceTypeConfiguration : IEntityTypeConfiguration<MaintenanceType>
{
    public void Configure(EntityTypeBuilder<MaintenanceType> builder)
    {
        builder.ToTable("MaintenanceTypes");
        builder.HasKey(x => x.MaintenanceTypeId);

        builder.Property(x => x.Code).HasMaxLength(30).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(150).IsRequired();
        builder.Property(x => x.NameFa).HasMaxLength(150);

        builder.HasIndex(x => x.Code).IsUnique();
    }
}

public class MaintenanceStrategyTypeConfiguration : IEntityTypeConfiguration<MaintenanceStrategyType>
{
    public void Configure(EntityTypeBuilder<MaintenanceStrategyType> builder)
    {
        builder.ToTable("MaintenanceStrategyTypes");
        builder.HasKey(x => x.MaintenanceStrategyTypeId);

        builder.Property(x => x.Code).HasMaxLength(30).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(150).IsRequired();
        builder.Property(x => x.NameFa).HasMaxLength(150);

        builder.HasIndex(x => x.Code).IsUnique();
    }
}

public class EquipmentClassConfiguration : IEntityTypeConfiguration<EquipmentClass>
{
    public void Configure(EntityTypeBuilder<EquipmentClass> builder)
    {
        builder.ToTable("EquipmentClasses");
        builder.HasKey(x => x.EquipmentClassId);

        builder.Property(x => x.Code).HasMaxLength(30).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(150).IsRequired();
        builder.Property(x => x.NameFa).HasMaxLength(150);

        builder.HasIndex(x => x.Code).IsUnique();
    }
}

public class StandardReferenceConfiguration : IEntityTypeConfiguration<StandardReference>
{
    public void Configure(EntityTypeBuilder<StandardReference> builder)
    {
        builder.ToTable("StandardReferences");
        builder.HasKey(x => x.StandardReferenceId);

        builder.Property(x => x.Code).HasMaxLength(30).IsRequired();
        builder.Property(x => x.Organization).HasMaxLength(50).IsRequired();
        builder.Property(x => x.StandardNumber).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Title).HasMaxLength(300).IsRequired();
        builder.Property(x => x.TitleFa).HasMaxLength(300);
        builder.Property(x => x.Category).HasMaxLength(100);

        // طبق بند ۱۹: «duplicate standard references are prohibited»
        builder.HasIndex(x => x.Code).IsUnique();
        builder.HasIndex(x => new { x.Organization, x.StandardNumber }).IsUnique();
    }
}

public class MaintenanceActivityConfiguration : IEntityTypeConfiguration<MaintenanceActivity>
{
    public void Configure(EntityTypeBuilder<MaintenanceActivity> builder)
    {
        builder.ToTable("MaintenanceActivities");
        builder.HasKey(x => x.MaintenanceActivityId);

        // طبق ممیزی این پاس: برخلاف بقیه‌ی Master Data (که کدهای کوتاهی مثل "CONDITION_BASED"
        // دارند)، کد فعالیت‌ها می‌تواند توصیفی و طولانی باشد (مثلاً "ACT_DAMAGE_MECHANISM_
        // ASSESSMENT" = ۳۲ کاراکتر) - با ۳۰ کاراکتر Insert واقعی روی SQL Server با خطای
        // Truncation شکست می‌خورد (که کل Batch Seed را می‌شکند، نه فقط همون یک ردیف).
        builder.Property(x => x.Code).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.NameFa).HasMaxLength(200);

        builder.HasIndex(x => x.Code).IsUnique();

        // طبق بند ۱۹: «orphan Maintenance Activities are prohibited» - Group همیشه اجباری است
        builder.HasOne(x => x.MaintenanceGroup)
            .WithMany()
            .HasForeignKey(x => x.MaintenanceGroupId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.MaintenanceType)
            .WithMany()
            .HasForeignKey(x => x.MaintenanceTypeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class MaintenanceActivityEquipmentClassConfiguration : IEntityTypeConfiguration<MaintenanceActivityEquipmentClass>
{
    public void Configure(EntityTypeBuilder<MaintenanceActivityEquipmentClass> builder)
    {
        builder.ToTable("MaintenanceActivityEquipmentClasses");
        builder.HasKey(x => new { x.MaintenanceActivityId, x.EquipmentClassId });

        builder.HasOne(x => x.MaintenanceActivity)
            .WithMany(x => x.ApplicableEquipmentClasses)
            .HasForeignKey(x => x.MaintenanceActivityId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.EquipmentClass)
            .WithMany()
            .HasForeignKey(x => x.EquipmentClassId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class MaintenanceActivityStrategyTypeConfiguration : IEntityTypeConfiguration<MaintenanceActivityStrategyType>
{
    public void Configure(EntityTypeBuilder<MaintenanceActivityStrategyType> builder)
    {
        builder.ToTable("MaintenanceActivityStrategyTypes");
        builder.HasKey(x => new { x.MaintenanceActivityId, x.MaintenanceStrategyTypeId });

        builder.HasOne(x => x.MaintenanceActivity)
            .WithMany(x => x.ApplicableStrategyTypes)
            .HasForeignKey(x => x.MaintenanceActivityId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.MaintenanceStrategyType)
            .WithMany()
            .HasForeignKey(x => x.MaintenanceStrategyTypeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class MaintenanceActivityStandardReferenceConfiguration : IEntityTypeConfiguration<MaintenanceActivityStandardReference>
{
    public void Configure(EntityTypeBuilder<MaintenanceActivityStandardReference> builder)
    {
        builder.ToTable("MaintenanceActivityStandardReferences");
        builder.HasKey(x => new { x.MaintenanceActivityId, x.StandardReferenceId });

        builder.HasOne(x => x.MaintenanceActivity)
            .WithMany(x => x.ApplicableStandardReferences)
            .HasForeignKey(x => x.MaintenanceActivityId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.StandardReference)
            .WithMany()
            .HasForeignKey(x => x.StandardReferenceId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

// طبق بند ۱۷: ارتباط افزودنی/اختیاری Equipment → EquipmentClass (Nullable، بدون شکستن هیچ
// کد/Import موجودی که فقط EquipmentType رشته‌ای را می‌نویسد)
public class EquipmentClassLinkConfiguration : IEntityTypeConfiguration<Equipment>
{
    public void Configure(EntityTypeBuilder<Equipment> builder)
    {
        builder.HasOne(x => x.EquipmentClassRef)
            .WithMany()
            .HasForeignKey(x => x.EquipmentClassId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

// طبق بند ۹: ارتباط افزودنی/اختیاری MaintenanceStrategy (رکورد تراکنشی RCM) → MaintenanceStrategyType
public class MaintenanceStrategyTypeLinkConfiguration : IEntityTypeConfiguration<MaintenanceStrategy>
{
    public void Configure(EntityTypeBuilder<MaintenanceStrategy> builder)
    {
        builder.HasOne(x => x.MaintenanceStrategyTypeRef)
            .WithMany()
            .HasForeignKey(x => x.MaintenanceStrategyTypeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
