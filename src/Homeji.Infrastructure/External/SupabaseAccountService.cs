using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Homeji.Application.Common.Exceptions;
using Homeji.Application.DTOs.Accounts;
using Homeji.Application.IRepositories.Accounts;
using Homeji.Application.IServices.Accounts;
using Homeji.Application.IServices.Emails;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Homeji.Infrastructure.External;

public sealed class SupabaseAccountService : IAccountService
{
    private static readonly Regex FullNameRegex = new(
        "^[\\p{L}\\p{M}' -]+$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly Action<ILogger, Guid, Exception?> FailedToRemoveUnconfirmedUser =
        LoggerMessage.Define<Guid>(
            LogLevel.Error,
            new EventId(1101, nameof(FailedToRemoveUnconfirmedUser)),
            "Failed to remove unconfirmed Supabase user {UserId} after registration email failure.");

    private readonly IAccountEmailSender _accountEmailSender;
    private readonly IAccountEmailRepository _accountEmailRepository;
    private readonly HttpClient _httpClient;
    private readonly ILogger<SupabaseAccountService> _logger;
    private readonly SupaBaseAuthOptions _options;

    public SupabaseAccountService(
        HttpClient httpClient,
        IOptions<SupaBaseAuthOptions> options,
        IAccountEmailSender accountEmailSender,
        IAccountEmailRepository accountEmailRepository,
        ILogger<SupabaseAccountService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _accountEmailSender = accountEmailSender;
        _accountEmailRepository = accountEmailRepository;
        _logger = logger;
    }

    public async Task<AuthSessionDto> RegisterAsync(
        RegisterAccountDto request,
        CancellationToken cancellationToken = default)
    {
        var displayName = ValidateRegistration(request);
        EnsureConfigured();

        var emailAvailability = await CheckEmailAvailabilityAsync(request.Email, cancellationToken);
        if (emailAvailability.Exists)
        {
            throw new ConflictException("Email này đã được đăng ký tài khoản.");
        }

        var redirectTo = ResolveRedirectUrl(request.RedirectTo, _options.RegistrationRedirectUrl);

        if (!string.IsNullOrWhiteSpace(_options.ServiceRoleKey))
        {
            using var httpRequest = CreateAdminJsonRequest(
                HttpMethod.Post,
                new Uri("auth/v1/admin/generate_link", UriKind.Relative),
                new
                {
                    type = "signup",
                    email = emailAvailability.Email,
                    password = request.Password,
                    data = new
                    {
                        display_name = displayName,
                    },
                    redirect_to = redirectTo,
                });

            var json = await SendAsync(httpRequest, cancellationToken);
            var generatedLink = ParseGeneratedSignupLink(json);
            try
            {
                var emailResult = await _accountEmailSender.SendRegistrationConfirmationAsync(
                    emailAvailability.Email,
                    displayName,
                    generatedLink.ActionLink,
                    cancellationToken);

                if (!emailResult.Sent)
                {
                    await TryRemoveUnconfirmedUserAsync(generatedLink.UserId);
                    throw new ExternalServiceUnavailableException(
                        "SMTP",
                        "The confirmation email could not be sent. Please try registering again.");
                }
            }
            catch (OperationCanceledException)
            {
                await TryRemoveUnconfirmedUserAsync(generatedLink.UserId);
                throw;
            }

            return new AuthSessionDto(
                null,
                null,
                null,
                null,
                generatedLink.UserId,
                generatedLink.Email ?? emailAvailability.Email,
                true,
                "Registration succeeded. Check your email to confirm your account before signing in.");
        }
        else
        {
            var endpoint = BuildAuthUri("signup", redirectTo);
            using var httpRequest = CreateJsonRequest(
                HttpMethod.Post,
                endpoint,
                new
                {
                    email = emailAvailability.Email,
                    password = request.Password,
                    data = new
                    {
                        display_name = displayName,
                    },
                });

            var json = await SendAsync(httpRequest, cancellationToken);
            return ParseAuthSession(json, "Registration succeeded. Check your email to confirm your account before signing in.");
        }
    }

    public async Task<EmailAvailabilityDto> GetEmailAvailabilityAsync(
        string? email,
        CancellationToken cancellationToken = default)
    {
        var emailAvailability = await CheckEmailAvailabilityAsync(email, cancellationToken);
        return new EmailAvailabilityDto(
            emailAvailability.Email,
            emailAvailability.Exists,
            !emailAvailability.Exists);
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
        return ParseAuthSession(json, emailConfirmationMessage: "Đăng nhập thành công.");
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
        return new AccountMessageDto("Đã gửi email khôi phục mật khẩu.");
    }

    public async Task<AccountMessageDto> ResetPasswordAsync(
        ResetPasswordDto request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.AccessToken))
        {
            throw Validation("accessToken", "Thiếu access token.");
        }

        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 6)
        {
            throw Validation("newPassword", "Mật khẩu mới phải có ít nhất 6 ký tự.");
        }

        EnsureConfigured();

        using var httpRequest = CreateJsonRequest(HttpMethod.Put, new Uri("auth/v1/user", UriKind.Relative), new { password = request.NewPassword });
        httpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", request.AccessToken);
        _ = await SendAsync(httpRequest, cancellationToken, addBearerApiKey: false);
        return new AccountMessageDto("Đã cập nhật mật khẩu.");
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

    private async Task<(string Email, bool Exists)> CheckEmailAvailabilityAsync(
        string? email,
        CancellationToken cancellationToken)
    {
        ValidateEmail(email);
        var normalizedEmail = NormalizeEmail(email!);
        var exists = await _accountEmailRepository.ExistsAsync(normalizedEmail, cancellationToken);
        return (normalizedEmail, exists);
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

    private HttpRequestMessage CreateAdminJsonRequest(HttpMethod method, Uri relativeUri, object payload)
    {
        var request = new HttpRequestMessage(method, relativeUri)
        {
            Content = JsonContent.Create(payload, options: JsonOptions),
        };

        request.Headers.Add("apikey", _options.ServiceRoleKey);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
            "Bearer",
            _options.ServiceRoleKey);
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

    private static (string ActionLink, Guid? UserId, string? Email) ParseGeneratedSignupLink(JsonDocument document)
    {
        var root = document.RootElement;
        var properties = root.TryGetProperty("properties", out var propertiesElement)
            ? propertiesElement
            : root;
        var actionLink = GetString(properties, "action_link") ?? GetString(root, "action_link");
        if (string.IsNullOrWhiteSpace(actionLink))
        {
            throw new ExternalServiceUnavailableException(
                "Supabase Auth",
                "Supabase không trả về liên kết xác nhận email.");
        }

        var user = root.TryGetProperty("user", out var userElement) ? userElement : default;
        var userIdText = GetString(user, "id");
        return (
            actionLink,
            Guid.TryParse(userIdText, out var userId) ? userId : null,
            GetString(user, "email"));
    }

    private async Task TryRemoveUnconfirmedUserAsync(Guid? userId)
    {
        if (userId is null)
        {
            return;
        }

        try
        {
            using var request = CreateAdminJsonRequest(
                HttpMethod.Delete,
                new Uri($"auth/v1/admin/users/{userId.Value:D}", UriKind.Relative),
                new { });
            request.Content = null;
            _ = await SendAsync(request, CancellationToken.None);
        }
        catch (Exception exception)
        {
            FailedToRemoveUnconfirmedUser(_logger, userId.Value, exception);
        }
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

    private void EnsureRegistrationConfigured()
    {
        if (string.IsNullOrWhiteSpace(_options.ServiceRoleKey))
        {
            throw new InvalidOperationException(
                "Supabase ServiceRoleKey must be configured to generate registration confirmation links.");
        }
    }

    private static string? ResolveRedirectUrl(string? requestRedirectTo, string? configuredRedirectTo)
    {
        if (!string.IsNullOrWhiteSpace(requestRedirectTo)
            && requestRedirectTo != "string"
            && Uri.TryCreate(requestRedirectTo, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            return requestRedirectTo;
        }

        return configuredRedirectTo;
    }

    private static void ValidateEmailAndPassword(string? email, string? password)
    {
        ValidateEmail(email);
        if (string.IsNullOrWhiteSpace(password) || password.Length < 6)
        {
            throw Validation("password", "Mật khẩu phải có ít nhất 6 ký tự.");
        }
    }

    private static string ValidateRegistration(RegisterAccountDto request)
    {
        ValidateEmailAndPassword(request.Email, request.Password);

        var normalizedEmail = NormalizeEmail(request.Email!);
        if (!Regex.IsMatch(normalizedEmail, "^[^\\s@]+@gmail\\.com$", RegexOptions.CultureInvariant))
        {
            throw Validation("email", "Registration requires a Gmail address (@gmail.com).");
        }

        var normalizedName = Regex.Replace(request.DisplayName?.Trim() ?? string.Empty, "\\s+", " ");
        if (normalizedName.Length is < 2 or > 60)
        {
            throw Validation("displayName", "Full name must contain between 2 and 60 characters.");
        }

        if (!FullNameRegex.IsMatch(normalizedName)
            || normalizedName.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length < 2)
        {
            throw Validation("displayName", "Enter both family name and given name using letters only.");
        }

        return normalizedName;
    }

    private static void ValidateEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@', StringComparison.Ordinal))
        {
            throw Validation("email", "Email không hợp lệ.");
        }
    }

    private static string NormalizeEmail(string email)
    {
        return email.Trim().ToLowerInvariant();
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
            return "Yêu cầu xác thực Supabase thất bại.";
        }

        try
        {
            using var document = JsonDocument.Parse(content);
            var root = document.RootElement;
            return GetString(root, "msg")
                ?? GetString(root, "message")
                ?? GetString(root, "error_description")
                ?? GetString(root, "error")
                ?? "Yêu cầu xác thực Supabase thất bại.";
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
