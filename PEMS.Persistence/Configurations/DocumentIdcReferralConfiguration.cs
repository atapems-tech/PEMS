using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PEMS.Domain.Entities;

namespace PEMS.Persistence.Configurations;

public class DocumentIdcReferralConfiguration : IEntityTypeConfiguration<DocumentIdcReferral>
{
    public void Configure(EntityTypeBuilder<DocumentIdcReferral> builder)
    {
        builder.ToTable("DocumentIdcReferrals");
        builder.HasKey(x => x.DocumentIdcReferralId);

        builder.HasIndex(x => new { x.DocumentId, x.DisciplineId }).IsUnique();

        builder.HasOne(x => x.Discipline)
            .WithMany()
            .HasForeignKey(x => x.DisciplineId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
