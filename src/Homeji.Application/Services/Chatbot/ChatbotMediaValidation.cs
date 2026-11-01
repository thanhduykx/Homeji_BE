using Homeji.Application.Common.Exceptions;
using Homeji.Application.DTOs.Chatbot;
using Homeji.Application.Services.Conversations;

namespace Homeji.Application.Services.Chatbot;

public static class ChatbotMediaValidation
{
    public const int MaxTotalBytes = 8 * 1024 * 1024;
    public const int MaxImageBytes = 4 * 1024 * 1024;

    public static IReadOnlyList<ChatbotMediaContent> Decode(IReadOnlyCollection<ChatbotAttachmentDto>? attachments)
    {
        if (attachments is null || attachments.Count == 0) return [];
        if (attachments.Count > 4) throw Invalid();
        var result = new List<ChatbotMediaContent>();
        var total = 0;
        var images = 0;
        var audio = 0;
        foreach (var attachment in attachments)
        {
            if (attachment is null || string.IsNullOrEmpty(attachment.Data)
                || attachment.Data.Length > (MaxTotalBytes + 2) / 3 * 4) throw Invalid();
            var mime = attachment.MimeType;
            if (mime is not ("image/jpeg" or "image/png" or "image/webp" or "audio/wav")) throw Invalid();
            byte[] content;
            try { content = Convert.FromBase64String(attachment.Data); }
            catch (FormatException) { throw Invalid(); }
            total += content.Length;
            if (content.Length == 0 || total > MaxTotalBytes) throw Invalid();
            if (mime == "audio/wav")
            {
                if (++audio > 1) throw Invalid();
                ConversationVoiceFormat.Validate(content);
            }
            else if (++images > 3 || content.Length > MaxImageBytes) throw Invalid();
            result.Add(new(mime, content));
        }
        return result;
    }

    public static RequestValidationException Invalid() => new(new Dictionary<string, string[]>
    {
        ["attachments"] = ["Tối đa 3 ảnh JPEG/PNG/WebP (4 MB/ảnh) và 1 bản ghi WAV 2 phút; tổng không quá 8 MB."],
    });
}
