using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PEMS.Domain.Entities;

namespace PEMS.Persistence.Configurations;

public class NumberingSequenceConfiguration : IEntityTypeConfiguration<NumberingSequence>
{
    public void Configure(EntityTypeBuilder<NumberingSequence> builder)
    {
        builder.ToTable("NumberingSequences");
        builder.HasKey(x => x.NumberingSequenceId);

        builder.Property(x => x.DocumentType).HasMaxLength(50).IsRequired();
        builder.Property(x => x.ScopeKey).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Prefix).HasMaxLength(20).IsRequired();

        builder.HasIndex(x => new { x.DocumentType, x.ScopeKey }).IsUnique();
    }
}
