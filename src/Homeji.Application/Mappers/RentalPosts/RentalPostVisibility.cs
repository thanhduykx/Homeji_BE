using Homeji.Application.DTOs.RentalPosts;
using Homeji.Domain.Enums;

namespace Homeji.Application.Mappers.RentalPosts;

/// <summary>Keep comparison, detail and AI cards within the same location disclosure boundary.</summary>
public static class RentalPostVisibility
{
    public static RentalPostDto ForViewer(RentalPostDto dto, Guid? currentUserId)
    {
        if (currentUserId != dto.OwnerId)
        {
            dto = dto with { OwnerConsentContact = null };
            if (dto.Type == RentalPostType.RoomTransfer)
                dto = dto with
                {
                    Address = ApproximateTransferAddress(dto.Address),
                    Latitude = Math.Round(dto.Latitude, 3),
                    Longitude = Math.Round(dto.Longitude, 3),
                };
        }
        return currentUserId is null ? dto with { ModerationReason = null } : dto;
    }

    public static RentalPostSummaryDto ForPublicSearch(RentalPostSummaryDto dto) =>
        dto.Type == RentalPostType.RoomTransfer ? dto with
        {
            Address = ApproximateTransferAddress(dto.Address),
            Latitude = Math.Round(dto.Latitude, 3),
            Longitude = Math.Round(dto.Longitude, 3),
            OwnerConsentContact = null,
        } : dto;

    private static string ApproximateTransferAddress(string address)
    {
        var segments = address.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return segments.Length < 2 ? "Khu vực " + address : "Khu vực " + string.Join(", ", segments.TakeLast(Math.Min(2, segments.Length)));
    }
}
