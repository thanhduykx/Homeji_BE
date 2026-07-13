using Homeji.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Homeji.Infrastructure.Context.Configurations;

public sealed class PostMessageConfiguration : IEntityTypeConfiguration<PostMessage>
{
    public void Configure(EntityTypeBuilder<PostMessage> builder)
    {
        builder.ToTable("post_messages", "homeji");
        builder.HasKey(message => message.Id);
        builder.Property(message => message.Body).HasMaxLength(PostMessage.MaxBodyLength).IsRequired();
        builder.HasIndex(message => new { message.ConversationId, message.SentAt });
        builder.HasOne<PostConversation>()
            .WithMany()
            .HasForeignKey(message => message.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
