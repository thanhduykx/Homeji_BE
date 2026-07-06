namespace Homeji.Application.DTOs.SavedPosts;

public sealed record RoommateCandidateDto(
    Guid UserId,
    string DisplayName,
    string? School,
    string? PreferredArea,
    int MatchScore);
