using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PEMS.Domain.Entities;

namespace PEMS.Persistence.Configurations;

public class DepartmentConfiguration : IEntityTypeConfiguration<Department>
{
    public void Configure(EntityTypeBuilder<Department> builder)
    {
        builder.ToTable("Departments");
        builder.HasKey(x => x.DepartmentId);

        builder.Property(x => x.Code).HasMaxLength(30);
        builder.Property(x => x.Name).HasMaxLength(150).IsRequired();

        // Master data: Department should not be duplicated.
        builder.HasIndex(x => x.Name).IsUnique();

        // طبق سند RBAC-ABAC Authorization Architecture بخش ۲: سلسله‌مراتب خودارجاع - Restrict
        // تا حذف یک دپارتمان والد، زیرمجموعه‌هایش را یتیم/آبشاری حذف نکند
        builder.HasOne(x => x.ParentDepartment)
            .WithMany()
            .HasForeignKey(x => x.ParentDepartmentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> builder)
    {
        builder.ToTable("Employees");
        builder.HasKey(x => x.EmployeeId);

        // PersonnelCode is optional because some employees/contractors may not
        // have a personnel code at onboarding time. Non-empty codes remain unique.
        builder.Property(x => x.PersonnelCode)
            .HasMaxLength(50)
            .IsRequired(false);

        builder.Property(x => x.FirstName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.LastName).HasMaxLength(100).IsRequired();

        // طبق «Employee Details — Stitch UI Redesign»: فیلدهای افزایشی پروفایل
        builder.Property(x => x.Location).HasMaxLength(200);
        builder.Property(x => x.Shift).HasMaxLength(100);
        builder.Property(x => x.HomeAddress).HasMaxLength(500);

        builder.HasIndex(x => x.PersonnelCode)
            .IsUnique()
            .HasFilter("[PersonnelCode] IS NOT NULL AND [PersonnelCode] <> ''");

        // Department and Discipline are master data. Their removal must never
        // cascade into historical Employee records.
        builder.HasOne(x => x.Department)
            .WithMany()
            .HasForeignKey(x => x.DepartmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Discipline)
            .WithMany()
            .HasForeignKey(x => x.DisciplineId)
            .OnDelete(DeleteBehavior.Restrict);

        // طبق سند RBAC-ABAC Authorization Architecture بخش ۳: لینک اختیاری به رکورد واقعی Position
        builder.HasOne(x => x.PositionRef)
            .WithMany()
            .HasForeignKey(x => x.PositionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class PositionConfiguration : IEntityTypeConfiguration<Position>
{
    public void Configure(EntityTypeBuilder<Position> builder)
    {
        builder.ToTable("Positions");
        builder.HasKey(x => x.PositionId);

        builder.Property(x => x.Title).HasMaxLength(150).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(500);

        builder.HasOne(x => x.Department)
            .WithMany()
            .HasForeignKey(x => x.DepartmentId)
            .OnDelete(DeleteBehavior.Restrict);

        // طبق «PEMS Dynamic Maintenance Organization» بخش ۱۴-۱۶: یک سمت معمولاً جای مشخصی در
        // سلسله‌مراتب تعمیرونگهداری دارد (مثلاً «سرگروه»/«سرشیفت» زیر یک Team). Null یعنی سمت
        // عمومی/سطح دپارتمان (رفتار امروز، دست‌نخورده) - این فقط یک ستون اضافه است، جایگزین
        // DepartmentId نیست.
        builder.HasOne(x => x.Team)
            .WithMany(x => x.Positions)
            .HasForeignKey(x => x.TeamId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
