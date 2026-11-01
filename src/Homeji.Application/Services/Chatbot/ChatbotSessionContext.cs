using Homeji.Application.DTOs.Chatbot;
using Homeji.Domain.Enums;
using Homeji.Application.Services.AI;

namespace Homeji.Application.Services.Chatbot;

public static class ChatbotSessionContext
{
    private static readonly string[] PostingPhrases = ["dang bai", "dang ban", "dang tin", "post len", "tu post", "tu dang", "ban giup", "pass mon", "pass do", "len bai", "tao bai", "rao ban", "dang len"];
    public static IReadOnlyList<ChatbotMessageDto> Bound(IEnumerable<ChatbotMessageDto>? messages)
        => (messages ?? []).TakeLast(6).Where(row => row is not null && row.Sender is ChatMessageSender.User or ChatMessageSender.Assistant)
            .Select(row => row with { Content = (row.Content ?? "")[..Math.Min(row.Content?.Length ?? 0, 1000)] }).ToArray();

    public static bool RequestsPosting(string message) => PostingPhrases.Any(RentalSearchIntent.Normalize(message).Contains);
}
