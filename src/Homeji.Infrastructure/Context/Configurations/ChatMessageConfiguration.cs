using Homeji.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Homeji.Infrastructure.Context.Configurations;

public sealed class ChatMessageConfiguration : IEntityTypeConfiguration<ChatMessage>
{
    public void Configure(EntityTypeBuilder<ChatMessage> builder)
    {
        builder.ToTable("chat_messages", "homeji");

        builder.HasKey(message => message.Id)
            .HasName("pk_chat_messages");

        builder.Property(message => message.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(message => message.ConversationId)
            .HasColumnName("conversation_id")
            .IsRequired();

        builder.Property(message => message.Sender)
            .HasColumnName("sender")
            .HasConversion<int>()
            .IsRequired();

        builder.Property(message => message.Content)
            .HasColumnName("content")
            .HasMaxLength(ChatMessage.MaxContentLength)
            .IsRequired();

        builder.Property(message => message.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.HasIndex(message => new { message.ConversationId, message.CreatedAt })
            .HasDatabaseName("ix_chat_messages_conversation_created_at");
    }
}
