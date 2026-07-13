using Homeji.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Homeji.Infrastructure.Context.Configurations;

public sealed class MarketplacePostMediaConfiguration : IEntityTypeConfiguration<MarketplacePostMedia>
{
    public void Configure(EntityTypeBuilder<MarketplacePostMedia> builder)
    {
        builder.ToTable("marketplace_post_media", "homeji");
        builder.HasKey(media => media.Id).HasName("pk_marketplace_post_media");
        builder.Property(media => media.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(media => media.MarketplacePostId).HasColumnName("marketplace_post_id").IsRequired();
        builder.Property(media => media.Url).HasColumnName("url").HasMaxLength(MarketplacePostMedia.MaxUrlLength).IsRequired();
        builder.Property(media => media.SortOrder).HasColumnName("sort_order").IsRequired();
        builder.HasIndex(media => new { media.MarketplacePostId, media.SortOrder })
            .HasDatabaseName("ix_marketplace_post_media_post_sort");
        builder.HasIndex(media => new { media.MarketplacePostId, media.Url })
            .IsUnique()
            .HasDatabaseName("ux_marketplace_post_media_post_url");
    }
}
