using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PEMS.Domain.Entities;

namespace PEMS.Persistence.Configurations;

/// <summary>
/// طبق بند ۱۷ سند: حداکثر یک Provider می‌تواند IsDefault=true باشد - با Unique Index فیلترشده
/// (همان الگوی EquipmentCriticalityConfiguration) در سطح دیتابیس تضمین می‌شود، نه فقط اعتبارسنجی UI.
/// </summary>
public class StorageProviderConfiguration : IEntityTypeConfiguration<StorageProvider>
{
    public void Configure(EntityTypeBuilder<StorageProvider> builder)
    {
        builder.HasIndex(x => x.Code)
            .IsUnique()
            .HasFilter("[IsActive] = 1");

        builder.HasIndex(x => x.IsDefault)
            .IsUnique()
            .HasFilter("[IsDefault] = 1 AND [IsActive] = 1");
    }
}

public class StorageRouteConfiguration : IEntityTypeConfiguration<StorageRoute>
{
    public void Configure(EntityTypeBuilder<StorageRoute> builder)
    {
        // طبق تجربه‌ی مستقیم این نشست (CriticalityAssessment/AIReliabilityInsight): Restrict
        // عمدی - حذف یک StorageProvider هرگز نباید Route های وابسته را زنجیره‌ای حذف کند؛
        // بند ۱۳ سند صریحاً حذف Provider دارای Route فعال را ممنوع می‌کند (در سرویس بررسی می‌شود).
        builder.HasOne(x => x.StorageProvider)
            .WithMany(x => x.Routes)
            .HasForeignKey(x => x.StorageProviderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.Module, x.DocumentType })
            .HasDatabaseName("IX_StorageRoutes_Module_DocumentType");
    }
}
