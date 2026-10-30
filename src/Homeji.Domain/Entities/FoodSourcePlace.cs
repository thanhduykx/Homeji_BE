namespace Homeji.Domain.Entities;

// A sourced directory entry has no seller, stock, invented price or ordering flow.
public sealed class FoodSourcePlace
{
    public Guid Id { get; private set; }
    public decimal? Latitude { get; private set; }
    public decimal? Longitude { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Address { get; private set; } = string.Empty;
    public string SourceUrl { get; private set; } = string.Empty;
    public string MapsSearchUrl { get; private set; } = string.Empty;
    public string VerificationNote { get; private set; } = string.Empty;
    public DateTimeOffset CollectedAt { get; private set; }
}
