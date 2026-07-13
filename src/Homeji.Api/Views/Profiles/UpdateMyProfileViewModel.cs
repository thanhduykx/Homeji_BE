namespace Homeji.Api.Views.Profiles;

public sealed record UpdateMyProfileViewModel(
    string? DisplayName,
    string? Phone,
    string? AvatarPath,
    string? School,
    string? PreferredArea,
    string? ContactAddress,
    string? RentalNeed);
