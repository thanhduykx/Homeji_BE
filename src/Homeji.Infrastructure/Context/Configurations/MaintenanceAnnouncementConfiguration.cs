using Homeji.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Homeji.Infrastructure.Context.Configurations;

public sealed class MaintenanceAnnouncementConfiguration : IEntityTypeConfiguration<MaintenanceAnnouncement>
{
    public void Configure(EntityTypeBuilder<MaintenanceAnnouncement> builder)
    {
        builder.ToTable("maintenance_announcements", "homeji");
        builder.HasKey(row => row.Id);
        builder.Property(row => row.Title).HasMaxLength(Notification.MaxTitleLength).IsRequired();
        builder.Property(row => row.Message).HasMaxLength(Notification.MaxMessageLength).IsRequired();
        builder.HasIndex(row => new { row.CreatedAt, row.Id });
    }
}
