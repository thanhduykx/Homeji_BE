using Homeji.Domain.Enums;

namespace Homeji.Application.DTOs.RentalPosts;

public sealed record AddRentalPostMediaDto(
    MediaType MediaType,
    string? Bucket,
    string? Path,
    bool IsThumbnail,
    int SortOrder);
