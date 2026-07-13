using Homeji.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Homeji.Infrastructure.Context.Configurations;

public sealed class MarketplacePostConfiguration : IEntityTypeConfiguration<MarketplacePost>
{
    public void Configure(EntityTypeBuilder<MarketplacePost> builder)
    {
        builder.ToTable("marketplace_posts", "homeji");
        builder.HasKey(post => post.Id).HasName("pk_marketplace_posts");
        builder.Property(post => post.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(post => post.SellerId).HasColumnName("seller_id").IsRequired();
        builder.Property(post => post.Status).HasColumnName("status").HasConversion<int>().IsRequired();
        builder.Property(post => post.Title).HasColumnName("title").HasMaxLength(MarketplacePost.MaxTitleLength).IsRequired();
        builder.Property(post => post.Description).HasColumnName("description").HasMaxLength(MarketplacePost.MaxDescriptionLength).IsRequired();
        builder.Property(post => post.Price).HasColumnName("price").HasPrecision(18, 2).IsRequired();
        builder.Property(post => post.Condition).HasColumnName("condition").HasMaxLength(MarketplacePost.MaxConditionLength).IsRequired();
        builder.Property(post => post.Category).HasColumnName("category").HasMaxLength(MarketplacePost.MaxCategoryLength).IsRequired();
        builder.Property(post => post.Address).HasColumnName("address").HasMaxLength(MarketplacePost.MaxAddressLength).IsRequired();
        builder.Property(post => post.Latitude).HasColumnName("latitude").HasPrecision(9, 6).IsRequired();
        builder.Property(post => post.Longitude).HasColumnName("longitude").HasPrecision(9, 6).IsRequired();
        builder.Property(post => post.LinkedRentalPostId).HasColumnName("linked_rental_post_id");
        builder.Property(post => post.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(post => post.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.HasMany(post => post.Media)
            .WithOne()
            .HasForeignKey(media => media.MarketplacePostId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(post => post.Media).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasOne<UserProfile>()
            .WithMany()
            .HasForeignKey(post => post.SellerId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_marketplace_posts_sellers");
        builder.HasOne<RentalPost>()
            .WithMany()
            .HasForeignKey(post => post.LinkedRentalPostId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("fk_marketplace_posts_linked_rental_posts");
        builder.HasIndex(post => new { post.Status, post.UpdatedAt }).HasDatabaseName("ix_marketplace_posts_status_updated");
        builder.HasIndex(post => new { post.Status, post.Latitude, post.Longitude }).HasDatabaseName("ix_marketplace_posts_map_search");
        builder.HasIndex(post => post.SellerId).HasDatabaseName("ix_marketplace_posts_seller_id");
        builder.HasIndex(post => post.LinkedRentalPostId).HasDatabaseName("ix_marketplace_posts_linked_rental_post_id");
        builder.ToTable(table => table.HasCheckConstraint("ck_marketplace_posts_price", "price > 0"));
    }
}
