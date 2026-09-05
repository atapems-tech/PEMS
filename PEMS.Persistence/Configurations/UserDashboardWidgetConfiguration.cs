using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PEMS.Domain.Entities;

namespace PEMS.Persistence.Configurations;

public class UserDashboardWidgetConfiguration : IEntityTypeConfiguration<UserDashboardWidget>
{
    public void Configure(EntityTypeBuilder<UserDashboardWidget> builder)
    {
        builder.HasIndex(x => new { x.ApplicationUserId, x.WidgetKey }).IsUnique();
    }
}
