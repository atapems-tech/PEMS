using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PEMS.Domain.Entities;

namespace PEMS.Persistence.Configurations;

public class WarehouseConfiguration : IEntityTypeConfiguration<Warehouse>
{
    public void Configure(EntityTypeBuilder<Warehouse> builder)
    {
        builder.ToTable("Warehouses");
        builder.HasKey(x => x.WarehouseId);

        builder.Property(x => x.Code).HasMaxLength(30).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(150).IsRequired();

        builder.HasIndex(x => x.Code).IsUnique();
    }
}

public class StorageLocationConfiguration : IEntityTypeConfiguration<StorageLocation>
{
    public void Configure(EntityTypeBuilder<StorageLocation> builder)
    {
        builder.ToTable("StorageLocations");
        builder.HasKey(x => x.StorageLocationId);

        builder.Property(x => x.Code).HasMaxLength(30).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(150).IsRequired();

        builder.HasOne(x => x.Warehouse)
            .WithMany(x => x.Locations)
            .HasForeignKey(x => x.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Parent)
            .WithMany(x => x.Children)
            .HasForeignKey(x => x.ParentLocationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.WarehouseId, x.Code }).IsUnique();
    }
}

public class MaterialStockConfiguration : IEntityTypeConfiguration<MaterialStock>
{
    public void Configure(EntityTypeBuilder<MaterialStock> builder)
    {
        builder.ToTable("MaterialStocks");
        builder.HasKey(x => x.MaterialStockId);

        builder.Property(x => x.OnHandQuantity).HasColumnType("decimal(18,3)");
        builder.Property(x => x.ReservedQuantity).HasColumnType("decimal(18,3)");
        builder.Property(x => x.MinimumQuantity).HasColumnType("decimal(18,3)");
        builder.Property(x => x.MaximumQuantity).HasColumnType("decimal(18,3)");
        builder.Property(x => x.ReorderPoint).HasColumnType("decimal(18,3)");
        builder.Property(x => x.ReorderQuantity).HasColumnType("decimal(18,3)");

        builder.HasOne(x => x.MaterialItem)
            .WithMany()
            .HasForeignKey(x => x.MaterialItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Warehouse)
            .WithMany()
            .HasForeignKey(x => x.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.StorageLocation)
            .WithMany()
            .HasForeignKey(x => x.StorageLocationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.MaterialItemId, x.WarehouseId, x.StorageLocationId }).IsUnique();

        // Phase 2 / P4: کلید کسب‌وکار (متریال، انبار، محل) برای محل NULL («موجودی بدون محل») تا اینجا هیچ یکتایی نداشت - EF برای ایندکس یکتای
        // ستون Nullable خودکار Filter «IS NOT NULL» می‌گذارد. دو دریافت هم‌زمانِ اول می‌توانستند دو ردیف NULL-location بسازند و
        // رزرو/تحویل روی ردیف دلخواه برود. این ایندکس فقط یک ایندکس است (ستون/Entity تغییر نمی‌کند، پس کد روی Schema بدون این Migration هم کار می‌کند).
        builder.HasIndex(x => new { x.MaterialItemId, x.WarehouseId })
            .IsUnique()
            .HasFilter("[StorageLocationId] IS NULL")
            .HasDatabaseName("UX_MaterialStocks_Item_Warehouse_NoLocation");
    }
}
