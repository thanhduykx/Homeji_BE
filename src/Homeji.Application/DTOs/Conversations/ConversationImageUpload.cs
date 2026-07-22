using Homeji.Domain.Enums;

namespace Homeji.Application.DTOs.Conversations;

public sealed record ConversationImageUpload(
    string FileName,
    string ContentType,
    byte[] Content,
    MessageAttachmentContext Context);

public sealed record ProcessedConversationImage(
    string MimeType,
    byte[] Content,
    int Width,
    int Height,
    string Sha256);

public sealed record PostMessageAttachmentContentDto(
    string MimeType,
    byte[] Content,
    string Sha256);
