using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PEMS.Domain.Entities;

namespace PEMS.Persistence.Configurations;

/// <summary>
/// طبق «PEMS Phase 5 — Final Reliability Intelligence» بند ۴۳: Trend/Pareto/DataQuality همه
/// روی (IsActive, FailureDate) فیلتر/گروه‌بندی می‌کنند و MTTR/DataQuality روی
/// (Status, CompletionDate) - این دو ایندکس دقیقاً همان الگوی Query جدید این فاز را پوشش
/// می‌دهند؛ کانفیگ افزودنی مجزا طبق الگوی چندکلاسه‌ی همین پروژه.
/// </summary>
public class ReliabilityIndexConfiguration : IEntityTypeConfiguration<FailureRecord>
{
    public void Configure(EntityTypeBuilder<FailureRecord> builder)
    {
        builder.HasIndex(x => new { x.IsActive, x.FailureDate })
            .HasDatabaseName("IX_FailureRecords_IsActive_FailureDate");
    }
}

public class WorkOrderReliabilityIndexConfiguration : IEntityTypeConfiguration<WorkOrder>
{
    public void Configure(EntityTypeBuilder<WorkOrder> builder)
    {
        builder.HasIndex(x => new { x.Status, x.CompletionDate })
            .HasDatabaseName("IX_WorkOrders_Status_CompletionDate");
    }
}

/// <summary>
/// طبق «PEMS Phase 7 — Maintenance Intelligence» بند ۲۷: این دو ایندکس دقیقاً همان الگوی
/// Query تازه‌ی این فاز را پوشش می‌دهند - نه بیشتر:
/// (WorkOrderType, CreatedDate) برای شکست Preventive/Corrective/Emergency در یک بازه‌ی زمانی
/// (GetMaintenanceTypeBreakdownAsync)، و (EquipmentId, FailureModeId, FailureDate) برای
/// تشخیص خرابی تکراری با پنجره‌ی زمانی روی هر ترکیب تجهیز+حالت‌خرابی (GetRepeatFailuresAsync).
/// </summary>
public class WorkOrderMaintenanceIntelligenceIndexConfiguration : IEntityTypeConfiguration<WorkOrder>
{
    public void Configure(EntityTypeBuilder<WorkOrder> builder)
    {
        builder.HasIndex(x => new { x.WorkOrderType, x.CreatedDate })
            .HasDatabaseName("IX_WorkOrders_WorkOrderType_CreatedDate");
    }
}

public class FailureRecordRepeatFailureIndexConfiguration : IEntityTypeConfiguration<FailureRecord>
{
    public void Configure(EntityTypeBuilder<FailureRecord> builder)
    {
        builder.HasIndex(x => new { x.EquipmentId, x.FailureModeId, x.FailureDate })
            .HasDatabaseName("IX_FailureRecords_EquipmentId_FailureModeId_FailureDate");
    }
}
