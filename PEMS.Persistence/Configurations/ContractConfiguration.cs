using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PEMS.Domain.Entities;

namespace PEMS.Persistence.Configurations;

public class ContractConfiguration : IEntityTypeConfiguration<Contract>
{
    public void Configure(EntityTypeBuilder<Contract> builder)
    {
        builder.HasOne(x => x.Project)
            .WithMany()
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Vendor)
            .WithMany(x => x.Contracts)
            .HasForeignKey(x => x.VendorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ApprovedBy)
            .WithMany()
            .HasForeignKey(x => x.ApprovedById)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.SignedBy)
            .WithMany()
            .HasForeignKey(x => x.SignedById)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(x => x.ContractValue)
            .HasPrecision(18, 2);

        builder.Property(x => x.CurrentValue)
            .HasPrecision(18, 2);
    }
}

public class ContractRevisionConfiguration : IEntityTypeConfiguration<ContractRevision>
{
    public void Configure(EntityTypeBuilder<ContractRevision> builder)
    {
        builder.HasOne(x => x.Contract)
            .WithMany(x => x.Revisions)
            .HasForeignKey(x => x.ContractId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.RequestedBy)
            .WithMany()
            .HasForeignKey(x => x.RequestedById)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ApprovedBy)
            .WithMany()
            .HasForeignKey(x => x.ApprovedById)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(x => x.PreviousValue).HasPrecision(18, 2);
        builder.Property(x => x.NewValue).HasPrecision(18, 2);

        builder.HasIndex(x => new { x.ContractId, x.RevisionNo }).IsUnique();
    }
}

public class ContractDisciplineReviewConfiguration : IEntityTypeConfiguration<ContractDisciplineReview>
{
    public void Configure(EntityTypeBuilder<ContractDisciplineReview> builder)
    {
        builder.HasOne(x => x.Contract)
            .WithMany(x => x.DisciplineReviews)
            .HasForeignKey(x => x.ContractId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Reviewer)
            .WithMany()
            .HasForeignKey(x => x.ReviewerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.ContractId, x.DisciplineRole }).IsUnique();
    }
}

public class ContractApprovalAuthorityRuleConfiguration : IEntityTypeConfiguration<ContractApprovalAuthorityRule>
{
    public void Configure(EntityTypeBuilder<ContractApprovalAuthorityRule> builder)
    {
        builder.Property(x => x.MinValue).HasPrecision(18, 2);
        builder.Property(x => x.MaxValue).HasPrecision(18, 2);
    }
}

public class VendorQualificationHistoryConfiguration : IEntityTypeConfiguration<VendorQualificationHistory>
{
    public void Configure(EntityTypeBuilder<VendorQualificationHistory> builder)
    {
        builder.HasOne(x => x.Vendor)
            .WithMany(x => x.QualificationHistory)
            .HasForeignKey(x => x.VendorId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.ChangedBy)
            .WithMany()
            .HasForeignKey(x => x.ChangedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
