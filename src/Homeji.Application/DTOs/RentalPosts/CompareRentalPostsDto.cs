namespace Homeji.Application.DTOs.RentalPosts;

public sealed record CompareRentalPostsDto(IReadOnlyCollection<Guid> PostIds);

public sealed record RentalPostComparisonDto(IReadOnlyList<RentalPostComparisonItemDto> Posts);

public sealed record RentalPostComparisonItemDto(
    RentalPostDto Post,
    decimal AverageRating,
    int ReviewCount);
