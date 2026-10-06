using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PEMS.Domain.Entities;

namespace PEMS.Persistence.Configurations;

/// <summary>
/// طبق «PEMS — Equipment Passport Audit & CBM» فاز ۱ ممیزی §3.7: هر وضعیت (Condition) دقیقاً یک
/// Priority پیش‌فرض دارد - ایندکس Unique جلوگیری می‌کند از این‌که دو ردیف متناقض برای یک Condition
/// ثبت شود.
/// </summary>
public class ConditionPriorityMappingConfiguration : IEntityTypeConfiguration<ConditionPriorityMapping>
{
    public void Configure(EntityTypeBuilder<ConditionPriorityMapping> builder)
    {
        builder.Property(x => x.Condition).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Priority).HasMaxLength(20).IsRequired();

        builder.HasIndex(x => x.Condition).IsUnique();
    }
}
