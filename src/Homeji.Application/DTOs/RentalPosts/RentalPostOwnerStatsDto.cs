using Homeji.Domain.Enums;

namespace Homeji.Application.DTOs.RentalPosts;

public sealed record RentalPostOwnerStatsItemDto(
    Guid Id,
    string Title,
    RentalPostStatus Status,
    int ViewCount,
    int SaveCount,
    DateTimeOffset UpdatedAt);

public sealed record RentalPostOwnerStatsDto(
    int TotalPosts,
    int TotalViews,
    int TotalSaves,
    IReadOnlyList<RentalPostOwnerStatsItemDto> Posts);
