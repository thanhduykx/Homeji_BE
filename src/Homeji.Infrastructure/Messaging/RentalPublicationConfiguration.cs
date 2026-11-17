using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Homeji.Infrastructure.Messaging;

internal sealed class RentalPublicationConfiguration : IEntityTypeConfiguration<RentalPublication>
{
    public void Configure(EntityTypeBuilder<RentalPublication> builder)
    {
        builder.ToTable("rental_publications", "homeji");
        builder.HasKey(publication => publication.Id);
        builder.Property(publication => publication.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(publication => publication.RentalPostId).HasColumnName("rental_post_id");
        builder.Property(publication => publication.OccurredAt).HasColumnName("occurred_at");
        builder.Property(publication => publication.AvailableAt).HasColumnName("available_at");
        builder.Property(publication => publication.PublishedAt).HasColumnName("published_at");
        builder.Property(publication => publication.LeaseId).HasColumnName("lease_id");
        builder.Property(publication => publication.Attempts).HasColumnName("attempts");
        builder.HasIndex(publication => new { publication.AvailableAt, publication.Id })
            .HasDatabaseName("ix_rental_publications_available")
            .HasFilter("published_at IS NULL");
    }
}
