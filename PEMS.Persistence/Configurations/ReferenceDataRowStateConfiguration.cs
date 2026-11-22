using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PEMS.Domain.Entities;

namespace PEMS.Persistence.Configurations;

/// <summary>SCRUM-803: one state row per list and row key.</summary>
public class ReferenceDataRowStateConfiguration : IEntityTypeConfiguration<ReferenceDataRowState>
{
    public void Configure(EntityTypeBuilder<ReferenceDataRowState> builder)
    {
        builder.ToTable("ReferenceDataRowStates");
        builder.HasIndex(x => new { x.ListKey, x.RowKey }).IsUnique();
        builder.Property(x => x.ListKey).HasMaxLength(80).IsRequired();
        builder.Property(x => x.RowKey).HasMaxLength(80).IsRequired();
        builder.Property(x => x.Owner).HasMaxLength(20).IsRequired();
        builder.Property(x => x.ShippedHash).HasMaxLength(32).IsRequired();
    }
}
