using Homeji.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Homeji.Infrastructure.Context.Configurations;

public sealed class PostConversationConfiguration : IEntityTypeConfiguration<PostConversation>
{
    public void Configure(EntityTypeBuilder<PostConversation> builder)
    {
        builder.ToTable("post_conversations", "homeji");
        builder.HasKey(conversation => conversation.Id);
        builder.Property(conversation => conversation.SubjectType).HasConversion<int>();
        builder.HasIndex(conversation => new
        {
            conversation.SubjectType,
            conversation.SubjectId,
            conversation.ParticipantAId,
            conversation.ParticipantBId,
        }).IsUnique();
        builder.HasIndex(conversation => new { conversation.ParticipantAId, conversation.UpdatedAt });
        builder.HasIndex(conversation => new { conversation.ParticipantBId, conversation.UpdatedAt });
    }
}
