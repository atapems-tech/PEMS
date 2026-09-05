using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PEMS.Domain.Entities;

namespace PEMS.Persistence.Configurations;

public class MOCConfiguration : IEntityTypeConfiguration<MOC>
{
    public void Configure(EntityTypeBuilder<MOC> builder)
    {
        builder.ToTable("MOCs");
        builder.HasKey(x => x.MOCId);

        builder.Property(x => x.MOCNumber).HasMaxLength(100).IsRequired();
        builder.Property(x => x.ChangeType).HasMaxLength(20).IsRequired();
        builder.Property(x => x.RiskLevel).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(30).IsRequired();

        builder.HasIndex(x => new { x.ProjectId, x.MOCNumber }).IsUnique();

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
