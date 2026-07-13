using Homeji.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Homeji.Infrastructure.Context.Configurations;

public sealed class UserActivityConfiguration : IEntityTypeConfiguration<UserActivity>
{
    public void Configure(EntityTypeBuilder<UserActivity> builder)
    {
        builder.ToTable("user_activities", "homeji");
        builder.HasKey(activity => activity.Id);
        builder.Property(activity => activity.Action).HasMaxLength(UserActivity.MaxActionLength).IsRequired();
        builder.Property(activity => activity.ResourcePath).HasMaxLength(UserActivity.MaxPathLength).IsRequired();
        builder.Property(activity => activity.HttpMethod).HasMaxLength(UserActivity.MaxMethodLength).IsRequired();
        builder.HasIndex(activity => new { activity.UserId, activity.OccurredAt });
        builder.HasOne<UserProfile>()
            .WithMany()
            .HasForeignKey(activity => activity.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
