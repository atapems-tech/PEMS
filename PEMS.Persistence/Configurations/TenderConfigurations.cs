using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PEMS.Domain.Entities;

namespace PEMS.Persistence.Configurations;

public class TenderConfiguration : IEntityTypeConfiguration<Tender>
{
    public void Configure(EntityTypeBuilder<Tender> builder)
    {
        builder.HasOne(x => x.ProcurementPackage)
            .WithMany()
            .HasForeignKey(x => x.ProcurementPackageId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.AwardedBid)
            .WithMany()
            .HasForeignKey(x => x.AwardedBidId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.AwardedBy)
            .WithMany()
            .HasForeignKey(x => x.AwardedById)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Contract)
            .WithMany()
            .HasForeignKey(x => x.ContractId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.TenderNumber).IsUnique();
    }
}

public class TenderInvitedVendorConfiguration : IEntityTypeConfiguration<TenderInvitedVendor>
{
    public void Configure(EntityTypeBuilder<TenderInvitedVendor> builder)
    {
        builder.HasOne(x => x.Tender)
            .WithMany(x => x.InvitedVendors)
            .HasForeignKey(x => x.TenderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Vendor)
            .WithMany()
            .HasForeignKey(x => x.VendorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.TenderId, x.VendorId }).IsUnique();
    }
}

public class BidConfiguration : IEntityTypeConfiguration<Bid>
{
    public void Configure(EntityTypeBuilder<Bid> builder)
    {
        builder.HasOne(x => x.Tender)
            .WithMany(x => x.Bids)
            .HasForeignKey(x => x.TenderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Vendor)
            .WithMany()
            .HasForeignKey(x => x.VendorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(x => x.OriginalValue).HasPrecision(18, 2);
        builder.Property(x => x.NegotiatedValue).HasPrecision(18, 2);
        builder.Property(x => x.FinalValue).HasPrecision(18, 2);

        builder.HasIndex(x => new { x.TenderId, x.VendorId }).IsUnique();
        builder.HasIndex(x => x.BidNumber).IsUnique();
    }
}

public class BidEvaluationConfiguration : IEntityTypeConfiguration<BidEvaluation>
{
    public void Configure(EntityTypeBuilder<BidEvaluation> builder)
    {
        builder.HasOne(x => x.Bid)
            .WithMany(x => x.Evaluations)
            .HasForeignKey(x => x.BidId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Evaluator)
            .WithMany()
            .HasForeignKey(x => x.EvaluatorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class BidNegotiationConfiguration : IEntityTypeConfiguration<BidNegotiation>
{
    public void Configure(EntityTypeBuilder<BidNegotiation> builder)
    {
        builder.HasOne(x => x.Bid)
            .WithMany(x => x.Negotiations)
            .HasForeignKey(x => x.BidId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.ApprovedBy)
            .WithMany()
            .HasForeignKey(x => x.ApprovedById)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(x => x.PreviousValue).HasPrecision(18, 2);
        builder.Property(x => x.NegotiatedValue).HasPrecision(18, 2);

        builder.HasIndex(x => new { x.BidId, x.RoundNo }).IsUnique();
    }
}

public class ContractNTPConfiguration : IEntityTypeConfiguration<ContractNTP>
{
    public void Configure(EntityTypeBuilder<ContractNTP> builder)
    {
        builder.HasOne(x => x.Contract)
            .WithMany()
            .HasForeignKey(x => x.ContractId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.IssuedBy)
            .WithMany()
            .HasForeignKey(x => x.IssuedById)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Attachment)
            .WithMany()
            .HasForeignKey(x => x.AttachmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.NTPNumber).IsUnique();
    }
}
