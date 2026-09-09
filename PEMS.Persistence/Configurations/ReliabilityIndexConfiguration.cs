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
