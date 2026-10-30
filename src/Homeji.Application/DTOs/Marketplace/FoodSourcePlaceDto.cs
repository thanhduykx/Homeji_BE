namespace Homeji.Application.DTOs.Marketplace;

public sealed record FoodSourcePlaceDto(Guid Id, string Name, string Address, string SourceUrl,
    string MapsSearchUrl, string VerificationNote, DateTimeOffset CollectedAt)
{
    public decimal? Latitude { get; init; }
    public decimal? Longitude { get; init; }
}
