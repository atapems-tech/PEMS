using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PEMS.Domain.Entities;

namespace PEMS.Persistence.Configurations;

public class LetterTagConfiguration : IEntityTypeConfiguration<LetterTag>
{
    public void Configure(EntityTypeBuilder<LetterTag> builder)
    {
        builder.HasIndex(x => new { x.LetterId, x.TagId }).IsUnique();

        builder.HasOne(x => x.Letter)
            .WithMany(x => x.Tags)
            .HasForeignKey(x => x.LetterId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Tag)
            .WithMany()
            .HasForeignKey(x => x.TagId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class TaskItemConfiguration : IEntityTypeConfiguration<TaskItem>
{
    public void Configure(EntityTypeBuilder<TaskItem> builder)
    {
        builder.HasOne(x => x.AssignedToEmployee)
            .WithMany()
            .HasForeignKey(x => x.AssignedToEmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.RelatedType, x.RelatedId });
    }
}
