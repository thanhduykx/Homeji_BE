using System.Net;
using Homeji.Application.Common.Exceptions;
using Homeji.Application.DTOs.Accounts;
using Homeji.Application.DTOs.Emails;
using Homeji.Application.IRepositories.Accounts;
using Homeji.Application.IServices.Emails;
using Homeji.Infrastructure.External;
using Microsoft.Extensions.Options;

namespace Homeji.Api.IntegrationTests.Infrastructure;

public sealed class SupabaseAccountServiceTests
{
    [Fact]
    public async Task CheckEmailAsync_NormalizesEmailAndReturnsAvailability()
    {
        var repository = new StubAccountEmailRepository(exists: false);
        var service = CreateService(repository);

        var result = await service.CheckEmailAsync("  User@Example.COM ");

        Assert.Equal("user@example.com", result.Email);
        Assert.False(result.Exists);
        Assert.True(result.Available);
        Assert.Equal("user@example.com", repository.LastEmail);
    }

    [Fact]
    public async Task RegisterAsync_WhenEmailExists_DoesNotCallSupabaseSignup()
    {
        var repository = new StubAccountEmailRepository(exists: true);
        var handler = new CountingHttpMessageHandler();
        var service = CreateService(repository, handler);
        var request = new RegisterAccountDto(
            "existing@example.com",
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
              "user": {
                "id": "c664dbea-992b-4ba7-8702-bf5740e82034",
                "email": "new@example.com"
              }
            }
            """);
        var service = CreateService(repository, handler);
        var request = new RegisterAccountDto(
            "new@example.com",
            "password123",
            "New User",
            null);

        _ = await service.RegisterAsync(request);

        Assert.NotNull(handler.LastRequestUri);
        Assert.Equal(
            "/auth/v1/signup?redirect_to=https%3A%2F%2Fhomeji.example%2Fauth%2Fcallback",
            handler.LastRequestUri!.PathAndQuery);
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
            RegistrationRedirectUrl = "https://homeji.example/auth/callback",
        });

        return new SupabaseAccountService(
            httpClient,
            options,
            new StubAccountEmailSender(),
            repository);
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

    private sealed class StubAccountEmailSender : IAccountEmailSender
    {
        public Task<EmailSendResultDto> SendRegistrationConfirmationAsync(
            string email,
            string? displayName,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new EmailSendResultDto(true, "Sent."));
        }
    }

    private sealed class CountingHttpMessageHandler(string responseContent = "{}") : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        public Uri? LastRequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            LastRequestUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseContent),
            });
        }
    }
}
