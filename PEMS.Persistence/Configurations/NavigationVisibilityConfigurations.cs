using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PEMS.Domain.Entities;
using PEMS.Persistence.Identity;

namespace PEMS.Persistence.Configurations;

/// <summary>
/// طبق سند «PEMS Phase 6»: عمداً در فایلی جدا از AccessControlConfigurations.cs - این جدول
/// بخشی از موتور Authorization نیست (Visibility یک نگرانی Presentation/Navigation است)، فقط از
/// همان الگوی FK-بدون-Navigation-Property به ApplicationRole پیروی می‌کند (هم‌الگو با
/// RolePermissionGrant).
/// </summary>
public class NavigationVisibilityOverrideConfiguration : IEntityTypeConfiguration<NavigationVisibilityOverride>
{
    public void Configure(EntityTypeBuilder<NavigationVisibilityOverride> builder)
    {
        builder.ToTable("NavigationVisibilityOverrides");

        builder.HasIndex(x => new { x.RoleId, x.ModuleKey, x.ItemKey }).IsUnique();

        builder.Property(x => x.ModuleKey).HasMaxLength(100).IsRequired();
        builder.Property(x => x.ItemKey).HasMaxLength(150);
        builder.Property(x => x.Reason).HasMaxLength(500);
        builder.Property(x => x.CreatedBy).HasMaxLength(200);
        builder.Property(x => x.ModifiedBy).HasMaxLength(200);

        builder.HasOne<ApplicationRole>()
            .WithMany()
            .HasForeignKey(x => x.RoleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
