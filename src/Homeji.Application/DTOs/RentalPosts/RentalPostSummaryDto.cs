using Homeji.Domain.Enums;

namespace Homeji.Application.DTOs.RentalPosts;

public sealed record RentalPostSummaryDto(
    Guid Id,
    RentalPostType Type,
    string Title,
    decimal Price,
    decimal Area,
    string Address,
    decimal Latitude,
    decimal Longitude,
    string? ThumbnailPath,
    int ViewCount,
    int SaveCount,
    bool IsOwnerPremium,
    string? OwnerBadge,
    decimal BoostScore,
    string? HighlightTag,
    RoomTransferKind? TransferKind = null,
    DateOnly? OriginalLeaseEndsOn = null,
    decimal PassFee = 0,
    bool OwnerConsentVerified = false,
    string? OwnerConsentContact = null,
    bool IsSynthetic = false,
    Guid? OwnerId = null,
    UserRole? OwnerRole = null,
    string? OwnerDisplayName = null,
    string? OwnerAvatarPath = null,
    string? OwnerSchool = null,
    int AvailableSlots = 1,
    int MaxOccupants = 1,
    string? DescriptionExcerpt = null);
