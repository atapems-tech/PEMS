using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PEMS.Domain.Entities;

namespace PEMS.Persistence.Configurations;

public class MrsImportBatchConfiguration : IEntityTypeConfiguration<MrsImportBatch>
{
    public void Configure(EntityTypeBuilder<MrsImportBatch> builder)
    {
        builder.ToTable("MrsImportBatches");
        builder.HasKey(x => x.MrsImportBatchId);

        builder.Property(x => x.SourceFileName).HasMaxLength(255).IsRequired();
        builder.Property(x => x.SourceSheet).HasMaxLength(255);
        builder.Property(x => x.Status).HasMaxLength(30).IsRequired();

        builder.HasOne(x => x.ImportedBy).WithMany().HasForeignKey(x => x.ImportedById).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.SourceAttachment).WithMany().HasForeignKey(x => x.SourceAttachmentId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class MrsImportRowMetadataConfiguration : IEntityTypeConfiguration<MrsImportRowMetadata>
{
    public void Configure(EntityTypeBuilder<MrsImportRowMetadata> builder)
    {
        builder.ToTable("MrsImportRowMetadata");
        builder.HasKey(x => x.MrsImportRowMetadataId);

        builder.Property(x => x.OriginalHeader).HasMaxLength(255).IsRequired();
        builder.Property(x => x.CanonicalField).HasMaxLength(100);
        builder.Property(x => x.Value).HasMaxLength(1000);

        builder.HasOne(x => x.Row).WithMany(x => x.Metadata).HasForeignKey(x => x.MrsImportRowId).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.MrsImportRowId, x.CanonicalField });
    }
}

public class MrsImportRowConfiguration : IEntityTypeConfiguration<MrsImportRow>
{
    public void Configure(EntityTypeBuilder<MrsImportRow> builder)
    {
        builder.ToTable("MrsImportRows");
        builder.HasKey(x => x.MrsImportRowId);

        builder.Property(x => x.RawQtyReceived).HasColumnType("decimal(18,3)");
        builder.Property(x => x.RawQtyIssued).HasColumnType("decimal(18,3)");
        builder.Property(x => x.RawInventory).HasColumnType("decimal(18,3)");
        builder.Property(x => x.MatchStatus).HasMaxLength(30).IsRequired();
        builder.Property(x => x.ReviewStatus).HasMaxLength(30).IsRequired();

        builder.HasOne(x => x.Batch).WithMany(x => x.Rows).HasForeignKey(x => x.MrsImportBatchId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.ResolvedWarehouse).WithMany().HasForeignKey(x => x.ResolvedWarehouseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.MatchedMaterialItem).WithMany().HasForeignKey(x => x.MatchedMaterialItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ReviewedBy).WithMany().HasForeignKey(x => x.ReviewedById).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ResultMaterialItem).WithMany().HasForeignKey(x => x.ResultMaterialItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ResultInventoryTransaction).WithMany().HasForeignKey(x => x.ResultInventoryTransactionId).OnDelete(DeleteBehavior.Restrict);
    }
}
