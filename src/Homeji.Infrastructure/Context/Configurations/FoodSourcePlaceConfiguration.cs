using Homeji.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Homeji.Infrastructure.Context.Configurations;

public sealed class FoodSourcePlaceConfiguration : IEntityTypeConfiguration<FoodSourcePlace>
{
    public void Configure(EntityTypeBuilder<FoodSourcePlace> builder)
    {
        builder.ToTable("food_source_places", "homeji", table => table.HasCheckConstraint("ck_food_source_coordinates", "(latitude IS NULL AND longitude IS NULL) OR (latitude IS NOT NULL AND longitude IS NOT NULL AND latitude BETWEEN -90 AND 90 AND longitude BETWEEN -180 AND 180)"));
        builder.Property(item => item.Latitude).HasColumnName("latitude").HasPrecision(10, 7);
        builder.Property(item => item.Longitude).HasColumnName("longitude").HasPrecision(10, 7);
        builder.HasKey(place => place.Id);
        builder.Property(place => place.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(place => place.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(place => place.Address).HasColumnName("address").HasMaxLength(500).IsRequired();
        builder.Property(place => place.SourceUrl).HasColumnName("source_url").HasMaxLength(1000).IsRequired();
        builder.Property(place => place.MapsSearchUrl).HasColumnName("maps_search_url").HasMaxLength(1000).IsRequired();
        builder.Property(place => place.VerificationNote).HasColumnName("verification_note").HasMaxLength(1000).IsRequired();
        builder.Property(place => place.CollectedAt).HasColumnName("collected_at");
        builder.HasIndex(place => new { place.Name, place.Address }).IsUnique();
    }
}
