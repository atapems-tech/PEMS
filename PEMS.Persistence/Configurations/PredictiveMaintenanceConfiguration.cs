using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PEMS.Domain.Entities;

namespace PEMS.Persistence.Configurations;

public class PredictiveMaintenanceModelConfiguration : IEntityTypeConfiguration<PredictiveMaintenanceModel>
{
    public void Configure(EntityTypeBuilder<PredictiveMaintenanceModel> builder)
    {
        // بدون FK - این‌جا فقط توصیف مدل است.
    }
}

public class PredictiveMaintenanceResultConfiguration : IEntityTypeConfiguration<PredictiveMaintenanceResult>
{
    public void Configure(EntityTypeBuilder<PredictiveMaintenanceResult> builder)
    {
        builder.HasOne(x => x.PredictiveMaintenanceModel)
            .WithMany(x => x.Results)
            .HasForeignKey(x => x.PredictiveMaintenanceModelId)
            .OnDelete(DeleteBehavior.Cascade);

        // طبق همان استدلال CriticalityAssessmentConfiguration - Restrict روی Equipment
        builder.HasOne(x => x.Equipment)
            .WithMany()
            .HasForeignKey(x => x.EquipmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.EquipmentId, x.GeneratedDate })
            .HasDatabaseName("IX_PredictiveMaintenanceResults_EquipmentId_GeneratedDate");
    }
}
