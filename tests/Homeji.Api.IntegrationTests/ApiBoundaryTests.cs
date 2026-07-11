using System.Net;
using System.Net.Http.Json;
using Homeji.Application.DTOs.Accounts;
using Homeji.Application.IServices.Accounts;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Homeji.Api.IntegrationTests;

public sealed class ApiBoundaryTests : IClassFixture<HomejiApiFactory>
{
    private readonly HttpClient _client;

    public ApiBoundaryTests(HomejiApiFactory factory)
    {
        _client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
        });
    }

    [Fact]
    public async Task LiveHealthCheck_ReturnsOk()
    {
        var response = await _client.GetAsync(new Uri("/health/live", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ProfileEndpoint_WithoutAccessToken_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync(new Uri("/api/profile/me", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CheckEmailEndpoint_WithoutAccessToken_ReturnsOk()
    {
        using var factory = new HomejiApiFactory()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IAccountService>();
                    services.AddScoped<IAccountService, StubAccountService>();
                });
            });
        using var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
        });

        var response = await client.PostAsJsonAsync(
            new Uri("/api/account/check-email", UriKind.Relative),
            new { email = "new@example.com" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private sealed class StubAccountService : IAccountService
    {
        public Task<EmailAvailabilityDto> CheckEmailAsync(
            string? email,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new EmailAvailabilityDto(email ?? string.Empty, false, true));
        }

        public Task<AuthSessionDto> RegisterAsync(
            RegisterAccountDto request,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<AuthSessionDto> LoginAsync(
            LoginAccountDto request,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<AccountMessageDto> ForgotPasswordAsync(
            ForgotPasswordDto request,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<AccountMessageDto> ResetPasswordAsync(
            ResetPasswordDto request,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public AuthUrlDto CreateGoogleLoginUrl(string? redirectTo)
        {
            throw new NotSupportedException();
        }
    }
}
