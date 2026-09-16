using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PEMS.Domain.Entities;

namespace PEMS.Persistence.Configurations;

public class ProductionWorkCenterConfiguration : IEntityTypeConfiguration<ProductionWorkCenter>
{
    public void Configure(EntityTypeBuilder<ProductionWorkCenter> builder)
    {
        builder.ToTable("ProductionWorkCenters");
        builder.HasKey(x => x.ProductionWorkCenterId);

        builder.Property(x => x.Code).HasMaxLength(30).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(150).IsRequired();

        builder.HasIndex(x => x.Code).IsUnique();

        builder.HasOne(x => x.FunctionalLocation)
            .WithMany()
            .HasForeignKey(x => x.FunctionalLocationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Equipment)
            .WithMany()
            .HasForeignKey(x => x.EquipmentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class ShiftDefinitionConfiguration : IEntityTypeConfiguration<ShiftDefinition>
{
    public void Configure(EntityTypeBuilder<ShiftDefinition> builder)
    {
        builder.ToTable("ShiftDefinitions");
        builder.HasKey(x => x.ShiftDefinitionId);

        builder.Property(x => x.Code).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();

        builder.HasIndex(x => x.Code).IsUnique();
    }
}

public class ProductionOrderConfiguration : IEntityTypeConfiguration<ProductionOrder>
{
    public void Configure(EntityTypeBuilder<ProductionOrder> builder)
    {
        builder.ToTable("ProductionOrders");
        builder.HasKey(x => x.ProductionOrderId);

        builder.Property(x => x.OrderNumber).HasMaxLength(40).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(20).IsRequired();
        builder.Property(x => x.PlannedQuantity).HasColumnType("decimal(18,3)");

        // طبق بند ۲۵ سند فاز (Concurrency): جلوگیری از شماره‌ی سفارش تکراری در سطح دیتابیس،
        // نه فقط بررسی سمت اپلیکیشن.
        builder.HasIndex(x => x.OrderNumber).IsUnique();
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.MaterialItemId);

        builder.HasOne(x => x.MaterialItem)
            .WithMany()
            .HasForeignKey(x => x.MaterialItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Uom)
            .WithMany()
            .HasForeignKey(x => x.UomId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ProductionWorkCenter)
            .WithMany()
            .HasForeignKey(x => x.ProductionWorkCenterId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Equipment)
            .WithMany()
            .HasForeignKey(x => x.EquipmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ResponsibleEmployee)
            .WithMany()
            .HasForeignKey(x => x.ResponsibleEmployeeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class ProductionRunConfiguration : IEntityTypeConfiguration<ProductionRun>
{
    public void Configure(EntityTypeBuilder<ProductionRun> builder)
    {
        builder.ToTable("ProductionRuns");
        builder.HasKey(x => x.ProductionRunId);

        builder.Property(x => x.RunNumber).HasMaxLength(40).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(20).IsRequired();

        builder.HasIndex(x => x.RunNumber).IsUnique();
        builder.HasIndex(x => x.ProductionOrderId);
        builder.HasIndex(x => x.Status);

        builder.HasOne(x => x.ProductionOrder)
            .WithMany(x => x.ProductionRuns)
            .HasForeignKey(x => x.ProductionOrderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ProductionWorkCenter)
            .WithMany()
            .HasForeignKey(x => x.ProductionWorkCenterId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Equipment)
            .WithMany()
            .HasForeignKey(x => x.EquipmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.OperatorEmployee)
            .WithMany()
            .HasForeignKey(x => x.OperatorEmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ShiftDefinition)
            .WithMany()
            .HasForeignKey(x => x.ShiftDefinitionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class ProductionBatchConfiguration : IEntityTypeConfiguration<ProductionBatch>
{
    public void Configure(EntityTypeBuilder<ProductionBatch> builder)
    {
        builder.ToTable("ProductionBatches");
        builder.HasKey(x => x.ProductionBatchId);

        builder.Property(x => x.BatchNumber).HasMaxLength(40).IsRequired();
        builder.Property(x => x.QualityStatus).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Quantity).HasColumnType("decimal(18,3)");

        builder.HasIndex(x => x.BatchNumber).IsUnique();
        builder.HasIndex(x => x.ProductionRunId);
        builder.HasIndex(x => x.MaterialItemId);
        builder.HasIndex(x => x.QualityStatus);

        builder.HasOne(x => x.ProductionOrder)
            .WithMany()
            .HasForeignKey(x => x.ProductionOrderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ProductionRun)
            .WithMany()
            .HasForeignKey(x => x.ProductionRunId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.MaterialItem)
            .WithMany()
            .HasForeignKey(x => x.MaterialItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Uom)
            .WithMany()
            .HasForeignKey(x => x.UomId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.QualityStatusChangedBy)
            .WithMany()
            .HasForeignKey(x => x.QualityStatusChangedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class ProductionInputConfiguration : IEntityTypeConfiguration<ProductionInput>
{
    public void Configure(EntityTypeBuilder<ProductionInput> builder)
    {
        builder.ToTable("ProductionInputs");
        builder.HasKey(x => x.ProductionInputId);

        builder.Property(x => x.Quantity).HasColumnType("decimal(18,3)");

        builder.HasIndex(x => x.ProductionRunId);

        builder.HasOne(x => x.ProductionRun)
            .WithMany(x => x.Inputs)
            .HasForeignKey(x => x.ProductionRunId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.MaterialItem)
            .WithMany()
            .HasForeignKey(x => x.MaterialItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Uom)
            .WithMany()
            .HasForeignKey(x => x.UomId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.SourceProductionBatch)
            .WithMany(x => x.ConsumedAsSourceIn)
            .HasForeignKey(x => x.SourceProductionBatchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.InventoryTransaction)
            .WithMany()
            .HasForeignKey(x => x.InventoryTransactionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Employee)
            .WithMany()
            .HasForeignKey(x => x.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class ProductionOutputConfiguration : IEntityTypeConfiguration<ProductionOutput>
{
    public void Configure(EntityTypeBuilder<ProductionOutput> builder)
    {
        builder.ToTable("ProductionOutputs");
        builder.HasKey(x => x.ProductionOutputId);

        builder.Property(x => x.Quantity).HasColumnType("decimal(18,3)");
        builder.Property(x => x.OutputClassification).HasMaxLength(20).IsRequired();

        builder.HasIndex(x => x.ProductionRunId);
        builder.HasIndex(x => x.ProductionBatchId);

        builder.HasOne(x => x.ProductionRun)
            .WithMany(x => x.Outputs)
            .HasForeignKey(x => x.ProductionRunId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.MaterialItem)
            .WithMany()
            .HasForeignKey(x => x.MaterialItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ProductionBatch)
            .WithMany(x => x.Outputs)
            .HasForeignKey(x => x.ProductionBatchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Uom)
            .WithMany()
            .HasForeignKey(x => x.UomId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.InventoryTransaction)
            .WithMany()
            .HasForeignKey(x => x.InventoryTransactionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Employee)
            .WithMany()
            .HasForeignKey(x => x.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class ProductionLossConfiguration : IEntityTypeConfiguration<ProductionLoss>
{
    public void Configure(EntityTypeBuilder<ProductionLoss> builder)
    {
        builder.ToTable("ProductionLosses");
        builder.HasKey(x => x.ProductionLossId);

        builder.Property(x => x.Quantity).HasColumnType("decimal(18,3)");
        builder.Property(x => x.LossCategory).HasMaxLength(30).IsRequired();

        builder.HasIndex(x => x.ProductionRunId);

        builder.HasOne(x => x.ProductionRun)
            .WithMany(x => x.Losses)
            .HasForeignKey(x => x.ProductionRunId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Uom)
            .WithMany()
            .HasForeignKey(x => x.UomId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Employee)
            .WithMany()
            .HasForeignKey(x => x.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class ProductionEventConfiguration : IEntityTypeConfiguration<ProductionEvent>
{
    public void Configure(EntityTypeBuilder<ProductionEvent> builder)
    {
        builder.ToTable("ProductionEvents");
        builder.HasKey(x => x.ProductionEventId);

        builder.Property(x => x.EventType).HasMaxLength(40).IsRequired();

        builder.HasIndex(x => x.ProductionOrderId);
        builder.HasIndex(x => x.ProductionRunId);

        builder.HasOne(x => x.ProductionOrder)
            .WithMany()
            .HasForeignKey(x => x.ProductionOrderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ProductionRun)
            .WithMany(x => x.Events)
            .HasForeignKey(x => x.ProductionRunId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Employee)
            .WithMany()
            .HasForeignKey(x => x.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
