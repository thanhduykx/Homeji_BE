using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Homeji.Infrastructure.Messaging;

internal sealed record RentalPublicationFact(Guid EventId, Guid RentalPostId, DateTimeOffset OccurredAt);

internal sealed class RentalPublicationFactConfiguration : IEntityTypeConfiguration<RentalPublicationFact>
{
    public void Configure(EntityTypeBuilder<RentalPublicationFact> builder)
    {
        builder.ToTable("rental_publication_facts", "homeji");
        builder.HasKey(fact => fact.EventId);
        builder.Property(fact => fact.EventId).HasColumnName("event_id").ValueGeneratedNever();
        builder.Property(fact => fact.RentalPostId).HasColumnName("rental_post_id");
        builder.Property(fact => fact.OccurredAt).HasColumnName("occurred_at");
        builder.HasIndex(fact => fact.OccurredAt).HasDatabaseName("ix_rental_publication_facts_occurred");
    }
}
