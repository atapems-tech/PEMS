using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PEMS.Domain.Entities;

namespace PEMS.Persistence.Configurations;

public class EquipmentCriticalityConfiguration : IEntityTypeConfiguration<EquipmentCriticality>
{
    public void Configure(EntityTypeBuilder<EquipmentCriticality> builder)
    {
        // طبق ممیزی مستقل فاز ۶B Wave A (P1): حداکثر یک رکورد فعال به‌ازای هر Equipment - همون
        // الگوی Unique-فقط-روی-فعال که در CrewMember/TeamMember استفاده شده (فیلتر به‌جای ستون
        // ترکیبی، چون رکوردهای تاریخی غیرفعال نباید محدود شوند). رابطه‌ی FK/OnDelete موجود
        // (CASCADE - از Convention) عمداً این‌جا بازنویسی نشده تا رفتار فعلی تغییر نکند.
        builder.HasIndex(x => x.EquipmentId)
            .IsUnique()
            .HasFilter("[IsActive] = 1");
    }
}
