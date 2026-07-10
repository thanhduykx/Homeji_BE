using Homeji.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Homeji.Infrastructure.Context.Configurations;

public sealed class ChatConversationConfiguration : IEntityTypeConfiguration<ChatConversation>
{
    public void Configure(EntityTypeBuilder<ChatConversation> builder)
    {
        builder.ToTable("chat_conversations", "homeji");

        builder.HasKey(conversation => conversation.Id)
            .HasName("pk_chat_conversations");

        builder.Property(conversation => conversation.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(conversation => conversation.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(conversation => conversation.Title)
            .HasColumnName("title")
            .HasMaxLength(ChatConversation.MaxTitleLength)
            .IsRequired();

        builder.Property(conversation => conversation.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(conversation => conversation.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.HasMany(conversation => conversation.Messages)
            .WithOne()
            .HasForeignKey(message => message.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata
            .FindNavigation(nameof(ChatConversation.Messages))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(conversation => new { conversation.UserId, conversation.UpdatedAt })
            .HasDatabaseName("ix_chat_conversations_user_updated_at");
    }
}
