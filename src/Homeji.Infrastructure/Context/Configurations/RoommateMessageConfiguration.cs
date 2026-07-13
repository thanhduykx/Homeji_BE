using Homeji.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Homeji.Infrastructure.Context.Configurations;

public sealed class RoommateMessageConfiguration : IEntityTypeConfiguration<RoommateMessage>
{
    public void Configure(EntityTypeBuilder<RoommateMessage> builder)
    {
        builder.ToTable("roommate_messages", "homeji");
        builder.HasKey(message => message.Id).HasName("pk_roommate_messages");
        builder.Property(message => message.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(message => message.ConversationId).HasColumnName("conversation_id").IsRequired();
        builder.Property(message => message.SenderId).HasColumnName("sender_id").IsRequired();
        builder.Property(message => message.Body).HasColumnName("body").HasMaxLength(RoommateMessage.MaxBodyLength).IsRequired();
        builder.Property(message => message.SentAt).HasColumnName("sent_at").IsRequired();
        builder.HasIndex(message => new { message.ConversationId, message.SentAt })
            .HasDatabaseName("ix_roommate_messages_conversation_sent");
        builder.HasIndex(message => message.SenderId)
            .HasDatabaseName("ix_roommate_messages_sender_id");
        builder.HasOne<RoommateConversation>()
            .WithMany()
            .HasForeignKey(message => message.ConversationId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_roommate_messages_conversations");
        builder.HasOne<UserProfile>()
            .WithMany()
            .HasForeignKey(message => message.SenderId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_roommate_messages_senders");
    }
}
