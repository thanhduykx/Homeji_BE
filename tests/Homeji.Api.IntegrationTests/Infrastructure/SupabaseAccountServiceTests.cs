using System.Net;
using Homeji.Application.Common.Exceptions;
using Homeji.Application.DTOs.Accounts;
using Homeji.Application.DTOs.Emails;
using Homeji.Application.IRepositories.Accounts;
using Homeji.Application.IServices.Emails;
using Homeji.Infrastructure.External;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Homeji.Api.IntegrationTests.Infrastructure;

public sealed class SupabaseAccountServiceTests
{
    [Fact]
    public async Task BrevoRegistration_WithoutServiceKey_DoesNotFallBackToSupabaseMailer()
    {
        using var handler = new CountingHttpMessageHandler();
        var service = CreateService(new StubAccountEmailRepository(false), handler, "Brevo", serviceRoleKey: "");
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RegisterAsync(
            new RegisterAccountDto("user@example.com", "password123", "New User", null)));
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task BrevoRecovery_RejectsUntrustedRedirectBeforeGeneratingLink()
    {
        using var handler = new CountingHttpMessageHandler();
        var service = CreateService(new StubAccountEmailRepository(true), handler, "Brevo");
        var error = await Assert.ThrowsAsync<RequestValidationException>(() => service.ForgotPasswordAsync(
            new ForgotPasswordDto("user@example.com", "https://untrusted.example/reset")));
        Assert.Contains("redirectTo", error.Errors.Keys);
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task ForgotPassword_Brevo_GeneratesRecoveryLinkAndUsesEmailSender()
    {
        using var handler = new CountingHttpMessageHandler("""
            {"properties":{"action_link":"https://project.supabase.co/auth/v1/verify?token=recovery&type=recovery"},
             "user":{"id":"c664dbea-992b-4ba7-8702-bf5740e82034","email":"user@example.com"}}
            """);
        var sender = new StubAccountEmailSender();
        var service = CreateService(new StubAccountEmailRepository(true), handler, "Brevo", sender);

        _ = await service.ForgotPasswordAsync(new ForgotPasswordDto(" User@Example.COM ", null));

        Assert.Equal("/auth/v1/admin/generate_link", handler.LastRequestUri!.AbsolutePath);
        Assert.Contains("\"type\":\"recovery\"", handler.LastRequestBody, StringComparison.Ordinal);
        Assert.Contains("\"email\":\"user@example.com\"", handler.LastRequestBody, StringComparison.Ordinal);
        Assert.Contains("https://homeji.example/reset-password", handler.LastRequestBody, StringComparison.Ordinal);
        Assert.Equal("Bearer test-service-role-key", handler.LastAuthorization);
        Assert.Contains("type=recovery", sender.ResetUrl, StringComparison.Ordinal);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task ForgotPassword_UnknownEmail_DoesNotGenerateLinkOrSend()
    {
        using var handler = new CountingHttpMessageHandler();
        var sender = new StubAccountEmailSender();
        var service = CreateService(new StubAccountEmailRepository(false), handler, "Brevo", sender);
        var result = await service.ForgotPasswordAsync(new ForgotPasswordDto("unknown@example.com", null));
        Assert.Equal("Đã gửi email khôi phục mật khẩu.", result.Message);
        Assert.Equal(0, handler.RequestCount);
        Assert.Null(sender.ResetUrl);
    }

    [Fact]
    public async Task ForgotPassword_EmailFailure_DoesNotDeleteExistingUser()
    {
        using var handler = new CountingHttpMessageHandler("""
            {"action_link":"https://project.supabase.co/verify?type=recovery",
             "user":{"id":"c664dbea-992b-4ba7-8702-bf5740e82034"}}
            """);
        var service = CreateService(new StubAccountEmailRepository(true), handler, "Brevo", new StubAccountEmailSender { Succeeds = false });
        await Assert.ThrowsAsync<ExternalServiceUnavailableException>(() => service.ForgotPasswordAsync(new ForgotPasswordDto("user@example.com", null)));
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task ForgotPassword_Smtp_PreservesSupabaseRecoveryEndpoint()
    {
        using var handler = new CountingHttpMessageHandler();
        var service = CreateService(new StubAccountEmailRepository(true), handler);
        _ = await service.ForgotPasswordAsync(new ForgotPasswordDto("user@example.com", null));
        Assert.Equal("/auth/v1/recover", handler.LastRequestUri!.AbsolutePath);
        Assert.Equal("Bearer test-key", handler.LastAuthorization);
    }

    [Fact]
    public async Task GetEmailAvailabilityAsync_NormalizesEmailAndReturnsAvailability()
    {
        var repository = new StubAccountEmailRepository(exists: true);
        var service = CreateService(repository);

        var result = await service.GetEmailAvailabilityAsync("  User@Example.COM ");

        Assert.Equal("user@example.com", result.Email);
        Assert.True(result.Exists);
        Assert.False(result.Available);
        Assert.Equal("user@example.com", repository.LastEmail);
    }

    [Fact]
    public async Task RegisterAsync_NormalizesEmailBeforeCheckingAvailability()
    {
        var repository = new StubAccountEmailRepository(exists: false);
        var handler = new CountingHttpMessageHandler(
            """
            {
              "action_link": "https://project.supabase.co/auth/v1/verify?token=test&type=signup",
              "user": {
                "id": "c664dbea-992b-4ba7-8702-bf5740e82034",
                "email": "user@gmail.com"
              }
            }
            """);
        var service = CreateService(repository, handler);
        var request = new RegisterAccountDto(
            "  User@Gmail.COM ",
            "password123",
            "New User",
            null);

        var result = await service.RegisterAsync(request);

        Assert.Equal("user@gmail.com", result.Email);
        Assert.Equal("user@gmail.com", repository.LastEmail);
        Assert.True(result.EmailConfirmationRequired);
        Assert.Equal(
            "Registration succeeded. Check your email to confirm your account before signing in.",
            result.Message);
    }

    [Fact]
    public async Task RegisterAsync_WhenEmailExists_DoesNotCallSupabaseSignup()
    {
        var repository = new StubAccountEmailRepository(exists: true);
        var handler = new CountingHttpMessageHandler();
        var service = CreateService(repository, handler);
        var request = new RegisterAccountDto(
            "existing@gmail.com",
            "password123",
            "Existing User",
            null);

        var exception = await Assert.ThrowsAsync<ConflictException>(
            () => service.RegisterAsync(request));

        Assert.Equal("Email này đã được đăng ký tài khoản.", exception.Message);
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task RegisterAsync_UsesConfiguredRegistrationRedirectUrl()
    {
        var repository = new StubAccountEmailRepository(exists: false);
        var handler = new CountingHttpMessageHandler(
            """
            {
              "action_link": "https://project.supabase.co/auth/v1/verify?token=test&type=signup",
              "user": {
                "id": "c664dbea-992b-4ba7-8702-bf5740e82034",
                "email": "new@gmail.com"
              }
            }
            """);
        var service = CreateService(repository, handler);
        var request = new RegisterAccountDto(
            "new@gmail.com",
            "password123",
            "New User",
            null);

        _ = await service.RegisterAsync(request);

        Assert.NotNull(handler.LastRequestUri);
        Assert.Equal(
            "/auth/v1/admin/generate_link",
            handler.LastRequestUri!.PathAndQuery);
        Assert.Contains(
            "\"redirect_to\":\"https://homeji.example/auth/callback\"",
            handler.LastRequestBody,
            StringComparison.Ordinal);
        Assert.Equal("Bearer test-service-role-key", handler.LastAuthorization);
    }

    [Theory]
    [InlineData("userexample.com", "New User", "email")]
    [InlineData("user@gmail.com", "Duy", "displayName")]
    [InlineData("user@gmail.com", "Duy 123", "displayName")]
    public async Task RegisterAsync_WhenIdentityFieldsAreInvalid_DoesNotCallSupabase(
        string email,
        string displayName,
        string expectedField)
    {
        var repository = new StubAccountEmailRepository(exists: false);
        var handler = new CountingHttpMessageHandler();
        var service = CreateService(repository, handler);

        var exception = await Assert.ThrowsAsync<RequestValidationException>(() =>
            service.RegisterAsync(new RegisterAccountDto(email, "password123", displayName, null)));

        Assert.Contains(expectedField, exception.Errors.Keys);
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task RegisterAsync_WhenServiceRoleKeyIsEmpty_FallsBackToStandardSignup()
    {
        var repository = new StubAccountEmailRepository(exists: false);
        var handler = new CountingHttpMessageHandler(
            """
            {
              "access_token": "fallback-access-token",
              "token_type": "bearer",
              "expires_in": 3600,
              "refresh_token": "fallback-refresh-token",
              "user": {
                "id": "c664dbea-992b-4ba7-8702-bf5740e82034",
                "email": "fallback@gmail.com"
              }
            }
            """);

        var httpClient = new HttpClient(handler);
        var options = Options.Create(new SupaBaseAuthOptions
        {
            ProjectUrl = "https://project.supabase.co",
            ApiKey = "test-key",
            ServiceRoleKey = "",
            RegistrationRedirectUrl = "https://homeji.example/auth/callback",
        });

        var service = new SupabaseAccountService(
            httpClient,
            options,
            new StubAccountEmailSender(),
            repository,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<SupabaseAccountService>.Instance,
            Options.Create(new EmailDeliveryOptions()));

        var request = new RegisterAccountDto(
            "fallback@gmail.com",
            "password123",
            "New User",
            null);

        var result = await service.RegisterAsync(request);

        Assert.Equal("fallback@gmail.com", result.Email);
        Assert.False(result.EmailConfirmationRequired);
        Assert.NotNull(handler.LastRequestUri);
        Assert.Equal(
            "/auth/v1/signup",
            handler.LastRequestUri!.AbsolutePath);
        Assert.Contains(
            "redirect_to=https%3A%2F%2Fhomeji.example%2Fauth%2Fcallback",
            handler.LastRequestUri!.Query);
        Assert.Equal("Bearer test-key", handler.LastAuthorization);
    }

    [Fact]
    public async Task RegisterAsync_WhenServiceRoleKeyIsEmptyAndConfirmationRequired_FallsBackToStandardSignup()
    {
        var repository = new StubAccountEmailRepository(exists: false);
        var handler = new CountingHttpMessageHandler(
            """
            {
              "id": "c664dbea-992b-4ba7-8702-bf5740e82034",
              "email": "fallback@gmail.com"
            }
            """);

        var httpClient = new HttpClient(handler);
        var options = Options.Create(new SupaBaseAuthOptions
        {
            ProjectUrl = "https://project.supabase.co",
            ApiKey = "test-key",
            ServiceRoleKey = "",
            RegistrationRedirectUrl = "https://homeji.example/auth/callback",
        });

        var service = new SupabaseAccountService(
            httpClient,
            options,
            new StubAccountEmailSender(),
            repository,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<SupabaseAccountService>.Instance,
            Options.Create(new EmailDeliveryOptions()));

        var request = new RegisterAccountDto(
            "fallback@gmail.com",
            "password123",
            "New User",
            null);

        var result = await service.RegisterAsync(request);

        Assert.Equal("fallback@gmail.com", result.Email);
        Assert.True(result.EmailConfirmationRequired);
        Assert.NotNull(handler.LastRequestUri);
        Assert.Equal(
            "/auth/v1/signup",
            handler.LastRequestUri!.AbsolutePath);
        Assert.Contains(
            "redirect_to=https%3A%2F%2Fhomeji.example%2Fauth%2Fcallback",
            handler.LastRequestUri!.Query);
        Assert.Equal("Bearer test-key", handler.LastAuthorization);
    }

    [Fact]
    public async Task LoginWithGoogleAsync_WhenIdTokenIsMissing_ThrowsValidationException()
    {
        var repository = new StubAccountEmailRepository(exists: false);
        var handler = new CountingHttpMessageHandler();
        var service = CreateService(repository, handler);

        var exception = await Assert.ThrowsAsync<RequestValidationException>(() =>
            service.LoginWithGoogleAsync(new GoogleLoginDto(string.Empty)));

        Assert.Contains("idToken", exception.Errors.Keys);
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task LoginWithGoogleAsync_WhenIdTokenIsValid_CallsSupabaseAndReturnsSession()
    {
        var repository = new StubAccountEmailRepository(exists: false);
        var handler = new CountingHttpMessageHandler(
            """
            {
              "access_token": "google-access-token",
              "token_type": "bearer",
              "expires_in": 3600,
              "refresh_token": "google-refresh-token",
              "user": {
                "id": "c664dbea-992b-4ba7-8702-bf5740e82034",
                "email": "user@gmail.com"
              }
            }
            """);
        var service = CreateService(repository, handler);

        var result = await service.LoginWithGoogleAsync(new GoogleLoginDto("valid-id-token"));

        Assert.Equal("google-access-token", result.AccessToken);
        Assert.Equal("bearer", result.TokenType);
        Assert.Equal(3600, result.ExpiresIn);
        Assert.Equal("google-refresh-token", result.RefreshToken);
        Assert.Equal(Guid.Parse("c664dbea-992b-4ba7-8702-bf5740e82034"), result.UserId);
        Assert.Equal("user@gmail.com", result.Email);
        Assert.False(result.EmailConfirmationRequired);
        Assert.Equal("Đăng nhập Google thành công.", result.Message);

        Assert.NotNull(handler.LastRequestUri);
        Assert.Equal("/auth/v1/token", handler.LastRequestUri!.AbsolutePath);
        Assert.Equal("?grant_type=id_token", handler.LastRequestUri!.Query);
        Assert.Contains("\"provider\":\"google\"", handler.LastRequestBody, StringComparison.Ordinal);
        Assert.Contains("\"id_token\":\"valid-id-token\"", handler.LastRequestBody, StringComparison.Ordinal);
    }

    private static SupabaseAccountService CreateService(
        IAccountEmailRepository repository,
        HttpMessageHandler? handler = null,
        string provider = "Smtp",
        StubAccountEmailSender? emailSender = null,
        string serviceRoleKey = "test-service-role-key")
    {
        var httpClient = new HttpClient(handler ?? new CountingHttpMessageHandler());
        var options = Options.Create(new SupaBaseAuthOptions
        {
            ProjectUrl = "https://project.supabase.co",
            ApiKey = "test-key",
            ServiceRoleKey = serviceRoleKey,
            RegistrationRedirectUrl = "https://homeji.example/auth/callback",
            PasswordResetRedirectUrl = "https://homeji.example/reset-password",
        });

        return new SupabaseAccountService(
            httpClient,
            options,
            emailSender ?? new StubAccountEmailSender(),
            repository,
            NullLogger<SupabaseAccountService>.Instance,
            Options.Create(new EmailDeliveryOptions { Provider = provider }));
    }

    private sealed class StubAccountEmailRepository(bool exists) : IAccountEmailRepository
    {
        public string? LastEmail { get; private set; }

        public Task<bool> ExistsAsync(
            string normalizedEmail,
            CancellationToken cancellationToken = default)
        {
            LastEmail = normalizedEmail;
            return Task.FromResult(exists);
        }
    }

    private sealed class CountingHttpMessageHandler(string responseContent = "{}") : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        public Uri? LastRequestUri { get; private set; }

        public string? LastRequestBody { get; private set; }

        public string? LastAuthorization { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            LastRequestUri = request.RequestUri;
            LastAuthorization = request.Headers.Authorization?.ToString();
            LastRequestBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseContent),
            };
        }
    }

    private sealed class StubAccountEmailSender : IAccountEmailSender
    {
        public bool Succeeds { get; init; } = true;

        public string? ResetUrl { get; private set; }

        public Task<EmailSendResultDto> SendPasswordResetAsync(
            string email, string resetUrl, CancellationToken cancellationToken = default)
        {
            ResetUrl = resetUrl;
            return Task.FromResult(new EmailSendResultDto(Succeeds, "Email result."));
        }

        public Task<EmailSendResultDto> SendRegistrationConfirmationAsync(
            string email,
            string? displayName,
            string confirmationUrl,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new EmailSendResultDto(true, "Email sent."));
        }
    }
}
