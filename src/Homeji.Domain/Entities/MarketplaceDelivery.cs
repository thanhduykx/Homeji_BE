namespace Homeji.Domain.Entities;

public sealed record MarketplaceDelivery(
    string RecipientName,
    string RecipientPhone,
    string Address,
    decimal Latitude,
    decimal Longitude);
