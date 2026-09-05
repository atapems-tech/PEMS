using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PEMS.Domain.Entities;

namespace PEMS.Persistence.Configurations;

public class LetterRecipientConfiguration : IEntityTypeConfiguration<LetterRecipient>
{
    public void Configure(EntityTypeBuilder<LetterRecipient> builder)
    {
        builder.HasOne(x => x.Letter)
            .WithMany(x => x.Recipients)
            .HasForeignKey(x => x.LetterId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Employee)
            .WithMany()
            .HasForeignKey(x => x.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Department)
            .WithMany()
            .HasForeignKey(x => x.DepartmentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
