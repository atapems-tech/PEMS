using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PEMS.Domain.Entities;

namespace PEMS.Persistence.Configurations;

public class LetterStarConfiguration : IEntityTypeConfiguration<LetterStar>
{
    public void Configure(EntityTypeBuilder<LetterStar> builder)
    {
        builder.HasIndex(x => new { x.LetterId, x.ApplicationUserId }).IsUnique();

        builder.HasOne(x => x.Letter)
            .WithMany()
            .HasForeignKey(x => x.LetterId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
