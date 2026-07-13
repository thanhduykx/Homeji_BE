using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Homeji.Application.Common.Exceptions;
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
    public async Task RegisterEndpoint_WhenEmailAlreadyExists_ReturnsConflict()
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
            new Uri("/api/account/register", UriKind.Relative),
            new
            {
                email = "existing@example.com",
                password = "password123",
                displayName = "Existing User",
            });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        using var problem = JsonDocument.Parse(content);
        Assert.Equal(
            "An account with this email already exists.",
            problem.RootElement.GetProperty("detail").GetString());
        Assert.Equal(
            "An account with this email already exists.",
            problem.RootElement
                .GetProperty("errors")
                .GetProperty("email")[0]
                .GetString());
    }

    [Fact]
    public async Task EmailAvailabilityEndpoint_WithoutAccessToken_ReturnsAvailability()
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

        var response = await client.GetAsync(
            new Uri("/api/account/email-availability?email=existing@example.com", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var availability = await response.Content.ReadFromJsonAsync<EmailAvailabilityDto>();
        Assert.NotNull(availability);
        Assert.Equal("existing@example.com", availability.Email);
        Assert.True(availability.Exists);
        Assert.False(availability.Available);
    }

    private sealed class StubAccountService : IAccountService
    {
        public Task<EmailAvailabilityDto> GetEmailAvailabilityAsync(
            string? email,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new EmailAvailabilityDto(email ?? string.Empty, true, false));
        }

        public Task<AuthSessionDto> RegisterAsync(
            RegisterAccountDto request,
            CancellationToken cancellationToken = default)
        {
            throw new ConflictException("An account with this email already exists.");
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
