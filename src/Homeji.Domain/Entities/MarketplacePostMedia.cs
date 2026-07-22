using Homeji.Domain.Exceptions;

namespace Homeji.Domain.Entities;

public sealed class MarketplacePostMedia
{
    public const int MaxUrlLength = 1_000;

    private MarketplacePostMedia()
    {
        Url = null!;
    }

    internal MarketplacePostMedia(Guid marketplacePostId, string url, int sortOrder)
    {
        var normalizedUrl = url?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedUrl)
            || !Uri.TryCreate(normalizedUrl, UriKind.Absolute, out var parsedUrl)
            || parsedUrl.Scheme is not ("http" or "https"))
        {
            throw new DomainException("URL ảnh chợ đồ phải là HTTP/HTTPS tuyệt đối.");
        }

        if (normalizedUrl.Length > MaxUrlLength)
        {
            throw new DomainException($"URL ảnh chợ đồ không được vượt quá {MaxUrlLength} ký tự.");
        }

        if (sortOrder < 0)
        {
            throw new DomainException("Thứ tự ảnh chợ đồ không được âm.");
        }

        Id = Guid.NewGuid();
        MarketplacePostId = marketplacePostId;
        Url = normalizedUrl;
        SortOrder = sortOrder;
    }

    public Guid Id { get; private set; }

    public Guid MarketplacePostId { get; private set; }

    public string Url { get; private set; }

    public int SortOrder { get; private set; }
}
