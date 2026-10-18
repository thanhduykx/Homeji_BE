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
using Homeji.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace Homeji.Infrastructure.External;

public sealed partial class SupabaseAccountService : IAccountService, ISignupOtpService, IPasswordRecoveryOtpService
{
    private static readonly Regex FullNameRegex = new(
        "^[\\p{L}\\p{M}' -]+$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking, TimeSpan.FromMilliseconds(100));
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
    private readonly EmailDeliveryOptions _emailOptions;
    private readonly ApplicationDbContext? _database;
    private readonly ITransactionalEmailSender? _transactionalSender;
    private readonly TimeProvider _timeProvider;

    public SupabaseAccountService(
        HttpClient httpClient,
        IOptions<SupaBaseAuthOptions> options,
        IAccountEmailSender accountEmailSender,
        IAccountEmailRepository accountEmailRepository,
        ILogger<SupabaseAccountService> logger,
        IOptions<EmailDeliveryOptions> emailOptions,
        ApplicationDbContext? database = null,
        ITransactionalEmailSender? transactionalSender = null,
        TimeProvider? timeProvider = null)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _emailOptions = emailOptions.Value;
        _accountEmailSender = accountEmailSender;
        _accountEmailRepository = accountEmailRepository;
        _logger = logger;
        _database = database;
        _transactionalSender = transactionalSender;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<AuthSessionDto> RegisterAsync(
        RegisterAccountDto request,
        CancellationToken cancellationToken = default)
    {
        var displayName = ValidateRegistration(request);
        EnsureConfigured();

        if (_emailOptions.UsesBrevo)
        {
            EnsureRegistrationConfigured();
        }
        if (_emailOptions.RegistrationOtpEnabled)
        {
            EnsureSignupOtpConfigured();
        }

        var emailAvailability = await CheckEmailAvailabilityAsync(request.Email, cancellationToken);
        if (emailAvailability.Exists)
        {
            throw new ConflictException("Email này đã được đăng ký tài khoản.");
        }

        var redirectTo = _emailOptions.UsesBrevo
            ? ResolveAccountEmailRedirect(request.RedirectTo, _options.RegistrationRedirectUrl)
            : ResolveRedirectUrl(request.RedirectTo, _options.RegistrationRedirectUrl);

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

            using var json = await SendAsync(httpRequest, cancellationToken);
            var generatedLink = ParseGeneratedAccountLink(json);
            try
            {
                var emailResult = _emailOptions.RegistrationOtpEnabled
                    ? await SendInitialSignupOtpAsync(emailAvailability.Email, generatedLink.UserId, json, cancellationToken)
                    : await _accountEmailSender.SendRegistrationConfirmationAsync(
                        emailAvailability.Email, displayName, generatedLink.ActionLink, cancellationToken);

                if (!emailResult.Sent)
                {
                    throw new ExternalServiceUnavailableException(
                        "Email",
                        "The confirmation email could not be sent. Please try registering again.");
                }
            }
            catch
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
                _emailOptions.RegistrationOtpEnabled
                    ? "Nhập mã OTP trong email để hoàn tất đăng ký."
                    : "Registration succeeded. Check your email to confirm your account before signing in.",
                _emailOptions.RegistrationOtpEnabled ? "otp" : "link");
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

    private void EnsureSignupOtpConfigured()
    {
        EnsureConfigured();
        EnsureRegistrationConfigured();
        if (!_emailOptions.UsesBrevo || !_emailOptions.RegistrationOtpEnabled
            || _database is null || _transactionalSender is null)
        {
            throw new ExternalServiceUnavailableException("Email", "Xác thực OTP chưa được cấu hình.");
        }
    }

    private async Task<Homeji.Application.DTOs.Emails.EmailSendResultDto> SendInitialSignupOtpAsync(
        string email, Guid? userId, JsonDocument generated, CancellationToken cancellationToken)
    {
        EnsureSignupOtpConfigured();
        if (userId is null)
        {
            throw new ExternalServiceUnavailableException("Supabase Auth", "Thiếu tài khoản chờ xác thực.");
        }
        var code = ReadSignupOtp(generated);
        var now = _timeProvider.GetUtcNow();
        var challenges = _database!.Set<SignupVerificationChallenge>();
        await challenges.Where(x => x.Email == email).ExecuteDeleteAsync(cancellationToken);
        challenges.Add(new SignupVerificationChallenge
        {
            Email = email, UserId = userId.Value, ExpiresAt = now.AddDays(1),
            CodeExpiresAt = now.AddMinutes(10), NextSendAt = now.AddSeconds(60),
        });
        await _database.SaveChangesAsync(cancellationToken);
        return await SendSignupCodeAsync(email, code, cancellationToken);
    }

    private Task<Homeji.Application.DTOs.Emails.EmailSendResultDto> SendSignupCodeAsync(
        string email, string code, CancellationToken cancellationToken)
    {
        var text = $"Mã xác thực đăng ký Homeji: {code}. Nhập mã trên website trong 10 phút. Không chia sẻ mã này. Nếu bạn không đăng ký, hãy bỏ qua email.";
        var html = HomejiEmailLayout.Otp(code, recovery: false);
        return _transactionalSender!.SendAsync(email, "Mã OTP xác thực đăng ký Homeji", html, text, Guid.NewGuid(), cancellationToken);
    }

    private static string ReadSignupOtp(JsonDocument generated)
    {
        var root = generated.RootElement;
        var properties = root.TryGetProperty("properties", out var value) ? value : root;
        var code = GetString(properties, "email_otp");
        if (code is null || code.Length is < 6 or > 10 || code.Any(c => c is < '0' or > '9'))
        {
            throw new ExternalServiceUnavailableException("Supabase Auth", "Không tạo được mã xác thực đăng ký.");
        }
        return code;
    }

    public async Task<AccountMessageDto> ResendSignupOtpAsync(
        ResendSignupOtpDto request, CancellationToken cancellationToken = default)
    {
        EnsureSignupOtpConfigured();
        ValidateEmail(request.Email);
        var email = request.Email.Trim().ToLowerInvariant();
        var now = _timeProvider.GetUtcNow();
        var claimed = await _database!.Set<SignupVerificationChallenge>()
            .Where(x => x.Email == email && x.NextSendAt <= now)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.NextSendAt, now.AddSeconds(60))
                .SetProperty(x => x.ExpiresAt, now.AddDays(1))
                .SetProperty(x => x.CodeExpiresAt, now.AddMinutes(10))
                .SetProperty(x => x.Attempts, 0), cancellationToken);
        if (claimed == 0)
        {
            throw Validation("email", "Vui lòng chờ 60 giây giữa các lần gửi. Chỉ tài khoản đang chờ xác thực OTP mới nhận được mã.");
        }
        // No password/data: rotate only the confirmation token for an existing unconfirmed signup.
        using var generateRequest = CreateAdminJsonRequest(HttpMethod.Post,
            new Uri("auth/v1/admin/generate_link", UriKind.Relative), new { type = "signup", email });
        using var generated = await SendAsync(generateRequest, cancellationToken);
        var link = ParseGeneratedAccountLink(generated);
        var expectedUser = await _database.Set<SignupVerificationChallenge>().AsNoTracking()
            .Where(x => x.Email == email).Select(x => x.UserId).SingleAsync(cancellationToken);
        if (link.UserId != expectedUser)
        {
            throw new ExternalServiceUnavailableException("Supabase Auth", "Tài khoản xác thực không khớp.");
        }
        var sent = await SendSignupCodeAsync(email, ReadSignupOtp(generated), cancellationToken);
        if (!sent.Sent)
        {
            throw new ExternalServiceUnavailableException("Email", "Chưa gửi được mã. Vui lòng thử lại sau 60 giây.");
        }
        return new AccountMessageDto("Đã gửi mã mới. Mã trước đó không còn hiệu lực.");
    }

    public async Task<AuthSessionDto> VerifySignupOtpAsync(
        SignupOtpDto request, CancellationToken cancellationToken = default)
    {
        EnsureSignupOtpConfigured();
        ValidateEmail(request.Email);
        if (request.Code is null || request.Code.Length is < 6 or > 10
            || request.Code.Any(c => c is < '0' or > '9'))
        {
            throw Validation("code", "Mã OTP phải gồm 6 đến 10 chữ số.");
        }
        var email = request.Email.Trim().ToLowerInvariant();
        var now = _timeProvider.GetUtcNow();
        var challenges = _database!.Set<SignupVerificationChallenge>();
        var claimed = await challenges.Where(x => x.Email == email && x.ExpiresAt > now
                && x.CodeExpiresAt > now && x.Attempts < 5)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Attempts, x => x.Attempts + 1), cancellationToken);
        if (claimed == 0)
        {
            throw Validation("code", "Mã đã hết hạn hoặc vượt quá 5 lần thử. Vui lòng yêu cầu mã mới.");
        }
        var expectedUser = await challenges.AsNoTracking().Where(x => x.Email == email)
            .Select(x => x.UserId).SingleAsync(cancellationToken);
        using var verifyRequest = CreateJsonRequest(HttpMethod.Post,
            new Uri("auth/v1/verify", UriKind.Relative), new { type = "signup", email, token = request.Code });
        using var verified = await SendAsync(verifyRequest, cancellationToken);
        var session = ParseAuthSession(verified, "Xác thực email thành công.");
        if (session.UserId != expectedUser || string.IsNullOrEmpty(session.AccessToken))
        {
            throw new ExternalServiceUnavailableException("Supabase Auth", "Phiên xác thực không hợp lệ.");
        }
        await challenges.Where(x => x.Email == email && x.UserId == expectedUser).ExecuteDeleteAsync(cancellationToken);
        return session;
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

        var redirectTo = _emailOptions.UsesBrevo
            ? ResolveAccountEmailRedirect(request.RedirectTo, _options.PasswordResetRedirectUrl)
            : ResolveRedirectUrl(request.RedirectTo, _options.PasswordResetRedirectUrl);
        if (_emailOptions.UsesBrevo)
        {
            EnsureRegistrationConfigured();
            // Match the public recovery response for unknown accounts to avoid enumeration.
            var emailAvailability = await CheckEmailAvailabilityAsync(request.Email, cancellationToken);
            if (!emailAvailability.Exists)
            {
                return new AccountMessageDto("Đã gửi email khôi phục mật khẩu.");
            }

            using var generateRequest = CreateAdminJsonRequest(
                HttpMethod.Post,
                new Uri("auth/v1/admin/generate_link", UriKind.Relative),
                new { type = "recovery", email = emailAvailability.Email, redirect_to = redirectTo });
            using var generated = await SendAsync(generateRequest, cancellationToken);
            var link = ParseGeneratedAccountLink(generated);
            var result = await _accountEmailSender.SendPasswordResetAsync(
                emailAvailability.Email, link.ActionLink, cancellationToken);
            if (!result.Sent)
            {
                throw new ExternalServiceUnavailableException("Email", "Không gửi được email khôi phục mật khẩu. Vui lòng thử lại sau.");
            }

            return new AccountMessageDto("Đã gửi email khôi phục mật khẩu.");
        }

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

    public async Task<AuthSessionDto> LoginWithGoogleAsync(
        GoogleLoginDto request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.IdToken))
        {
            throw Validation("idToken", "Thiếu Google ID token.");
        }

        EnsureConfigured();

        using var httpRequest = CreateJsonRequest(
            HttpMethod.Post,
            new Uri("auth/v1/token?grant_type=id_token", UriKind.Relative),
            new
            {
                provider = "google",
                id_token = request.IdToken,
            });

        var json = await SendAsync(httpRequest, cancellationToken);
        return ParseAuthSession(json, emailConfirmationMessage: "Đăng nhập Google thành công.");
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

    private static (string ActionLink, Guid? UserId, string? Email) ParseGeneratedAccountLink(JsonDocument document)
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
                "Supabase không trả về liên kết xác thực tài khoản.");
        }

        var user = root.TryGetProperty("user", out var userElement) ? userElement : root;
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

    private static string ResolveAccountEmailRedirect(string? requested, string configured)
    {
        if (!Uri.TryCreate(configured, UriKind.Absolute, out var callback)
            || (callback.Scheme != Uri.UriSchemeHttps
                && !(callback.Scheme == Uri.UriSchemeHttp && callback.IsLoopback)))
        {
            throw new InvalidOperationException("An HTTPS account email callback (or localhost for development) must be configured.");
        }

        if (!string.IsNullOrWhiteSpace(requested) && requested != "string"
            && !string.Equals(requested, configured, StringComparison.Ordinal))
        {
            throw Validation("redirectTo", "Liên kết chuyển hướng phải khớp callback đã cấu hình.");
        }

        return configured;
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

        var normalizedName = Regex.Replace(request.DisplayName?.Trim() ?? string.Empty, "\\s+", " ",
            RegexOptions.NonBacktracking, TimeSpan.FromMilliseconds(100));
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
        if (string.IsNullOrWhiteSpace(email))
        {
            throw Validation("email", "Email không hợp lệ.");
        }

        var normalized = NormalizeEmail(email);
        if (normalized.Length > 254
            || !Regex.IsMatch(normalized, @"^[^\s@]+@[^\s@]+\.[^\s@]+$",
                RegexOptions.CultureInvariant | RegexOptions.NonBacktracking, TimeSpan.FromMilliseconds(100)))
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
