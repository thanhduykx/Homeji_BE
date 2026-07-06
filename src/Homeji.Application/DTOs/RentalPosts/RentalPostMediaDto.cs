using Homeji.Domain.Enums;

namespace Homeji.Application.DTOs.RentalPosts;

public sealed record RentalPostMediaDto(
    Guid Id,
    MediaType MediaType,
    string Bucket,
    string Path,
    bool IsThumbnail,
    int SortOrder,
    DateTimeOffset CreatedAt);
