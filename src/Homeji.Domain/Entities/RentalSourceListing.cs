using Homeji.Domain.Exceptions;

namespace Homeji.Domain.Entities;

// An external advertisement is evidence from a source, not a Homeji landlord's post.
public sealed class RentalSourceListing
{
    private RentalSourceListing() { }

    public Guid Id { get; private set; }
    public string Source { get; private set; } = string.Empty;
    public string SourceId { get; private set; } = string.Empty;
    public string SourceUrl { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public string Address { get; private set; } = string.Empty;
    public string District { get; private set; } = string.Empty;
    public decimal Price { get; private set; }
    public decimal Area { get; private set; }
    public string[] ImageUrls { get; private set; } = [];
    public DateTimeOffset? SourceUpdatedAt { get; private set; }
    public DateTimeOffset CollectedAt { get; private set; }

    public static RentalSourceListing Create(
        string sourceId, string sourceUrl, string title, string address, string district,
        decimal price, decimal area, IEnumerable<string> imageUrls,
        DateTimeOffset? sourceUpdatedAt, DateTimeOffset collectedAt)
    {
        var images = imageUrls.Distinct(StringComparer.Ordinal).ToArray();
        if (string.IsNullOrWhiteSpace(sourceId) || sourceId.Length > 80
            || sourceId.Any(character => !char.IsAsciiDigit(character))
            || !IsHttpsUrl(sourceUrl, "phongtro123.com") || sourceUrl.Length > 1000
            || string.IsNullOrWhiteSpace(title) || title.Length > 200
            || string.IsNullOrWhiteSpace(address) || address.Length > 500
            || district is not ("quan-9" or "thu-duc")
            || price is <= 0 or > 100_000_000 || area is <= 0 or > 1000
            || images.Length is < 1 or > 10
            || images.Any(url => url.Length > 1000 || !IsHttpsUrl(url, "pt123.cdn.static123.com")))
        {
            throw new DomainException("Tin nguồn hoặc ảnh không hợp lệ; chỉ hỗ trợ Quận 9 và Thủ Đức.");
        }

        return new RentalSourceListing
        {
            Id = Guid.NewGuid(), Source = "phongtro123", SourceId = sourceId,
            SourceUrl = sourceUrl, Title = title.Trim(), Address = address.Trim(), District = district,
            Price = price, Area = area, ImageUrls = images,
            SourceUpdatedAt = sourceUpdatedAt, CollectedAt = collectedAt,
        };
    }

    private static bool IsHttpsUrl(string value, string host) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps && uri.Host.Equals(host, StringComparison.OrdinalIgnoreCase)
        && uri.IsDefaultPort && string.IsNullOrEmpty(uri.UserInfo);
}
