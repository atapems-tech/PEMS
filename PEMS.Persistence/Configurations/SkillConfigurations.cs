using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PEMS.Domain.Entities;

namespace PEMS.Persistence.Configurations;

public class SkillConfiguration : IEntityTypeConfiguration<Skill>
{
    public void Configure(EntityTypeBuilder<Skill> builder)
    {
        builder.ToTable("Skills");
        builder.HasKey(x => x.SkillId);

        builder.Property(x => x.Name).HasMaxLength(150).IsRequired();
        builder.HasIndex(x => x.Name).IsUnique();

        builder.HasOne(x => x.Discipline)
            .WithMany()
            .HasForeignKey(x => x.DisciplineId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

// طبق بخش ۱۷ سند: جلوگیری از تخصیص تکراری فعال، با اجازه‌ی Reactivate بعد از غیرفعال شدن
public class EmployeeSkillConfiguration : IEntityTypeConfiguration<EmployeeSkill>
{
    public void Configure(EntityTypeBuilder<EmployeeSkill> builder)
    {
        builder.ToTable("EmployeeSkills");
        builder.HasKey(x => x.EmployeeSkillId);

        builder.HasIndex(x => new { x.EmployeeId, x.SkillId, x.IsActive })
            .IsUnique()
            .HasFilter("[IsActive] = 1");

        builder.HasOne(x => x.Employee)
            .WithMany()
            .HasForeignKey(x => x.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Skill)
            .WithMany()
            .HasForeignKey(x => x.SkillId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class RequiredSkillConfiguration : IEntityTypeConfiguration<RequiredSkill>
{
    public void Configure(EntityTypeBuilder<RequiredSkill> builder)
    {
        builder.ToTable("RequiredSkills");
        builder.HasKey(x => x.RequiredSkillId);

        builder.HasIndex(x => new { x.WorkOrderId, x.SkillId }).IsUnique();

        builder.HasOne(x => x.WorkOrder)
            .WithMany()
            .HasForeignKey(x => x.WorkOrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Skill)
            .WithMany()
            .HasForeignKey(x => x.SkillId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
