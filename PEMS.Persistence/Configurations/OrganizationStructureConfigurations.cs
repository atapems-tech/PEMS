using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PEMS.Domain.Entities;

namespace PEMS.Persistence.Configurations;

// طبق «PEMS Dynamic Maintenance Organization»: پیکربندی‌های تازه‌ی خودارجاعِ MaintenanceGroup/
// Team/Position به کلاس‌های IEntityTypeConfiguration موجودشان (MaintenanceWorkforceConfigurations.cs،
// DepartmentEmployeeConfigurations.cs) اضافه شدند - نه این‌جا، چون آن‌ها از قبل configuration
// اختصاصی داشتند (این فایل اولیه اشتباهاً فرض کرده بود که ندارند - اصلاح شد پیش از هر Migration).
// EmployeeAssignment یک Entity کاملاً تازه است؛ فقط همین یکی این‌جا پیکربندی می‌شود.
public class EmployeeAssignmentConfiguration : IEntityTypeConfiguration<EmployeeAssignment>
{
    public void Configure(EntityTypeBuilder<EmployeeAssignment> builder)
    {
        builder.ToTable("EmployeeAssignments");
        builder.HasKey(x => x.EmployeeAssignmentId);

        builder.Property(x => x.Notes).HasMaxLength(500);

        // طبق بخش ۴ سند: یک Position در هر لحظه حداکثر یک اشغال‌کننده‌ی فعال دارد (همان سمت،
        // نه همان کارمند) - تلاش برای دو تخصیص فعال هم‌زمان روی یک Position رد می‌شود.
        builder.HasIndex(x => x.PositionId)
            .IsUnique()
            .HasFilter("[IsActive] = 1")
            .HasDatabaseName("IX_EmployeeAssignments_PositionId_ActiveOnly");

        builder.HasOne(x => x.Position)
            .WithMany(x => x.Assignments)
            .HasForeignKey(x => x.PositionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Employee)
            .WithMany()
            .HasForeignKey(x => x.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
