namespace Homeji.Application.DTOs.AI;

public sealed record AiHighlightRequestDto(
    string? Text,
    int MaxResults = 5);
