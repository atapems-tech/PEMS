using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PEMS.Domain.Entities;

namespace PEMS.Persistence.Configurations;

/// <summary>
/// طبق «PEMS — Standards-Based Mechanical Equipment Passport & Condition Monitoring» فاز ۴:
/// پیوند اختیاری MaintenanceStrategy(StrategyType=CBM) به ConditionMonitoringPoint - Restrict،
/// دقیقاً همون قرارداد کل کدبیس (رجوع کنید به ConditionMonitoringConfiguration.EquipmentId).
/// </summary>
public class MaintenanceStrategyCMConfiguration : IEntityTypeConfiguration<MaintenanceStrategy>
{
    public void Configure(EntityTypeBuilder<MaintenanceStrategy> builder)
    {
        builder.HasOne(x => x.ConditionMonitoringPoint)
            .WithMany()
            .HasForeignKey(x => x.ConditionMonitoringPointId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.ConditionMonitoringPointId)
            .HasDatabaseName("IX_MaintenanceStrategies_ConditionMonitoringPointId");
    }
}
