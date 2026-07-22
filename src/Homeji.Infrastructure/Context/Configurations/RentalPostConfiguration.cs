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
        builder.Property(post => post.ElectricityPrice).HasColumnName("electricity_price").HasPrecision(18, 2).IsRequired();
        builder.Property(post => post.WaterPrice).HasColumnName("water_price").HasPrecision(18, 2).IsRequired();
        builder.Property(post => post.InternetPrice).HasColumnName("internet_price").HasPrecision(18, 2).IsRequired();
        builder.Property(post => post.MaxOccupants).HasColumnName("max_occupants").IsRequired();
        builder.Property(post => post.AvailableSlots).HasColumnName("available_slots").IsRequired();
        builder.Property(post => post.HouseRules).HasColumnName("house_rules").HasMaxLength(RentalPost.MaxHouseRulesLength);
        builder.Property(post => post.AvailableFrom).HasColumnName("available_from");
        builder.Property(post => post.TransferKind).HasColumnName("transfer_kind").HasConversion<int?>();
        builder.Property(post => post.OriginalLeaseEndsOn).HasColumnName("original_lease_ends_on");
        builder.Property(post => post.PassFee).HasColumnName("pass_fee").HasPrecision(18, 2).IsRequired();
        builder.Property(post => post.TransferReason).HasColumnName("transfer_reason").HasMaxLength(RentalPost.MaxTransferReasonLength);
        builder.Property(post => post.OwnerConsentConfirmed).HasColumnName("owner_consent_confirmed").IsRequired();
        builder.Property(post => post.OwnerConsentContact).HasColumnName("owner_consent_contact").HasMaxLength(RentalPost.MaxOwnerConsentContactLength);
        builder.Property(post => post.OwnerConsentVerifiedAt).HasColumnName("owner_consent_verified_at");
        builder.Property(post => post.OwnerConsentVerifiedBy).HasColumnName("owner_consent_verified_by");
        builder.Property(post => post.OwnerConsentVerificationNote).HasColumnName("owner_consent_verification_note").HasMaxLength(RentalPost.MaxOwnerConsentVerificationNoteLength);
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
