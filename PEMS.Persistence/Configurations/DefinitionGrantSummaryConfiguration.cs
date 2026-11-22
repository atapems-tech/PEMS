using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PEMS.Domain.Entities;

namespace PEMS.Persistence.Configurations;

/// <summary>SCRUM-806: the view is created by migration Scrum806DefinitionGrantSummaryView; EF only reads it.</summary>
public class DefinitionGrantSummaryConfiguration : IEntityTypeConfiguration<DefinitionGrantSummary>
{
    public const string ViewName = "vw_DefinitionGrantSummary";

    public void Configure(EntityTypeBuilder<DefinitionGrantSummary> builder)
    {
        builder.HasKey(x => x.GrantKey);
        builder.ToView(ViewName);
        builder.Property(x => x.GrantKey).HasMaxLength(161);
        builder.Property(x => x.Kind).HasMaxLength(40);
        builder.Property(x => x.DefinitionKey).HasMaxLength(120);
    }
}
