using Homeji.Domain.Enums;

namespace Homeji.Application.DTOs.WantedPosts;

public sealed record UpsertRentalWantedPostDto(
    string? Title,
    string? Description,
    string? PreferredArea,
    decimal MaxBudget,
    int OccupantCount,
    IReadOnlyCollection<string> AmenityCodes,
    DateOnly DesiredMoveInDate);

public sealed record RentalWantedPostDto(
    Guid Id,
    Guid RequesterId,
    string RequesterDisplayName,
    string? RequesterAvatarPath,
    WantedPostStatus Status,
    string Title,
    string Description,
    string PreferredArea,
    decimal MaxBudget,
    int OccupantCount,
    IReadOnlyCollection<string> AmenityCodes,
    DateOnly DesiredMoveInDate,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
