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

        Assert.Equal("An account with this email already exists.", exception.Message);
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
    [InlineData("user@example.com", "New User", "email")]
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
            Microsoft.Extensions.Logging.Abstractions.NullLogger<SupabaseAccountService>.Instance);

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
            Microsoft.Extensions.Logging.Abstractions.NullLogger<SupabaseAccountService>.Instance);

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

    private static SupabaseAccountService CreateService(
        IAccountEmailRepository repository,
        HttpMessageHandler? handler = null)
    {
        var httpClient = new HttpClient(handler ?? new CountingHttpMessageHandler());
        var options = Options.Create(new SupaBaseAuthOptions
        {
            ProjectUrl = "https://project.supabase.co",
            ApiKey = "test-key",
            ServiceRoleKey = "test-service-role-key",
            RegistrationRedirectUrl = "https://homeji.example/auth/callback",
        });

        return new SupabaseAccountService(
            httpClient,
            options,
            new StubAccountEmailSender(),
            repository,
            NullLogger<SupabaseAccountService>.Instance);
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
