using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PEMS.Domain.Entities;

namespace PEMS.Persistence.Configurations;

/// <summary>
/// طبق «PEMS Phase 2 — Equipment Classification + ISO 14224 Failure Taxonomy + PM/Job Plan
/// Intelligence»: پیکربندی EF Core تاکسونومی خرابی + PMTemplate + پیوندهای افزودنی.
/// </summary>
public class FailureModeConfiguration : IEntityTypeConfiguration<FailureMode>
{
    public void Configure(EntityTypeBuilder<FailureMode> builder)
    {
        builder.ToTable("FailureModes");
        builder.HasKey(x => x.FailureModeId);

        builder.Property(x => x.Code).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.NameFa).HasMaxLength(200);

        builder.HasIndex(x => x.Code).IsUnique();

        builder.HasOne(x => x.EquipmentClass)
            .WithMany()
            .HasForeignKey(x => x.EquipmentClassId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.FailureEffect)
            .WithMany()
            .HasForeignKey(x => x.FailureEffectId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.FailureConsequence)
            .WithMany()
            .HasForeignKey(x => x.FailureConsequenceId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class FailureCauseConfiguration : IEntityTypeConfiguration<FailureCause>
{
    public void Configure(EntityTypeBuilder<FailureCause> builder)
    {
        builder.ToTable("FailureCauses");
        builder.HasKey(x => x.FailureCauseId);

        builder.Property(x => x.Code).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.NameFa).HasMaxLength(200);

        builder.HasIndex(x => x.Code).IsUnique();
    }
}

public class FailureMechanismConfiguration : IEntityTypeConfiguration<FailureMechanism>
{
    public void Configure(EntityTypeBuilder<FailureMechanism> builder)
    {
        builder.ToTable("FailureMechanisms");
        builder.HasKey(x => x.FailureMechanismId);

        builder.Property(x => x.Code).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.NameFa).HasMaxLength(200);

        builder.HasIndex(x => x.Code).IsUnique();
    }
}

public class FailureEffectConfiguration : IEntityTypeConfiguration<FailureEffect>
{
    public void Configure(EntityTypeBuilder<FailureEffect> builder)
    {
        builder.ToTable("FailureEffects");
        builder.HasKey(x => x.FailureEffectId);

        builder.Property(x => x.Code).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.NameFa).HasMaxLength(200);

        builder.HasIndex(x => x.Code).IsUnique();
    }
}

public class FailureConsequenceConfiguration : IEntityTypeConfiguration<FailureConsequence>
{
    public void Configure(EntityTypeBuilder<FailureConsequence> builder)
    {
        builder.ToTable("FailureConsequences");
        builder.HasKey(x => x.FailureConsequenceId);

        builder.Property(x => x.Code).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.NameFa).HasMaxLength(200);
        builder.Property(x => x.Category).HasMaxLength(30).IsRequired();

        builder.HasIndex(x => x.Code).IsUnique();
    }
}

public class FailureModeCauseConfiguration : IEntityTypeConfiguration<FailureModeCause>
{
    public void Configure(EntityTypeBuilder<FailureModeCause> builder)
    {
        builder.ToTable("FailureModeCauses");
        builder.HasKey(x => new { x.FailureModeId, x.FailureCauseId });

        builder.HasOne(x => x.FailureMode)
            .WithMany(x => x.Causes)
            .HasForeignKey(x => x.FailureModeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.FailureCause)
            .WithMany(x => x.Modes)
            .HasForeignKey(x => x.FailureCauseId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class FailureCauseMechanismConfiguration : IEntityTypeConfiguration<FailureCauseMechanism>
{
    public void Configure(EntityTypeBuilder<FailureCauseMechanism> builder)
    {
        builder.ToTable("FailureCauseMechanisms");
        builder.HasKey(x => new { x.FailureCauseId, x.FailureMechanismId });

        builder.HasOne(x => x.FailureCause)
            .WithMany(x => x.Mechanisms)
            .HasForeignKey(x => x.FailureCauseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.FailureMechanism)
            .WithMany(x => x.Causes)
            .HasForeignKey(x => x.FailureMechanismId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

// طبق بند ۱۹: ارتباط افزودنی/اختیاری WorkOrder/FailureRecord → تاکسونومی مرجع جدید
public class WorkOrderFailureTaxonomyLinkConfiguration : IEntityTypeConfiguration<WorkOrder>
{
    public void Configure(EntityTypeBuilder<WorkOrder> builder)
    {
        builder.HasOne(x => x.FailureModeRef)
            .WithMany()
            .HasForeignKey(x => x.FailureModeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.FailureCauseRef)
            .WithMany()
            .HasForeignKey(x => x.FailureCauseId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class FailureRecordFailureTaxonomyLinkConfiguration : IEntityTypeConfiguration<FailureRecord>
{
    public void Configure(EntityTypeBuilder<FailureRecord> builder)
    {
        builder.HasOne(x => x.FailureModeRef)
            .WithMany()
            .HasForeignKey(x => x.FailureModeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.FailureCauseRef)
            .WithMany()
            .HasForeignKey(x => x.FailureCauseId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

// طبق بند ۶: سلسله‌مراتب اختیاری EquipmentClass (Self-Reference)
public class EquipmentClassHierarchyConfiguration : IEntityTypeConfiguration<EquipmentClass>
{
    public void Configure(EntityTypeBuilder<EquipmentClass> builder)
    {
        builder.HasOne(x => x.ParentEquipmentClass)
            .WithMany(x => x.ChildEquipmentClasses)
            .HasForeignKey(x => x.ParentEquipmentClassId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class PMTemplateConfiguration : IEntityTypeConfiguration<PMTemplate>
{
    public void Configure(EntityTypeBuilder<PMTemplate> builder)
    {
        builder.ToTable("PMTemplates");
        builder.HasKey(x => x.PMTemplateId);

        builder.Property(x => x.Code).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.NameFa).HasMaxLength(200);
        builder.Property(x => x.TriggerType).HasMaxLength(20).IsRequired();
        builder.Property(x => x.MeterType).HasMaxLength(30);
        builder.Property(x => x.ApplicableCriticalityClasses).HasMaxLength(30);

        builder.HasIndex(x => x.Code).IsUnique();

        builder.HasOne(x => x.EquipmentClass)
            .WithMany()
            .HasForeignKey(x => x.EquipmentClassId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.MaintenanceStrategyType)
            .WithMany()
            .HasForeignKey(x => x.MaintenanceStrategyTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.MaintenanceType)
            .WithMany()
            .HasForeignKey(x => x.MaintenanceTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.JobPlan)
            .WithMany()
            .HasForeignKey(x => x.JobPlanId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.StandardReference)
            .WithMany()
            .HasForeignKey(x => x.StandardReferenceId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

// طبق بند ۲۳: پیوندهای افزودنی/اختیاری JobPlan → Master Data جدید
public class JobPlanMasterDataLinkConfiguration : IEntityTypeConfiguration<JobPlan>
{
    public void Configure(EntityTypeBuilder<JobPlan> builder)
    {
        builder.HasOne(x => x.EquipmentClass)
            .WithMany()
            .HasForeignKey(x => x.EquipmentClassId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.MaintenanceActivity)
            .WithMany()
            .HasForeignKey(x => x.MaintenanceActivityId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.StandardReference)
            .WithMany()
            .HasForeignKey(x => x.StandardReferenceId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
