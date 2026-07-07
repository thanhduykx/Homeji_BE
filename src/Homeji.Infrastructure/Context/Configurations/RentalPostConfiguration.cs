using Homeji.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Homeji.Infrastructure.Context.Configurations;

public sealed class RentalPostConfiguration : IEntityTypeConfiguration<RentalPost>
{
    public void Configure(EntityTypeBuilder<RentalPost> builder)
    {
        builder.ToTable("rental_posts", "homeji");
        builder.HasKey(post => post.Id).HasName("pk_rental_posts");

        builder.Property(post => post.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(post => post.OwnerId).HasColumnName("owner_id").IsRequired();
        builder.Property(post => post.Type).HasColumnName("type").HasConversion<int>().IsRequired();
        builder.Property(post => post.Status).HasColumnName("status").HasConversion<int>().IsRequired();
        builder.Property(post => post.Title).HasColumnName("title").HasMaxLength(RentalPost.MaxTitleLength).IsRequired();
        builder.Property(post => post.Description).HasColumnName("description").HasMaxLength(RentalPost.MaxDescriptionLength).IsRequired();
        builder.Property(post => post.Price).HasColumnName("price").HasPrecision(18, 2).IsRequired();
        builder.Property(post => post.Deposit).HasColumnName("deposit").HasPrecision(18, 2).IsRequired();
        builder.Property(post => post.Area).HasColumnName("area").HasPrecision(10, 2).IsRequired();
        builder.Property(post => post.Address).HasColumnName("address").HasMaxLength(RentalPost.MaxAddressLength).IsRequired();
        builder.Property(post => post.Latitude).HasColumnName("latitude").HasPrecision(10, 7).IsRequired();
        builder.Property(post => post.Longitude).HasColumnName("longitude").HasPrecision(10, 7).IsRequired();
        builder.Property(post => post.ModerationReason).HasColumnName("moderation_reason").HasMaxLength(RentalPost.MaxModerationReasonLength);
        builder.Property(post => post.ViewCount).HasColumnName("view_count").IsRequired();
        builder.Property(post => post.SaveCount).HasColumnName("save_count").IsRequired();
        builder.Property(post => post.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(post => post.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.HasMany(post => post.Media)
            .WithOne()
            .HasForeignKey(media => media.RentalPostId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(post => post.Amenities)
            .WithOne()
            .HasForeignKey(amenity => amenity.RentalPostId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(post => post.Media).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(post => post.Amenities).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(post => post.OwnerId).HasDatabaseName("ix_rental_posts_owner_id");
        builder.HasIndex(post => post.Status).HasDatabaseName("ix_rental_posts_status");
        builder.HasIndex(post => new { post.Status, post.Latitude, post.Longitude }).HasDatabaseName("ix_rental_posts_map_search");
        builder.HasIndex(post => post.Price).HasDatabaseName("ix_rental_posts_price");
    }
}
