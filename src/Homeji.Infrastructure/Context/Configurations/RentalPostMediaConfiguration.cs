using Homeji.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Homeji.Infrastructure.Context.Configurations;

public sealed class RentalPostMediaConfiguration : IEntityTypeConfiguration<RentalPostMedia>
{
    public void Configure(EntityTypeBuilder<RentalPostMedia> builder)
    {
        builder.ToTable("rental_post_media", "homeji");
        builder.HasKey(media => media.Id).HasName("pk_rental_post_media");
        builder.Property(media => media.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(media => media.RentalPostId).HasColumnName("rental_post_id").IsRequired();
        builder.Property(media => media.MediaType).HasColumnName("media_type").HasConversion<int>().IsRequired();
        builder.Property(media => media.Bucket).HasColumnName("bucket").HasMaxLength(RentalPostMedia.MaxBucketLength).IsRequired();
        builder.Property(media => media.Path).HasColumnName("path").HasMaxLength(RentalPostMedia.MaxPathLength).IsRequired();
        builder.Property(media => media.IsThumbnail).HasColumnName("is_thumbnail").IsRequired();
        builder.Property(media => media.SortOrder).HasColumnName("sort_order").IsRequired();
        builder.Property(media => media.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.HasIndex(media => media.RentalPostId).HasDatabaseName("ix_rental_post_media_post_id");
        builder.HasIndex(media => media.Path).IsUnique().HasDatabaseName("ux_rental_post_media_path");
    }
}
