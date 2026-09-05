using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PEMS.Domain.Entities;

namespace PEMS.Persistence.Configurations;

public class TableExtractionBatchConfiguration : IEntityTypeConfiguration<TableExtractionBatch>
{
    public void Configure(EntityTypeBuilder<TableExtractionBatch> builder)
    {
        builder.HasOne(x => x.SourceAttachment)
            .WithMany()
            .HasForeignKey(x => x.SourceAttachmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.CreatedBy)
            .WithMany()
            .HasForeignKey(x => x.CreatedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class TableExtractionRowConfiguration : IEntityTypeConfiguration<TableExtractionRow>
{
    public void Configure(EntityTypeBuilder<TableExtractionRow> builder)
    {
        builder.HasOne(x => x.Batch)
            .WithMany(b => b.Rows)
            .HasForeignKey(x => x.TableExtractionBatchId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.MatchedEquipment)
            .WithMany()
            .HasForeignKey(x => x.MatchedEquipmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ResultInstrument)
            .WithMany()
            .HasForeignKey(x => x.ResultInstrumentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ReviewedBy)
            .WithMany()
            .HasForeignKey(x => x.ReviewedById)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.TagNumber);
    }
}
