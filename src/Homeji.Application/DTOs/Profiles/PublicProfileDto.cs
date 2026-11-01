namespace Homeji.Application.DTOs.Profiles;

// Only the explicit social contacts opted in by active Premium members are public.
public sealed record PublicProfileDto(Guid Id, string DisplayName, string? AvatarPath,
    DateTimeOffset CreatedAt, bool IsPremium)
{
    public string? FacebookUrl { get; init; }
    public string? ZaloPhone { get; init; }
}
