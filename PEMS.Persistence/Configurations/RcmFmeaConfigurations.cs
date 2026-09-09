using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PEMS.Domain.Entities;

namespace PEMS.Persistence.Configurations;

/// <summary>
/// طبق «PEMS Phase 4 — RCM/FMEA/FMECA Decision Intelligence» بند ۴۱-۴۲: همه‌ی FK های اشاره‌کننده
/// به تاکسونومی/مرجع موجود (FailureMode/Cause/Mechanism/Effect/Consequence، MaintenanceStrategyType،
/// StandardReference، PMTemplate، EquipmentClass) عمداً Restrict هستند - حذف یک ردیف مرجع هرگز
/// نباید به‌صورت آبشاری یک تحلیل تاییدشده را نابود کند.
/// </summary>
public class FunctionalRequirementConfiguration : IEntityTypeConfiguration<FunctionalRequirement>
{
    public void Configure(EntityTypeBuilder<FunctionalRequirement> builder)
    {
        builder.ToTable("FunctionalRequirements");
        builder.HasKey(x => x.FunctionalRequirementId);

        builder.Property(x => x.Code).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Unit).HasMaxLength(30);
        builder.Property(x => x.MinimumPerformance).HasPrecision(18, 4);
        builder.Property(x => x.MaximumPerformance).HasPrecision(18, 4);

        builder.HasIndex(x => new { x.EquipmentClassId, x.Code }).IsUnique();

        builder.HasOne(x => x.EquipmentClass)
            .WithMany()
            .HasForeignKey(x => x.EquipmentClassId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class FunctionalFailureConfiguration : IEntityTypeConfiguration<FunctionalFailure>
{
    public void Configure(EntityTypeBuilder<FunctionalFailure> builder)
    {
        builder.ToTable("FunctionalFailures");
        builder.HasKey(x => x.FunctionalFailureId);

        builder.Property(x => x.Code).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Title).HasMaxLength(200).IsRequired();

        builder.HasIndex(x => new { x.FunctionalRequirementId, x.Code }).IsUnique();

        builder.HasOne(x => x.FunctionalRequirement)
            .WithMany(x => x.FunctionalFailures)
            .HasForeignKey(x => x.FunctionalRequirementId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class FMEAAnalysisConfiguration : IEntityTypeConfiguration<FMEAAnalysis>
{
    public void Configure(EntityTypeBuilder<FMEAAnalysis> builder)
    {
        builder.ToTable("FMEAAnalyses");
        builder.HasKey(x => x.FMEAAnalysisId);

        builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(20).IsRequired();
        builder.Property(x => x.PreparedBy).HasMaxLength(200);
        builder.Property(x => x.ReviewedBy).HasMaxLength(200);
        builder.Property(x => x.ApprovedBy).HasMaxLength(200);

        builder.HasIndex(x => new { x.EquipmentClassId, x.Status });

        builder.HasOne(x => x.EquipmentClass)
            .WithMany()
            .HasForeignKey(x => x.EquipmentClassId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.SupersededByAnalysis)
            .WithMany()
            .HasForeignKey(x => x.SupersededByAnalysisId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class FMEAItemConfiguration : IEntityTypeConfiguration<FMEAItem>
{
    public void Configure(EntityTypeBuilder<FMEAItem> builder)
    {
        builder.ToTable("FMEAItems");
        builder.HasKey(x => x.FMEAItemId);

        builder.Ignore(x => x.RiskPriorityNumber);

        builder.HasOne(x => x.FMEAAnalysis)
            .WithMany(x => x.Items)
            .HasForeignKey(x => x.FMEAAnalysisId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.FunctionalFailure)
            .WithMany()
            .HasForeignKey(x => x.FunctionalFailureId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.FailureMode)
            .WithMany()
            .HasForeignKey(x => x.FailureModeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.FailureCause)
            .WithMany()
            .HasForeignKey(x => x.FailureCauseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.FailureMechanism)
            .WithMany()
            .HasForeignKey(x => x.FailureMechanismId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.FailureEffect)
            .WithMany()
            .HasForeignKey(x => x.FailureEffectId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.FailureConsequence)
            .WithMany()
            .HasForeignKey(x => x.FailureConsequenceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.MaintenanceStrategyType)
            .WithMany()
            .HasForeignKey(x => x.MaintenanceStrategyTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.StandardReference)
            .WithMany()
            .HasForeignKey(x => x.StandardReferenceId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class RCMAnalysisConfiguration : IEntityTypeConfiguration<RCMAnalysis>
{
    public void Configure(EntityTypeBuilder<RCMAnalysis> builder)
    {
        builder.ToTable("RCMAnalyses");
        builder.HasKey(x => x.RCMAnalysisId);

        builder.Property(x => x.Status).HasMaxLength(20).IsRequired();
        builder.Property(x => x.PreparedBy).HasMaxLength(200);
        builder.Property(x => x.ReviewedBy).HasMaxLength(200);
        builder.Property(x => x.ApprovedBy).HasMaxLength(200);

        builder.HasIndex(x => new { x.EquipmentClassId, x.Status });

        builder.HasOne(x => x.EquipmentClass)
            .WithMany()
            .HasForeignKey(x => x.EquipmentClassId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.FMEAAnalysis)
            .WithMany()
            .HasForeignKey(x => x.FMEAAnalysisId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class RCMDecisionConfiguration : IEntityTypeConfiguration<RCMDecision>
{
    public void Configure(EntityTypeBuilder<RCMDecision> builder)
    {
        builder.ToTable("RCMDecisions");
        builder.HasKey(x => x.RCMDecisionId);

        builder.Property(x => x.DecisionType).HasMaxLength(30).IsRequired();
        builder.Property(x => x.ConsequenceCategory).HasMaxLength(30).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(20).IsRequired();
        builder.Property(x => x.ApprovedBy).HasMaxLength(200);

        builder.HasOne(x => x.RCMAnalysis)
            .WithMany(x => x.Decisions)
            .HasForeignKey(x => x.RCMAnalysisId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.FMEAItem)
            .WithMany()
            .HasForeignKey(x => x.FMEAItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.MaintenanceStrategyType)
            .WithMany()
            .HasForeignKey(x => x.MaintenanceStrategyTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.RecommendedPMTemplate)
            .WithMany()
            .HasForeignKey(x => x.RecommendedPMTemplateId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
