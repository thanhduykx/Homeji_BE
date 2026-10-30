using Homeji.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Homeji.Infrastructure.Context.Configurations;
public sealed class AiListingReviewConfiguration : IEntityTypeConfiguration<AiListingReview>
{
    public void Configure(EntityTypeBuilder<AiListingReview> builder)
    {
        builder.ToTable("ai_listing_reviews", "homeji"); builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.SubjectId).HasColumnName("subject_id");
        builder.Property(x => x.Kind).HasColumnName("kind").HasMaxLength(12);
        builder.Property(x => x.Title).HasColumnName("title").HasMaxLength(200);
        builder.Property(x => x.ContentHash).HasColumnName("content_hash").HasMaxLength(64);
        builder.Property(x => x.Snapshot).HasColumnName("snapshot").HasColumnType("jsonb");
        builder.Property(x => x.SourceUpdatedAt).HasColumnName("source_updated_at");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.NextAttemptAt).HasColumnName("next_attempt_at");
        builder.Property(x => x.State).HasColumnName("state").HasMaxLength(20);
        builder.Property(x => x.LeaseId).HasColumnName("lease_id").IsConcurrencyToken();
        builder.Property(x => x.LeaseUntil).HasColumnName("lease_until");
        builder.Property(x => x.Attempts).HasColumnName("attempts");
        builder.Property(x => x.Category).HasColumnName("category").HasMaxLength(40);
        builder.Property(x => x.Reason).HasColumnName("reason").HasMaxLength(500);
        builder.Property(x => x.Evidence).HasColumnName("evidence").HasMaxLength(500);
        builder.Property(x => x.Confidence).HasColumnName("confidence");
        builder.Property(x => x.CompletedAt).HasColumnName("completed_at");
        builder.Property(x => x.ReviewedBy).HasColumnName("reviewed_by");
        builder.Property(x => x.ReviewedAt).HasColumnName("reviewed_at");
        builder.Property(x => x.PreviousStatus).HasColumnName("previous_status");
        builder.Property(x => x.BlockedAt).HasColumnName("blocked_at");
        builder.HasIndex(x => new {x.Kind, x.SubjectId, x.ContentHash}).IsUnique();
        builder.HasIndex(x => new {x.State, x.NextAttemptAt});
        builder.HasIndex(x => new {x.Kind, x.SubjectId, x.SourceUpdatedAt});
    }
}
public sealed class AiModerationScheduleConfiguration : IEntityTypeConfiguration<AiModerationSchedule>
{
    public void Configure(EntityTypeBuilder<AiModerationSchedule> builder)
    {
        builder.ToTable("ai_moderation_schedule", "homeji"); builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasMaxLength(40);
        builder.Property(x => x.PauseReason).HasMaxLength(500);
    }
}
