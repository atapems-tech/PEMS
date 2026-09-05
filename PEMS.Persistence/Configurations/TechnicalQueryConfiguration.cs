using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PEMS.Domain.Entities;

namespace PEMS.Persistence.Configurations;

public class TechnicalQueryConfiguration : IEntityTypeConfiguration<TechnicalQuery>
{
    public void Configure(EntityTypeBuilder<TechnicalQuery> builder)
    {
        builder.ToTable("TechnicalQueries");
        builder.HasKey(x => x.TechnicalQueryId);

        builder.Property(x => x.QueryNumber).HasMaxLength(100).IsRequired();
        builder.Property(x => x.QueryType).HasMaxLength(10).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(30).IsRequired();
        builder.Property(x => x.Priority).HasMaxLength(20).IsRequired();

        builder.HasIndex(x => new { x.ProjectId, x.QueryNumber }).IsUnique();

        builder.HasOne(x => x.Discipline)
            .WithMany()
            .HasForeignKey(x => x.DisciplineId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.RelatedDocument)
            .WithMany()
            .HasForeignKey(x => x.RelatedDocumentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.RelatedEquipment)
            .WithMany()
            .HasForeignKey(x => x.RelatedEquipmentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
