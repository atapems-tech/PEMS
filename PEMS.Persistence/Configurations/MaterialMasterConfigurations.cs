using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PEMS.Domain.Entities;

namespace PEMS.Persistence.Configurations;

public class MaterialCategoryConfiguration : IEntityTypeConfiguration<MaterialCategory>
{
    public void Configure(EntityTypeBuilder<MaterialCategory> builder)
    {
        builder.ToTable("MaterialCategories");
        builder.HasKey(x => x.MaterialCategoryId);

        builder.Property(x => x.Code).HasMaxLength(30).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(150).IsRequired();

        builder.HasIndex(x => x.Code).IsUnique();

        builder.HasOne(x => x.Parent)
            .WithMany(x => x.Children)
            .HasForeignKey(x => x.ParentCategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class MaterialUOMConfiguration : IEntityTypeConfiguration<MaterialUOM>
{
    public void Configure(EntityTypeBuilder<MaterialUOM> builder)
    {
        builder.ToTable("MaterialUOMs");
        builder.HasKey(x => x.MaterialUOMId);

        builder.Property(x => x.Code).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.Property(x => x.ConversionFactor).HasColumnType("decimal(18,6)");

        builder.HasIndex(x => x.Code).IsUnique();
    }
}

public class MaterialItemConfiguration : IEntityTypeConfiguration<MaterialItem>
{
    public void Configure(EntityTypeBuilder<MaterialItem> builder)
    {
        builder.ToTable("MaterialItems");
        builder.HasKey(x => x.MaterialItemId);

        builder.Property(x => x.MaterialCode).HasMaxLength(100).IsRequired();

        // طبق ممیزی «MaterialItems.Description String-Length Integrity»: سقف ۵۰۰ کاراکتری قبلی نه
        // در هیچ Migration/کامنت/Validation Attribute ای مستند بود و نه یک قاعده‌ی کسب‌وکار واقعی
        // داشت - همون Migration اولیه (MaterialMaster) که آن را ساخته، دقیقاً همان لحظه Description
        // را روی MaterialCategory و MaterialUOM بدون سقف (nvarchar(max)) گذاشته بود. داده‌ی واقعی
        // MRS (۱۱٬۸۳۰ ردیف Stage شده) نشان داد شرح‌های واقعی مهندسی (به‌خصوص کابل/ابزار دقیق دوزبانه)
        // به ۵۴۰ کاراکتر هم می‌رسند و رشد می‌کنند - نه یک مقدار پرت اشتباه. پس به‌جای انتخاب یک عدد
        // ثابت دلبخواهی دیگر (۱۰۰۰? ۲۰۰۰?)، Description مثل بقیه‌ی فیلدهای متنی آزاد همین Entity
        // (Specification, Model, Manufacturer, LongDescription, MaterialGrade, Size) بدون سقف
        // (nvarchar(max)) می‌شود - هماهنگ با الگوی از پیش موجود همین جدول، نه یک استثنا.
        builder.Property(x => x.Description).IsRequired();

        builder.HasIndex(x => x.MaterialCode).IsUnique();

        builder.HasOne(x => x.Category)
            .WithMany()
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Discipline)
            .WithMany()
            .HasForeignKey(x => x.DisciplineId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Uom)
            .WithMany()
            .HasForeignKey(x => x.UomId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Vendor)
            .WithMany()
            .HasForeignKey(x => x.VendorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
