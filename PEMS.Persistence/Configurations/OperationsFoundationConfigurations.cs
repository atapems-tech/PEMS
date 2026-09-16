using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PEMS.Domain.Entities;

namespace PEMS.Persistence.Configurations;

/// <summary>
/// طبق «PEMS Operations Phase 2A — Foundational Hardening»: پیکربندی EF برای ProductionTarget و
/// ProductionDowntimeEvent - هم‌الگو دقیق با ProductionConfigurations.cs (فاز ۱).
/// </summary>
public class ProductionTargetConfiguration : IEntityTypeConfiguration<ProductionTarget>
{
    public void Configure(EntityTypeBuilder<ProductionTarget> builder)
    {
        builder.ToTable("ProductionTargets");
        builder.HasKey(x => x.ProductionTargetId);

        builder.Property(x => x.TargetQuantity).HasColumnType("decimal(18,3)");

        builder.HasIndex(x => x.TargetDate);
        builder.HasIndex(x => x.ProductionWorkCenterId);

        // طبق بخش ۶ سند فاز («Required constraints» - جلوگیری از هدف تکراری برای همون گرین
        // بعدی): یکتایی روی (تاریخ، مرکز کاری، متریال، شیفت). ShiftDefinitionId می‌تواند Null
        // باشد (هدف روزانه‌ی بدون شیفت مشخص) - چون SQL Server مقادیر NULL را در ایندکس یکتا
        // به‌عنوان متمایز در نظر می‌گیرد، این یعنی چند هدف Null-شیفت برای همون روز/مرکز/متریال
        // هنوز فنی مجاز می‌ماند؛ محدودیت سطح Application (ProductionTargetService) این حالت را
        // هم می‌بندد - طبق همون الگوی محافظت دوگانه‌ی BarcodeIdentity (ایندکس دیتابیس + بررسی
        // سطح سرویس، نه فقط یکی).
        builder.HasIndex(x => new { x.TargetDate, x.ProductionWorkCenterId, x.MaterialItemId, x.ShiftDefinitionId }).IsUnique();

        builder.HasOne(x => x.ProductionOrder)
            .WithMany()
            .HasForeignKey(x => x.ProductionOrderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.MaterialItem)
            .WithMany()
            .HasForeignKey(x => x.MaterialItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ProductionWorkCenter)
            .WithMany()
            .HasForeignKey(x => x.ProductionWorkCenterId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ShiftDefinition)
            .WithMany()
            .HasForeignKey(x => x.ShiftDefinitionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Uom)
            .WithMany()
            .HasForeignKey(x => x.UomId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class ProductionDowntimeEventConfiguration : IEntityTypeConfiguration<ProductionDowntimeEvent>
{
    public void Configure(EntityTypeBuilder<ProductionDowntimeEvent> builder)
    {
        builder.ToTable("ProductionDowntimeEvents");
        builder.HasKey(x => x.ProductionDowntimeEventId);

        builder.Property(x => x.CauseCategory).HasMaxLength(30).IsRequired();

        builder.HasIndex(x => x.ProductionRunId);
        builder.HasIndex(x => x.ProductionWorkCenterId);
        builder.HasIndex(x => x.StartTime);

        builder.HasOne(x => x.ProductionRun)
            .WithMany()
            .HasForeignKey(x => x.ProductionRunId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ProductionWorkCenter)
            .WithMany()
            .HasForeignKey(x => x.ProductionWorkCenterId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Equipment)
            .WithMany()
            .HasForeignKey(x => x.EquipmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.SourceWorkRequest)
            .WithMany()
            .HasForeignKey(x => x.SourceWorkRequestId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.SourceWorkOrder)
            .WithMany()
            .HasForeignKey(x => x.SourceWorkOrderId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
