using Homeji.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Homeji.Infrastructure.Context.Configurations;

public sealed class RentalWantedPostConfiguration : IEntityTypeConfiguration<RentalWantedPost>
{
    public void Configure(EntityTypeBuilder<RentalWantedPost> builder)
    {
        builder.ToTable("rental_wanted_posts", "homeji");
        builder.HasKey(post => post.Id);
        builder.Property(post => post.Status).HasConversion<int>();
        builder.Property(post => post.Title).HasMaxLength(RentalWantedPost.MaxTitleLength).IsRequired();
        builder.Property(post => post.Description).HasMaxLength(RentalWantedPost.MaxDescriptionLength).IsRequired();
        builder.Property(post => post.PreferredArea).HasMaxLength(RentalWantedPost.MaxPreferredAreaLength).IsRequired();
        builder.Property(post => post.MaxBudget).HasPrecision(18, 2).IsRequired();
        builder.Property(post => post.AmenityCodes).HasColumnType("text[]").IsRequired();
        builder.HasIndex(post => new { post.Status, post.PreferredArea, post.MaxBudget });
        builder.HasIndex(post => new { post.RequesterId, post.CreatedAt });
        builder.HasOne<UserProfile>()
            .WithMany()
            .HasForeignKey(post => post.RequesterId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
