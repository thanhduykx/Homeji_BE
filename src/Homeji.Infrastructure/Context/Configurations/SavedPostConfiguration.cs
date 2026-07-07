using Homeji.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Homeji.Infrastructure.Context.Configurations;

public sealed class SavedPostConfiguration : IEntityTypeConfiguration<SavedPost>
{
    public void Configure(EntityTypeBuilder<SavedPost> builder)
    {
        builder.ToTable("saved_posts", "homeji");
        builder.HasKey(saved => new { saved.UserId, saved.RentalPostId }).HasName("pk_saved_posts");
        builder.Property(saved => saved.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(saved => saved.RentalPostId).HasColumnName("rental_post_id").IsRequired();
        builder.Property(saved => saved.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.HasIndex(saved => saved.RentalPostId).HasDatabaseName("ix_saved_posts_rental_post_id");
    }
}
