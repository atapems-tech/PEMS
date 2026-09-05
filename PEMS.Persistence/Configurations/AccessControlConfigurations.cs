using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PEMS.Domain.Entities;
using PEMS.Persistence.Identity;

namespace PEMS.Persistence.Configurations;

/// <summary>
/// طبق سند RBAC-ABAC Authorization Architecture (فاز ۱ - مدل داده‌ی هسته): پیکربندی چهار
/// Entity جدید (Permission, RolePermissionGrant, RoleAssignment, AuthorizationAuditLog) در یک
/// فایل - هم‌الگو با DocumentCoreConfigurations.cs (یک فایل برای یک خانواده‌ی به‌هم‌مرتبط).
/// </summary>
public class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.ToTable("Permissions");

        builder.HasIndex(x => x.Name).IsUnique();
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.ResourceName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.ActionName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(500);
    }
}

public class RolePermissionGrantConfiguration : IEntityTypeConfiguration<RolePermissionGrant>
{
    public void Configure(EntityTypeBuilder<RolePermissionGrant> builder)
    {
        builder.ToTable("RolePermissionGrants");

        builder.HasIndex(x => new { x.RoleId, x.PermissionId }).IsUnique();

        // طبق الگوی WebAuthnCredential/PermitApprovalAuditRecord: Domain نباید به
        // Persistence.Identity وابسته شود - پیوند FK این‌جا (لایه‌ی Persistence) برقرار می‌شود
        builder.HasOne<ApplicationRole>()
            .WithMany()
            .HasForeignKey(x => x.RoleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Permission)
            .WithMany()
            .HasForeignKey(x => x.PermissionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class RoleAssignmentConfiguration : IEntityTypeConfiguration<RoleAssignment>
{
    public void Configure(EntityTypeBuilder<RoleAssignment> builder)
    {
        builder.ToTable("RoleAssignments");

        builder.HasIndex(x => new { x.UserId, x.RoleId, x.ScopeType, x.ScopeValue });
        builder.Property(x => x.ScopeType).HasMaxLength(50).IsRequired();
        builder.Property(x => x.ScopeValue).HasMaxLength(200);
        builder.Property(x => x.AssignmentType).HasMaxLength(30).IsRequired();
        builder.Property(x => x.Reason).HasMaxLength(500);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<ApplicationRole>()
            .WithMany()
            .HasForeignKey(x => x.RoleId)
            .OnDelete(DeleteBehavior.Restrict);

        // طبق درخواست: FK اختیاری به کاربری که تفویض کرده - Restrict تا حذف تفویض‌کننده
        // زنجیره‌ی تفویض‌های تاریخی را پاک نکند
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(x => x.DelegatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>
/// طبق سند RBAC-ABAC Authorization Architecture بخش ۱۰ (فاز ۲): «PermissionA CONFLICTS WITH
/// PermissionB» - دو FK مستقل به Permission (نه یک رابطه‌ی خودارجاع)، هرکدام نام محدودیت
/// جداگانه‌ی خودکار EF Core را می‌گیرند.
/// </summary>
public class SoDPolicyConfiguration : IEntityTypeConfiguration<SoDPolicy>
{
    public void Configure(EntityTypeBuilder<SoDPolicy> builder)
    {
        builder.ToTable("SoDPolicies");

        builder.Property(x => x.Description).HasMaxLength(500);

        builder.HasIndex(x => new { x.PermissionAId, x.PermissionBId }).IsUnique();

        builder.HasOne(x => x.PermissionA)
            .WithMany()
            .HasForeignKey(x => x.PermissionAId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.PermissionB)
            .WithMany()
            .HasForeignKey(x => x.PermissionBId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class AuthorizationAuditLogConfiguration : IEntityTypeConfiguration<AuthorizationAuditLog>
{
    public void Configure(EntityTypeBuilder<AuthorizationAuditLog> builder)
    {
        builder.ToTable("AuthorizationAuditLogs");

        builder.HasIndex(x => x.Timestamp);
        builder.HasIndex(x => new { x.UserId, x.Timestamp });

        builder.Property(x => x.ResourceName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.ActionName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.ResourceId).HasMaxLength(100);
        builder.Property(x => x.ScopeType).HasMaxLength(50);
        builder.Property(x => x.ScopeValue).HasMaxLength(200);
        builder.Property(x => x.Decision).HasMaxLength(30).IsRequired();
        builder.Property(x => x.Reason).HasMaxLength(500);
        builder.Property(x => x.IpAddress).HasMaxLength(64);

        // عمداً بدون FK به ApplicationUser (نه Domain nav نه Persistence Configuration) -
        // ثبت Audit هرگز نباید به خاطر محدودیت ارجاعی شکست بخورد، دقیقاً هم‌الگو با AuditLog.UserId
    }
}
