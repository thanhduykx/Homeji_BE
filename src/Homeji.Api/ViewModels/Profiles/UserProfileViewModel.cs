namespace Homeji.Api.ViewModels.Profiles;

public sealed record UserProfileViewModel(
    Guid Id,
    string DisplayName,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
