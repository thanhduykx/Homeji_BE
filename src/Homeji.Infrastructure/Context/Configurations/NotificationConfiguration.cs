using Homeji.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Homeji.Infrastructure.Context.Configurations;

public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("notifications", "homeji");
        builder.HasKey(notification => notification.Id).HasName("pk_notifications");
        builder.Property(notification => notification.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(notification => notification.RecipientId).HasColumnName("recipient_id").IsRequired();
        builder.Property(notification => notification.Type).HasColumnName("type").HasConversion<int>().IsRequired();
        builder.Property(notification => notification.Title).HasColumnName("title").HasMaxLength(Notification.MaxTitleLength).IsRequired();
        builder.Property(notification => notification.Message).HasColumnName("message").HasMaxLength(Notification.MaxMessageLength).IsRequired();
        builder.Property(notification => notification.RelatedEntityId).HasColumnName("related_entity_id");
        builder.Property(notification => notification.IsRead).HasColumnName("is_read").IsRequired();
        builder.Property(notification => notification.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(notification => notification.ReadAt).HasColumnName("read_at");
        builder.HasIndex(notification => new { notification.RecipientId, notification.IsRead, notification.CreatedAt })
            .HasDatabaseName("ix_notifications_recipient_unread_created");
    }
}
