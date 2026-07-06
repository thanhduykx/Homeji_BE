namespace Homeji.Api.Views.Profiles;

public sealed record UserProfileViewModel(
    Guid Id,
    string DisplayName,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
