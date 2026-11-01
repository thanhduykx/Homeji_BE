using Homeji.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Homeji.Infrastructure.Context.Configurations;

public sealed class AgentPostDraftConfiguration : IEntityTypeConfiguration<AgentPostDraft>
{
    public void Configure(EntityTypeBuilder<AgentPostDraft> builder)
    {
        builder.ToTable("agent_post_drafts", "homeji");
        builder.HasKey(row => row.Id);
        builder.Property(row => row.Payload).HasColumnType("jsonb");
        builder.Property(row => row.ConfirmedDetails).HasMaxLength(10000);
        builder.HasIndex(row => new { row.UserId, row.CreatedAt });
        builder.HasIndex(row => row.ExpiresAt);
        builder.HasOne<UserProfile>().WithMany().HasForeignKey(row => row.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
