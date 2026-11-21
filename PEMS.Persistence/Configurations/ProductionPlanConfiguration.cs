using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PEMS.Domain.Entities;

namespace PEMS.Persistence.Configurations;

public class ProductionPlanConfiguration : IEntityTypeConfiguration<ProductionPlan>
{
    public void Configure(EntityTypeBuilder<ProductionPlan> builder)
    {
        builder.HasMany(x => x.Versions)
            .WithOne(x => x.ProductionPlan)
            .HasForeignKey(x => x.ProductionPlanId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.ActiveVersion)
            .WithMany()
            .HasForeignKey(x => x.ActiveVersionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class ProcessFormulaItemConfiguration : IEntityTypeConfiguration<ProcessFormulaItem>
{
    public void Configure(EntityTypeBuilder<ProcessFormulaItem> builder)
    {
        builder.HasOne(x => x.ProcessFormula)
            .WithMany(x => x.Items)
            .HasForeignKey(x => x.ProcessFormulaId)
            .OnDelete(DeleteBehavior.Cascade);

        // MaterialItem already reaches formula items through ProcessFormula. A second
        // cascading path is rejected by SQL Server and is unsafe for production history.
        builder.HasOne(x => x.MaterialItem)
            .WithMany()
            .HasForeignKey(x => x.MaterialItemId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class ProcessFormulaCoProductConfiguration : IEntityTypeConfiguration<ProcessFormulaCoProduct>
{
    public void Configure(EntityTypeBuilder<ProcessFormulaCoProduct> builder)
    {
        builder.HasOne(x => x.ProcessFormula)
            .WithMany(x => x.CoProducts)
            .HasForeignKey(x => x.ProcessFormulaId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.MaterialItem)
            .WithMany()
            .HasForeignKey(x => x.MaterialItemId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class PlanVersionConfiguration : IEntityTypeConfiguration<PlanVersion>
{
    public void Configure(EntityTypeBuilder<PlanVersion> builder)
    {
        builder.HasOne(x => x.ProductionPlan)
            .WithMany(x => x.Versions)
            .HasForeignKey(x => x.ProductionPlanId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
