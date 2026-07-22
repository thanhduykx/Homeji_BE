using Homeji.Domain.Enums;
using Homeji.Domain.Exceptions;

namespace Homeji.Domain.Entities;

public sealed class PostMessageAttachment
{
    public const int MaxMimeTypeLength = 100;
    public const int MaxSha256Length = 64;

    private PostMessageAttachment()
    {
        MimeType = null!;
        Sha256 = null!;
        Content = null!;
    }

    public PostMessageAttachment(
        Guid messageId,
        Guid uploaderId,
        MessageAttachmentContext context,
        string mimeType,
        byte[] content,
        int width,
        int height,
        string sha256,
        DateTimeOffset createdAt)
    {
        if (messageId == Guid.Empty || uploaderId == Guid.Empty)
        {
            throw new DomainException("Message id and uploader id must not be empty.");
        }

        if (!Enum.IsDefined(context))
        {
            throw new DomainException("Attachment context is invalid.");
        }

        if (string.IsNullOrWhiteSpace(mimeType) || mimeType.Length > MaxMimeTypeLength)
        {
            throw new DomainException("Attachment MIME type is invalid.");
        }

        if (content is null || content.Length == 0 || width <= 0 || height <= 0)
        {
            throw new DomainException("Attachment image data is invalid.");
        }

        if (string.IsNullOrWhiteSpace(sha256) || sha256.Length != MaxSha256Length)
        {
            throw new DomainException("Attachment hash is invalid.");
        }

        Id = Guid.NewGuid();
        MessageId = messageId;
        UploaderId = uploaderId;
        Context = context;
        MimeType = mimeType;
        Content = content;
        Width = width;
        Height = height;
        Bytes = content.LongLength;
        Sha256 = sha256;
        Status = MessageAttachmentStatus.Ready;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public Guid MessageId { get; private set; }
    public Guid UploaderId { get; private set; }
    public MessageAttachmentContext Context { get; private set; }
    public string MimeType { get; private set; }
    public byte[] Content { get; private set; }
    public int Width { get; private set; }
    public int Height { get; private set; }
    public long Bytes { get; private set; }
    public string Sha256 { get; private set; }
    public MessageAttachmentStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }

    public void Delete(Guid userId, DateTimeOffset deletedAt)
    {
        if (UploaderId != userId)
        {
            throw new DomainException("Only the uploader can delete this attachment.");
        }

        if (Status == MessageAttachmentStatus.Deleted)
        {
            return;
        }

        Status = MessageAttachmentStatus.Deleted;
        Content = [];
        Bytes = 0;
        DeletedAt = deletedAt;
    }
}
