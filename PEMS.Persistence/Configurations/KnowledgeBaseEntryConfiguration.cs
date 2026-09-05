using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PEMS.Domain.Entities;

namespace PEMS.Persistence.Configurations;

public class KnowledgeBaseEntryConfiguration : IEntityTypeConfiguration<KnowledgeBaseEntry>
{
    public void Configure(EntityTypeBuilder<KnowledgeBaseEntry> builder)
    {
        builder.HasIndex(x => new { x.SourceType, x.SourceId });
    }
}
