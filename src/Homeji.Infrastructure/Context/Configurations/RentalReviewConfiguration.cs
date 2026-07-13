using Homeji.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Homeji.Infrastructure.Context.Configurations;

public sealed class RentalReviewConfiguration : IEntityTypeConfiguration<RentalReview>
{
    public void Configure(EntityTypeBuilder<RentalReview> builder)
    {
        builder.ToTable("rental_reviews", "homeji");
        builder.HasKey(review => review.Id).HasName("pk_rental_reviews");
        builder.Property(review => review.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(review => review.RentalPostId).HasColumnName("rental_post_id").IsRequired();
        builder.Property(review => review.ReviewerId).HasColumnName("reviewer_id").IsRequired();
        builder.Property(review => review.Rating).HasColumnName("rating").IsRequired();
        builder.Property(review => review.Comment).HasColumnName("comment").HasMaxLength(RentalReview.MaxCommentLength);
        builder.Property(review => review.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(review => review.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.HasIndex(review => new { review.RentalPostId, review.UpdatedAt })
            .HasDatabaseName("ix_rental_reviews_post_updated");
        builder.HasIndex(review => new { review.RentalPostId, review.ReviewerId })
            .IsUnique()
            .HasDatabaseName("ux_rental_reviews_post_reviewer");
        builder.HasIndex(review => review.ReviewerId)
            .HasDatabaseName("ix_rental_reviews_reviewer_id");
        builder.HasOne<RentalPost>()
            .WithMany()
            .HasForeignKey(review => review.RentalPostId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_rental_reviews_rental_posts");
        builder.HasOne<UserProfile>()
            .WithMany()
            .HasForeignKey(review => review.ReviewerId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_rental_reviews_user_profiles");
        builder.ToTable(table => table.HasCheckConstraint(
            "ck_rental_reviews_rating",
            "rating >= 1 AND rating <= 5"));
    }
}
