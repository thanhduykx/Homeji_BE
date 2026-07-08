using System.Net.Http.Json;
using System.Text.Json;
using Homeji.Application.Common.Exceptions;
using Homeji.Application.DTOs.Accounts;
using Homeji.Application.DTOs.Emails;
using Homeji.Application.IServices.Accounts;
using Homeji.Application.IServices.Emails;
using Microsoft.Extensions.Options;

namespace Homeji.Infrastructure.External;

public sealed class SupabaseAccountService : IAccountService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IAccountEmailSender _accountEmailSender;
    private readonly HttpClient _httpClient;
    private readonly SupaBaseAuthOptions _options;

    public SupabaseAccountService(
        HttpClient httpClient,
        IOptions<SupaBaseAuthOptions> options,
        IAccountEmailSender accountEmailSender)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _accountEmailSender = accountEmailSender;
    }

    public async Task<AuthSessionDto> RegisterAsync(
        RegisterAccountDto request,
        CancellationToken cancellationToken = default)
    {
        ValidateEmailAndPassword(request.Email, request.Password);
        EnsureConfigured();

        var redirectTo = ResolveRedirectUrl(request.RedirectTo, null);
        var endpoint = BuildAuthUri("signup", redirectTo);
        using var httpRequest = CreateJsonRequest(
            HttpMethod.Post,
            endpoint,
            new
            {
                email = request.Email,
                password = request.Password,
                data = new
                {
                    display_name = request.DisplayName,
                },
            });

        var json = await SendAsync(httpRequest, cancellationToken);
        var authSession = ParseAuthSession(json, emailConfirmationMessage: "Registration succeeded.");
        var emailResult = await _accountEmailSender.SendRegistrationConfirmationAsync(
            request.Email!,
            request.DisplayName,
            cancellationToken);

        return authSession with
        {
            Message = BuildRegistrationMessage(authSession, emailResult),
        };
    }

    public async Task<AuthSessionDto> LoginAsync(
        LoginAccountDto request,
        CancellationToken cancellationToken = default)
    {
        ValidateEmailAndPassword(request.Email, request.Password);
        EnsureConfigured();

        using var httpRequest = CreateJsonRequest(
            HttpMethod.Post,
            new Uri("auth/v1/token?grant_type=password", UriKind.Relative),
            new
            {
                email = request.Email,
                password = request.Password,
            });

        var json = await SendAsync(httpRequest, cancellationToken);
        return ParseAuthSession(json, emailConfirmationMessage: "Login succeeded.");
    }

    public async Task<AccountMessageDto> ForgotPasswordAsync(
        ForgotPasswordDto request,
        CancellationToken cancellationToken = default)
    {
        ValidateEmail(request.Email);
        EnsureConfigured();

        var redirectTo = ResolveRedirectUrl(request.RedirectTo, _options.PasswordResetRedirectUrl);
        var endpoint = BuildAuthUri("recover", redirectTo);
        using var httpRequest = CreateJsonRequest(HttpMethod.Post, endpoint, new { email = request.Email });
        _ = await SendAsync(httpRequest, cancellationToken);
        return new AccountMessageDto("Password recovery email has been requested.");
    }

    public async Task<AccountMessageDto> ResetPasswordAsync(
        ResetPasswordDto request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.AccessToken))
        {
            throw Validation("accessToken", "Access token is required.");
        }

        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 6)
        {
            throw Validation("newPassword", "New password must contain at least 6 characters.");
        }

        EnsureConfigured();

        using var httpRequest = CreateJsonRequest(HttpMethod.Put, new Uri("auth/v1/user", UriKind.Relative), new { password = request.NewPassword });
        httpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", request.AccessToken);
        _ = await SendAsync(httpRequest, cancellationToken, addBearerApiKey: false);
        return new AccountMessageDto("Password has been updated.");
    }

    public AuthUrlDto CreateGoogleLoginUrl(string? redirectTo)
    {
        EnsureConfigured();
        var callbackUrl = ResolveRedirectUrl(redirectTo, _options.GoogleRedirectUrl);
        var query = string.IsNullOrWhiteSpace(callbackUrl)
            ? "provider=google"
            : $"provider=google&redirect_to={Uri.EscapeDataString(callbackUrl)}";

        return new AuthUrlDto($"{_options.ProjectUrl.TrimEnd('/')}/auth/v1/authorize?{query}");
    }

    private HttpRequestMessage CreateJsonRequest(HttpMethod method, Uri relativeUri, object payload)
    {
        var request = new HttpRequestMessage(method, relativeUri)
        {
            Content = JsonContent.Create(payload, options: JsonOptions),
        };

        request.Headers.Add("apikey", _options.ApiKey);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _options.ApiKey);
        return request;
    }

    private async Task<JsonDocument> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken,
        bool addBearerApiKey = true)
    {
        if (addBearerApiKey && request.Headers.Authorization is null)
        {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _options.ApiKey);
        }

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw Validation("supabase", ExtractErrorMessage(content));
        }

        return JsonDocument.Parse(string.IsNullOrWhiteSpace(content) ? "{}" : content);
    }

    private static AuthSessionDto ParseAuthSession(JsonDocument document, string emailConfirmationMessage)
    {
        var root = document.RootElement;
        var accessToken = GetString(root, "access_token");
        var tokenType = GetString(root, "token_type");
        var expiresIn = GetInt(root, "expires_in");
        var refreshToken = GetString(root, "refresh_token");
        var user = root.TryGetProperty("user", out var userElement) ? userElement : root;
        var userIdText = GetString(user, "id");
        var email = GetString(user, "email");

        return new AuthSessionDto(
            accessToken,
            tokenType,
            expiresIn,
            refreshToken,
            Guid.TryParse(userIdText, out var userId) ? userId : null,
            email,
            string.IsNullOrWhiteSpace(accessToken),
            emailConfirmationMessage);
    }

    private static Uri BuildAuthUri(string path, string? redirectTo)
    {
        var relative = $"auth/v1/{path}";
        if (!string.IsNullOrWhiteSpace(redirectTo))
        {
            relative += $"?redirect_to={Uri.EscapeDataString(redirectTo)}";
        }

        return new Uri(relative, UriKind.Relative);
    }

    private static string BuildRegistrationMessage(AuthSessionDto authSession, EmailSendResultDto emailResult)
    {
        var authMessage = authSession.EmailConfirmationRequired
            ? "Registration succeeded. Email confirmation may be required before login."
            : "Registration succeeded.";

        return $"{authMessage} {emailResult.Message}";
    }

    private void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(_options.ProjectUrl) || string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new InvalidOperationException("Supabase ProjectUrl and ApiKey must be configured.");
        }

        if (_httpClient.BaseAddress is null)
        {
            _httpClient.BaseAddress = new Uri(_options.ProjectUrl.TrimEnd('/') + "/");
        }
    }

    private static string? ResolveRedirectUrl(string? requestRedirectTo, string? configuredRedirectTo)
    {
        return !string.IsNullOrWhiteSpace(requestRedirectTo)
            ? requestRedirectTo
            : configuredRedirectTo;
    }

    private static void ValidateEmailAndPassword(string? email, string? password)
    {
        ValidateEmail(email);
        if (string.IsNullOrWhiteSpace(password) || password.Length < 6)
        {
            throw Validation("password", "Password must contain at least 6 characters.");
        }
    }

    private static void ValidateEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@', StringComparison.Ordinal))
        {
            throw Validation("email", "A valid email is required.");
        }
    }

    private static RequestValidationException Validation(string field, string message)
    {
        return new RequestValidationException(new Dictionary<string, string[]>
        {
            [field] = [message],
        });
    }

    private static string ExtractErrorMessage(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return "Supabase Auth request failed.";
        }

        try
        {
            using var document = JsonDocument.Parse(content);
            var root = document.RootElement;
            return GetString(root, "msg")
                ?? GetString(root, "message")
                ?? GetString(root, "error_description")
                ?? GetString(root, "error")
                ?? "Supabase Auth request failed.";
        }
        catch (JsonException)
        {
            return content;
        }
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        return element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(propertyName, out var property)
            && property.ValueKind != JsonValueKind.Null
            ? property.GetString()
            : null;
    }

    private static int? GetInt(JsonElement element, string propertyName)
    {
        return element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(propertyName, out var property)
            && property.TryGetInt32(out var value)
            ? value
            : null;
    }
}
