namespace Homeji.Domain.Entities;

public sealed class PremiumPriceChange
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RequestId { get; set; }
    public Guid AdminId { get; set; }
    public string PackageCode { get; set; } = string.Empty;
    public string PackageName { get; set; } = string.Empty;
    public decimal PreviousPrice { get; set; }
    public decimal Price { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}
