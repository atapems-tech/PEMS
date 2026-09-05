using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PEMS.Domain.Entities;

namespace PEMS.Persistence.Configurations;

/// <summary>
/// طبق استاندارد جدید Database Design (Volume V): یک کلاس پیکربندی جدا به‌ازای هر Entity،
/// نه پیکربندی داخل DbContext.
/// </summary>
public class VendorConfiguration : IEntityTypeConfiguration<Vendor>
{
    public void Configure(EntityTypeBuilder<Vendor> builder)
    {
        builder.Property(x => x.RiskScore)
            .HasPrecision(5, 4);
    }
}
