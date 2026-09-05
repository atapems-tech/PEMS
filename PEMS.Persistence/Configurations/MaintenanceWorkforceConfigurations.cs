using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PEMS.Domain.Entities;

namespace PEMS.Persistence.Configurations;

public class MaintenanceRoleConfiguration : IEntityTypeConfiguration<MaintenanceRole>
{
    public void Configure(EntityTypeBuilder<MaintenanceRole> builder)
    {
        builder.ToTable("MaintenanceRoles");
        builder.HasKey(x => x.MaintenanceRoleId);

        builder.Property(x => x.Name).HasMaxLength(150).IsRequired();
        builder.Property(x => x.Level).HasMaxLength(20).IsRequired();

        builder.HasIndex(x => new { x.DisciplineId, x.Name }).IsUnique();

        builder.HasOne(x => x.Discipline)
            .WithMany()
            .HasForeignKey(x => x.DisciplineId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

// طبق PEMS_CMMS_MAINTENANCE_ORGANIZATION_BLUEPRINT.md بخش ۲: MaintenanceGroup یک لایه‌ی
// گروه‌بندی تعمیراتی دقیق‌تر از Discipline است - Team زیر آن تعریف می‌شود، نه مستقیم زیر Discipline
public class MaintenanceGroupConfiguration : IEntityTypeConfiguration<MaintenanceGroup>
{
    public void Configure(EntityTypeBuilder<MaintenanceGroup> builder)
    {
        builder.ToTable("MaintenanceGroups");
        builder.HasKey(x => x.MaintenanceGroupId);

        builder.Property(x => x.Code).HasMaxLength(30).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(150).IsRequired();

        builder.HasIndex(x => x.Code).IsUnique();
        builder.HasIndex(x => x.Name).IsUnique();

        builder.HasOne(x => x.Discipline)
            .WithMany()
            .HasForeignKey(x => x.DisciplineId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

// طبق سند معماری Employee/Team/Labor: جایگزین MaintenanceCrewConfiguration
public class TeamConfiguration : IEntityTypeConfiguration<Team>
{
    public void Configure(EntityTypeBuilder<Team> builder)
    {
        builder.ToTable("Teams");
        builder.HasKey(x => x.TeamId);

        builder.Property(x => x.Code).HasMaxLength(30).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(150).IsRequired();

        builder.HasIndex(x => x.Code).IsUnique();
        builder.HasIndex(x => x.Name).IsUnique();

        builder.HasOne(x => x.MaintenanceGroup)
            .WithMany()
            .HasForeignKey(x => x.MaintenanceGroupId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.SupervisorEmployee)
            .WithMany()
            .HasForeignKey(x => x.SupervisorEmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Members)
            .WithOne(x => x.Team)
            .HasForeignKey(x => x.TeamId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

// طبق سند معماری: جایگزین MaintenanceCrewMemberConfiguration - طبق بند ۷ سند (جلوگیری از
// Duplicate Membership فعال)، Unique Index فقط شرطی روی رکوردهای فعال اعمال می‌شود تا
// Reactivate بعد از ترک تیم ممکن بماند.
public class TeamMemberConfiguration : IEntityTypeConfiguration<TeamMember>
{
    public void Configure(EntityTypeBuilder<TeamMember> builder)
    {
        builder.ToTable("TeamMembers");
        builder.HasKey(x => x.TeamMemberId);

        builder.HasIndex(x => new { x.TeamId, x.EmployeeId, x.IsActive })
            .IsUnique()
            .HasFilter("[IsActive] = 1");

        builder.HasOne(x => x.Employee)
            .WithMany()
            .HasForeignKey(x => x.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Role)
            .WithMany()
            .HasForeignKey(x => x.RoleId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

// طبق سند معماری Labor Cost: تاریخچه‌ی نرخ دستمزد
public class EmployeeLaborRateConfiguration : IEntityTypeConfiguration<EmployeeLaborRate>
{
    public void Configure(EntityTypeBuilder<EmployeeLaborRate> builder)
    {
        builder.ToTable("EmployeeLaborRates");
        builder.HasKey(x => x.EmployeeLaborRateId);

        builder.Property(x => x.RateType).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Currency).HasMaxLength(10).IsRequired();
        builder.Property(x => x.Rate).HasColumnType("decimal(18,2)");

        builder.HasOne(x => x.Employee)
            .WithMany()
            .HasForeignKey(x => x.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

// طبق سند معماری Labor Cost: ثبت واقعی ساعت‌کار با Snapshot نرخ
public class WorkOrderLaborConfiguration : IEntityTypeConfiguration<WorkOrderLabor>
{
    public void Configure(EntityTypeBuilder<WorkOrderLabor> builder)
    {
        builder.ToTable("WorkOrderLabors");
        builder.HasKey(x => x.WorkOrderLaborId);

        builder.Property(x => x.RegularHours).HasColumnType("decimal(10,2)");
        builder.Property(x => x.OvertimeHours).HasColumnType("decimal(10,2)");
        builder.Property(x => x.Rate).HasColumnType("decimal(18,2)");
        builder.Property(x => x.OvertimeRate).HasColumnType("decimal(18,2)");
        builder.Property(x => x.LaborCost).HasColumnType("decimal(18,2)");

        builder.HasOne(x => x.WorkOrder)
            .WithMany(x => x.LaborEntries)
            .HasForeignKey(x => x.WorkOrderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Employee)
            .WithMany()
            .HasForeignKey(x => x.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Team)
            .WithMany()
            .HasForeignKey(x => x.TeamId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class WorkOrderAssignmentConfiguration : IEntityTypeConfiguration<WorkOrder>
{
    public void Configure(EntityTypeBuilder<WorkOrder> builder)
    {
        builder.HasIndex(x => x.WorkOrderNumber).IsUnique();

        builder.HasOne(x => x.Equipment)
            .WithMany()
            .HasForeignKey(x => x.EquipmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.MaintenancePlan)
            .WithMany()
            .HasForeignKey(x => x.MaintenancePlanId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.AssignedTeam)
            .WithMany()
            .HasForeignKey(x => x.AssignedTeamId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.AssignedEmployee)
            .WithMany()
            .HasForeignKey(x => x.AssignedEmployeeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
