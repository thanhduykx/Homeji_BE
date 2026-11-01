namespace Homeji.Application.DTOs.Chatbot;

public sealed record ChatbotAttachmentDto(string? MimeType, string? Data);
public sealed record ChatbotMediaContent(string MimeType, byte[] Content);
public sealed record ChatbotTranscriptionDto(ChatbotAttachmentDto? Audio);
