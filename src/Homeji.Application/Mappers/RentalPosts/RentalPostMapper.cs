using Homeji.Application.DTOs.RentalPosts;
using Homeji.Domain.Entities;
using Homeji.Domain.Enums;

namespace Homeji.Application.Mappers.RentalPosts;

public static class RentalPostMapper
{
    public static RentalPostDto ToDto(RentalPost post)
    {
        return new RentalPostDto(
            post.Id,
            post.OwnerId,
            post.Type,
            post.Status,
            post.Title,
            post.Description,
            post.Price,
            post.Deposit,
            post.Area,
            post.Address,
            post.Latitude,
            post.Longitude,
            post.Amenities.Select(amenity => amenity.Code).OrderBy(code => code, StringComparer.Ordinal).ToArray(),
            post.Media.OrderBy(media => media.SortOrder).Select(ToDto).ToArray(),
            post.ViewCount,
            post.SaveCount,
            post.ElectricityPrice,
            post.WaterPrice,
            post.InternetPrice,
            post.MaxOccupants,
            post.AvailableSlots,
            post.HouseRules,
            post.AvailableFrom,
            post.ModerationReason,
            post.CreatedAt,
            post.UpdatedAt);
    }

    public static RentalPostSummaryDto ToSummaryDto(
        RentalPost post,
        bool isOwnerPremium = false,
        decimal boostScore = 0,
        string? highlightTag = null)
    {
        return new RentalPostSummaryDto(
            post.Id,
            post.Type,
            post.Title,
            post.Price,
            post.Area,
            post.Address,
            post.Latitude,
            post.Longitude,
            post.Media
                .OrderByDescending(media => media.IsThumbnail)
                .ThenBy(media => media.SortOrder)
                .FirstOrDefault(media => media.MediaType == MediaType.Image)?.Path,
            post.ViewCount,
            post.SaveCount,
            isOwnerPremium,
            isOwnerPremium ? "Premium" : null,
            boostScore,
            highlightTag);
    }

    private static RentalPostMediaDto ToDto(RentalPostMedia media)
    {
        return new RentalPostMediaDto(
            media.Id,
            media.MediaType,
            media.Bucket,
            media.Path,
            media.IsThumbnail,
            media.SortOrder,
            media.CreatedAt);
    }
}
