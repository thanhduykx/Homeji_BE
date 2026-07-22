using Homeji.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Homeji.Infrastructure.Context.Configurations;

public sealed class PostMessageAttachmentConfiguration : IEntityTypeConfiguration<PostMessageAttachment>
{
    public void Configure(EntityTypeBuilder<PostMessageAttachment> builder)
    {
        builder.ToTable("post_message_attachments", "homeji");
        builder.HasKey(attachment => attachment.Id);
        builder.Property(attachment => attachment.Context).HasConversion<int>();
        builder.Property(attachment => attachment.Status).HasConversion<int>();
        builder.Property(attachment => attachment.MimeType)
            .HasMaxLength(PostMessageAttachment.MaxMimeTypeLength)
            .IsRequired();
        builder.Property(attachment => attachment.Sha256)
            .HasMaxLength(PostMessageAttachment.MaxSha256Length)
            .IsFixedLength()
            .IsRequired();
        builder.Property(attachment => attachment.Content).IsRequired();
        builder.HasIndex(attachment => new { attachment.MessageId, attachment.CreatedAt });
        builder.HasIndex(attachment => new { attachment.UploaderId, attachment.CreatedAt });
        builder.HasOne<PostMessage>()
            .WithMany(message => message.Attachments)
            .HasForeignKey(attachment => attachment.MessageId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
