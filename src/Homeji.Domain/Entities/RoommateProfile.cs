using Homeji.Domain.Enums;
using Homeji.Domain.Exceptions;
namespace Homeji.Domain.Entities;
public sealed class RoommateProfile
{
    private RoommateProfile() { }
    public RoommateProfile(Guid userId) { UserId = userId; }
    public Guid UserId { get; private set; }
    public RoommateIntent Intent { get; private set; } = RoommateIntent.SeekingAccommodation;
    public bool IsDiscoverable { get; private set; }
    public string? Introduction { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public void Update(RoommateIntent intent, bool isDiscoverable, string? introduction, DateTimeOffset now)
    {
        if (!Enum.IsDefined(intent)) throw new DomainException("Nhu cầu ở ghép không hợp lệ.");
        var text = introduction?.Trim();
        if (text?.Length > 600) throw new DomainException("Giới thiệu không được vượt quá 600 ký tự.");
        Intent = intent; IsDiscoverable = isDiscoverable;
        Introduction = string.IsNullOrEmpty(text) ? null : text; UpdatedAt = now;
    }
}
