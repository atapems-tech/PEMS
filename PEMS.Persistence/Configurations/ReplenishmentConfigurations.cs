using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PEMS.Domain.Entities;

namespace PEMS.Persistence.Configurations;

public class NetDemandRunConfiguration : IEntityTypeConfiguration<NetDemandRun>
{
    public void Configure(EntityTypeBuilder<NetDemandRun> builder)
    {
        builder.ToTable("NetDemandRuns");
        builder.HasKey(x => x.NetDemandRunId);

        builder.Property(x => x.RequestKey).HasMaxLength(64);
        builder.Property(x => x.Status).HasMaxLength(20).IsRequired();
        builder.Property(x => x.FailureMessage).HasMaxLength(500);
        builder.Property(x => x.ScopeMaterialItemIds).HasMaxLength(2000);
        builder.Property(x => x.ScopeWarehouseIds).HasMaxLength(2000);
        builder.Property(x => x.NotDefinitiveSummary).HasMaxLength(4000);

        // اجرای هم‌کلید فقط یک‌بار ثبت می‌شود (Idempotency)
        builder.HasIndex(x => x.RequestKey).IsUnique().HasFilter("[RequestKey] IS NOT NULL").HasDatabaseName("UX_NetDemandRuns_RequestKey");
    }
}

public class ReplenishmentRecommendationConfiguration : IEntityTypeConfiguration<ReplenishmentRecommendation>
{
    public void Configure(EntityTypeBuilder<ReplenishmentRecommendation> builder)
    {
        builder.ToTable("ReplenishmentRecommendations");
        builder.HasKey(x => x.ReplenishmentRecommendationId);

        builder.Property(x => x.Status).HasMaxLength(20).IsRequired();
        foreach (var p in new[] { nameof(ReplenishmentRecommendation.OnHandQuantity), nameof(ReplenishmentRecommendation.ReservedQuantity),
                     nameof(ReplenishmentRecommendation.UsableQuantity), nameof(ReplenishmentRecommendation.GrossEligibleDemand),
                     nameof(ReplenishmentRecommendation.CoveredByReservations), nameof(ReplenishmentRecommendation.UnreservedDemand),
                     nameof(ReplenishmentRecommendation.ShortageQuantity), nameof(ReplenishmentRecommendation.RecommendedQuantity),
                     nameof(ReplenishmentRecommendation.DismissedShortageQuantity) })
            builder.Property(p).HasColumnType("decimal(18,3)");

        builder.Property(x => x.NoticeCodes).HasMaxLength(500);
        builder.Property(x => x.BlockingIssueCodes).HasMaxLength(500);
        builder.Property(x => x.InputFingerprint).HasMaxLength(64).IsRequired();
        builder.Property(x => x.DismissedReason).HasMaxLength(500);
        builder.Property(x => x.DismissedSourceKeys).HasMaxLength(4000);
        builder.Property(x => x.ClosedReason).HasMaxLength(100);

        builder.HasOne(x => x.MaterialItem).WithMany().HasForeignKey(x => x.MaterialItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Warehouse).WithMany().HasForeignKey(x => x.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<NetDemandRun>().WithMany().HasForeignKey(x => x.FirstRunId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<NetDemandRun>().WithMany().HasForeignKey(x => x.LastRunId).OnDelete(DeleteBehavior.Restrict);

        // حداکثر یک توصیه‌ی «زنده» (Open یا Dismissed) برای هر (متریال، انبار)؛ توصیه‌ی بسته/تبدیل‌شده مانع توصیه‌ی تازه نیست
        builder.HasIndex(x => new { x.MaterialItemId, x.WarehouseId })
            .IsUnique()
            .HasFilter("[Status] IN (N'Open', N'Dismissed')")
            .HasDatabaseName("UX_ReplenishmentRecommendations_Live");
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.LastRunId);
    }
}

public class ReplenishmentRecommendationSourceConfiguration : IEntityTypeConfiguration<ReplenishmentRecommendationSource>
{
    public void Configure(EntityTypeBuilder<ReplenishmentRecommendationSource> builder)
    {
        builder.ToTable("ReplenishmentRecommendationSources", t =>
            t.HasCheckConstraint("CK_ReplenishmentRecommendationSources_OneSource",
                "([SourceType] = 1 AND [StoreRequisitionLineId] IS NOT NULL AND [WorkOrderMaterialId] IS NULL) OR " +
                "([SourceType] = 2 AND [WorkOrderMaterialId] IS NOT NULL AND [StoreRequisitionLineId] IS NULL)"));
        builder.HasKey(x => x.ReplenishmentRecommendationSourceId);

        builder.Property(x => x.GrossRemainingQuantity).HasColumnType("decimal(18,3)");
        builder.Property(x => x.CoveredByReservation).HasColumnType("decimal(18,3)");
        builder.Property(x => x.UnreservedQuantity).HasColumnType("decimal(18,3)");

        builder.HasOne(x => x.Recommendation).WithMany(x => x.Sources)
            .HasForeignKey(x => x.ReplenishmentRecommendationId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.ReplenishmentRecommendationId, x.Revision });
        builder.HasIndex(x => x.StoreRequisitionLineId).HasFilter("[StoreRequisitionLineId] IS NOT NULL");
        builder.HasIndex(x => x.WorkOrderMaterialId).HasFilter("[WorkOrderMaterialId] IS NOT NULL");
    }
}

public class ReplenishmentRecommendationHistoryConfiguration : IEntityTypeConfiguration<ReplenishmentRecommendationHistory>
{
    public void Configure(EntityTypeBuilder<ReplenishmentRecommendationHistory> builder)
    {
        builder.ToTable("ReplenishmentRecommendationHistories");
        builder.HasKey(x => x.ReplenishmentRecommendationHistoryId);

        builder.Property(x => x.Action).HasMaxLength(30).IsRequired();
        builder.Property(x => x.FromStatus).HasMaxLength(20);
        builder.Property(x => x.ToStatus).HasMaxLength(20).IsRequired();
        builder.Property(x => x.ShortageQuantity).HasColumnType("decimal(18,3)");
        builder.Property(x => x.UsableQuantity).HasColumnType("decimal(18,3)");
        builder.Property(x => x.UnreservedDemand).HasColumnType("decimal(18,3)");
        builder.Property(x => x.InputFingerprint).HasMaxLength(64);
        builder.Property(x => x.Note).HasMaxLength(500);

        builder.HasOne(x => x.Recommendation).WithMany(x => x.History)
            .HasForeignKey(x => x.ReplenishmentRecommendationId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.ReplenishmentRecommendationId, x.Revision, x.ReplenishmentRecommendationHistoryId });
    }
}
