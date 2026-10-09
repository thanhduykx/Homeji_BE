using Homeji.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System.Text.Json;

namespace Homeji.Infrastructure.Context.Configurations;

public sealed class RentalSourceListingConfiguration : IEntityTypeConfiguration<RentalSourceListing>
{
    public void Configure(EntityTypeBuilder<RentalSourceListing> builder)
    {
        builder.ToTable("rental_source_listings", "homeji", table =>
        {
            table.HasCheckConstraint("ck_rental_source_listings_district", "district IN ('quan-9', 'thu-duc')");
            table.HasCheckConstraint("ck_rental_source_listings_price_area", "price > 0 AND price <= 100000000 AND area > 0 AND area <= 1000");
            table.HasCheckConstraint("ck_rental_source_listings_images", "jsonb_typeof(image_urls) = 'array' AND jsonb_array_length(image_urls) BETWEEN 1 AND 10");
        });
        builder.HasKey(listing => listing.Id);
        builder.Property(listing => listing.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(listing => listing.Source).HasColumnName("source").HasMaxLength(80).IsRequired();
        builder.Property(listing => listing.SourceId).HasColumnName("source_id").HasMaxLength(80).IsRequired();
        builder.Property(listing => listing.SourceUrl).HasColumnName("source_url").HasMaxLength(1000).IsRequired();
        builder.Property(listing => listing.Title).HasColumnName("title").HasMaxLength(200).IsRequired();
        builder.Property(listing => listing.Address).HasColumnName("address").HasMaxLength(500).IsRequired();
        builder.Property(listing => listing.District).HasColumnName("district").HasMaxLength(20).IsRequired();
        builder.Property(listing => listing.Price).HasColumnName("price").HasPrecision(18, 2);
        builder.Property(listing => listing.Area).HasColumnName("area").HasPrecision(10, 2);
        builder.Property(listing => listing.ImageUrls).HasColumnName("image_urls").HasColumnType("jsonb")
            .HasConversion(
                value => JsonSerializer.Serialize(value, (JsonSerializerOptions?)null),
                value => JsonSerializer.Deserialize<string[]>(value, (JsonSerializerOptions?)null)!,
                new ValueComparer<string[]>(
                    (first, second) => first!.SequenceEqual(second!),
                    value => value.Aggregate(0, (hash, item) => HashCode.Combine(hash, item.GetHashCode())),
                    value => value.ToArray()))
            .IsRequired();
        builder.Property(listing => listing.SourceUpdatedAt).HasColumnName("source_updated_at");
        builder.Property(listing => listing.CollectedAt).HasColumnName("collected_at");
        builder.Property(listing => listing.SourceExpiresAt).HasColumnName("source_expires_at");
        builder.Property(listing => listing.SourceCheckedAt).HasColumnName("source_checked_at");
        builder.HasIndex(listing => new { listing.Source, listing.SourceId }).IsUnique();
        builder.HasIndex(listing => new { listing.District, listing.CollectedAt, listing.Id });
    }
}
