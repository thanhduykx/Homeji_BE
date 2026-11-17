using System.Text.Json;

namespace Homeji.Infrastructure.Messaging;

internal sealed record RentalPublicationEnvelope(Guid EventId, string EventType, int SchemaVersion, DateTimeOffset OccurredAt, Guid ListingId)
{
    public const string PublishedEventType = "RentalListingPublished";
    public const int Version = 1;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public string Serialize() => JsonSerializer.Serialize(this, Json);

    public static RentalPublicationEnvelope? Read(string? key, string? payload)
    {
        if (!Guid.TryParse(key, out var listingId) || string.IsNullOrWhiteSpace(payload) || payload.Length > 4096)
        {
            return null;
        }

        try
        {
            var envelope = JsonSerializer.Deserialize<RentalPublicationEnvelope>(payload, Json);
            return envelope is { EventType: PublishedEventType, SchemaVersion: Version } &&
                envelope.EventId != Guid.Empty && envelope.ListingId != Guid.Empty && envelope.ListingId == listingId &&
                envelope.OccurredAt != default ? envelope : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
