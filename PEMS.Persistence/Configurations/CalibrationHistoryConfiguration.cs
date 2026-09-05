using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PEMS.Domain.Entities;

namespace PEMS.Persistence.Configurations;

public class CalibrationHistoryConfiguration : IEntityTypeConfiguration<CalibrationHistory>
{
    public void Configure(EntityTypeBuilder<CalibrationHistory> builder)
    {
        builder.HasOne(x => x.Instrument)
            .WithMany()
            .HasForeignKey(x => x.InstrumentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Technician)
            .WithMany()
            .HasForeignKey(x => x.TechnicianEmployeeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
