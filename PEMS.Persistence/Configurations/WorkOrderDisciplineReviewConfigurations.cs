using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PEMS.Domain.Entities;

namespace PEMS.Persistence.Configurations;

public class WorkOrderDisciplineReferralConfiguration : IEntityTypeConfiguration<WorkOrderDisciplineReferral>
{
    public void Configure(EntityTypeBuilder<WorkOrderDisciplineReferral> builder)
    {
        builder.ToTable("WorkOrderDisciplineReferrals");
        builder.HasKey(x => x.WorkOrderDisciplineReferralId);

        builder.HasIndex(x => new { x.WorkOrderId, x.DisciplineId, x.CycleNumber }).IsUnique();

        builder.HasOne(x => x.WorkOrder)
            .WithMany()
            .HasForeignKey(x => x.WorkOrderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Discipline)
            .WithMany()
            .HasForeignKey(x => x.DisciplineId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class WorkOrderDisciplineFindingConfiguration : IEntityTypeConfiguration<WorkOrderDisciplineFinding>
{
    public void Configure(EntityTypeBuilder<WorkOrderDisciplineFinding> builder)
    {
        builder.ToTable("WorkOrderDisciplineFindings");
        builder.HasKey(x => x.WorkOrderDisciplineFindingId);

        builder.Property(x => x.Decision).HasMaxLength(20).IsRequired();

        builder.HasOne(x => x.WorkOrder)
            .WithMany()
            .HasForeignKey(x => x.WorkOrderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Discipline)
            .WithMany()
            .HasForeignKey(x => x.DisciplineId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
