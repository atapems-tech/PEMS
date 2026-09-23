using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PEMS.Domain.Entities;

namespace PEMS.Persistence.Configurations;

public class QCDisciplineConfiguration : IEntityTypeConfiguration<QCDiscipline>
{
    public void Configure(EntityTypeBuilder<QCDiscipline> builder)
    {
        builder.ToTable("QCDisciplines");
        builder.HasKey(x => x.QCDisciplineId);

        builder.Property(x => x.Code).HasMaxLength(30).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(150).IsRequired();

        builder.HasIndex(x => x.Code).IsUnique();
    }
}

public class InspectionTestPlanConfiguration : IEntityTypeConfiguration<InspectionTestPlan>
{
    public void Configure(EntityTypeBuilder<InspectionTestPlan> builder)
    {
        builder.ToTable("InspectionTestPlans");
        builder.HasKey(x => x.InspectionTestPlanId);

        builder.Property(x => x.ITPNumber).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Title).HasMaxLength(300).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(30).IsRequired();

        builder.HasIndex(x => new { x.ProjectId, x.ITPNumber, x.Revision }).IsUnique();

        builder.HasOne(x => x.QCDiscipline)
            .WithMany()
            .HasForeignKey(x => x.QCDisciplineId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Equipment)
            .WithMany()
            .HasForeignKey(x => x.EquipmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ProcurementPackage)
            .WithMany()
            .HasForeignKey(x => x.ProcurementPackageId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Contract)
            .WithMany()
            .HasForeignKey(x => x.ContractId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.LineItems)
            .WithOne(x => x.InspectionTestPlan)
            .HasForeignKey(x => x.InspectionTestPlanId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class ITPLineItemConfiguration : IEntityTypeConfiguration<ITPLineItem>
{
    public void Configure(EntityTypeBuilder<ITPLineItem> builder)
    {
        builder.ToTable("ITPLineItems");
        builder.HasKey(x => x.ITPLineItemId);

        builder.Property(x => x.Activity).HasMaxLength(300).IsRequired();
        builder.Property(x => x.InspectionCategory).HasMaxLength(5).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(30).IsRequired();

        builder.HasOne(x => x.QCDiscipline)
            .WithMany()
            .HasForeignKey(x => x.QCDisciplineId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.InspectionTestPlanId, x.SequenceNo });
    }
}

public class NonConformanceReportConfiguration : IEntityTypeConfiguration<NonConformanceReport>
{
    public void Configure(EntityTypeBuilder<NonConformanceReport> builder)
    {
        builder.ToTable("NonConformanceReports");
        builder.HasKey(x => x.NonConformanceReportId);

        builder.Property(x => x.NCRNumber).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Severity).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(30).IsRequired();

        builder.HasIndex(x => new { x.ProjectId, x.NCRNumber }).IsUnique();

        builder.HasOne(x => x.QCDiscipline)
            .WithMany()
            .HasForeignKey(x => x.QCDisciplineId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Equipment)
            .WithMany()
            .HasForeignKey(x => x.EquipmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ITPLineItem)
            .WithMany()
            .HasForeignKey(x => x.ITPLineItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.InspectionRecord)
            .WithMany()
            .HasForeignKey(x => x.InspectionRecordId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
