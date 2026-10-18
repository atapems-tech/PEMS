using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PEMS.Domain.Entities;

namespace PEMS.Persistence.Configurations;

/// <summary>
/// طبق فاز ۱ سند CM (بند ۳ ممیزی): تا پیش از این هیچ IEntityTypeConfiguration مستقلی برای
/// WorkRequest وجود نداشت (کاملاً بر پایه‌ی Convention). این کلاس فقط برای ستون تازه‌ی
/// SourceType لازم شد: HasDefaultValue صریح - بدون آن، WorkRequestهای موجود این ستون تازه را
/// "" می‌گیرند نه "Manual" (همان قرارداد NodeKind در MaintenanceWorkforceConfigurations).
/// سایر رفتار/روابط WorkRequest دست‌نخورده و بر پایه‌ی Convention باقی می‌ماند.
/// </summary>
public class WorkRequestConfiguration : IEntityTypeConfiguration<WorkRequest>
{
    public void Configure(EntityTypeBuilder<WorkRequest> builder)
    {
        builder.Property(x => x.SourceType).HasMaxLength(30).IsRequired().HasDefaultValue("Manual");
    }
}
