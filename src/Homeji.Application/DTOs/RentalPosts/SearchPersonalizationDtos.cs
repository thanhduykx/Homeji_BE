using System.ComponentModel.DataAnnotations;
namespace Homeji.Application.DTOs.RentalPosts;

public sealed record InterestCriteria(
    [Required, StringLength(160, MinimumLength = 2)] string Area,
    [Range(0, 1000000000)] decimal? MinPrice = null,
    [Range(0, 1000000000)] decimal? MaxPrice = null,
    [Range(0, 10000)] decimal? MinArea = null,
    [Range(0, 10000)] decimal? MaxArea = null,
    string[]? Amenities = null, string[]? ExcludedAmenities = null,
    [Range(-90, 90)] double? Latitude = null,
    [Range(-180, 180)] double? Longitude = null,
    [Range(.5, 30)] double? RadiusKm = null,
    [Range(1, 20)] int? Occupants = null,
    [Range(0, 1000000000)] decimal? MaxDeposit = null,
    bool ExcludeRoommateShare = false);
public sealed record InterestSettings(bool Enabled, bool AiEnabled = false, bool EmailEnabled = false);
public sealed record ObserveInterest(Guid SessionId, InterestCriteria Criteria);
public sealed record InterestProposal(string Key, InterestCriteria Criteria, int Sessions);
public sealed record FollowedInterest(Guid Id, InterestCriteria Criteria, DateTimeOffset CreatedAt);
public sealed record InterestRecommendation(RentalPostSummaryDto Post, string Reason, Guid FollowId);
public sealed record InterestOverview(InterestSettings Settings, IReadOnlyList<InterestProposal> Suggestions,
    IReadOnlyList<FollowedInterest> Following, IReadOnlyList<InterestRecommendation> Rooms, string? Notice = null)
{
    public int MaxFollowedAreas { get; init; } = 10;
}
public sealed record AiInterestProposal(InterestCriteria? Criteria, string[] Unconfirmed, string? Notice);
