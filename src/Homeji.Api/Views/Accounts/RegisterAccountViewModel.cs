namespace Homeji.Api.Views.Accounts;

public sealed record RegisterAccountViewModel(
    string? Email,
    string? Password,
    string? DisplayName,
    string? RedirectTo);
