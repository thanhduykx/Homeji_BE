using Homeji.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Homeji.Infrastructure.Context.Configurations;

public sealed class RentalPostAmenityConfiguration : IEntityTypeConfiguration<RentalPostAmenity>
{
    public void Configure(EntityTypeBuilder<RentalPostAmenity> builder)
    {
        builder.ToTable("rental_post_amenities", "homeji");
        builder.HasKey(amenity => new { amenity.RentalPostId, amenity.Code }).HasName("pk_rental_post_amenities");
        builder.Property(amenity => amenity.RentalPostId).HasColumnName("rental_post_id").IsRequired();
        builder.Property(amenity => amenity.Code).HasColumnName("code").HasMaxLength(RentalPost.MaxAmenityCodeLength).IsRequired();
        builder.HasIndex(amenity => amenity.Code).HasDatabaseName("ix_rental_post_amenities_code");
    }
}
