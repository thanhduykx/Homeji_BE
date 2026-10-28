using System.Reflection;
using Homeji.Application.Abstractions.Authentication;
using Homeji.Application.Common.Exceptions;
using Homeji.Application.DTOs.Payments;
using Homeji.Application.IRepositories.Profiles;
using Homeji.Application.IRepositories.Wallets;
using Homeji.Application.Services.Common;
using Homeji.Application.Services.Subscriptions;
using Homeji.Domain.Entities;
using Homeji.Infrastructure.External;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Homeji.Api.IntegrationTests.Infrastructure;

public sealed class PaymentProfilePrerequisiteTests
{
    [Theory]
    [InlineData("momo")]
    [InlineData("payos")]
    public async Task Missing_profile_returns_setup_instruction_before_wallet_write(string provider)
    {
        using var client = new HttpClient(new NoNetwork());
        var walletReads = 0;
        var profiles = Stub<IUserProfileRepository>.Create((method, _) => method.Name == "GetByIdAsync"
            ? Task.FromResult<UserProfile?>(null) : throw new InvalidOperationException("Unexpected profile write"));
        var wallets = Stub<IWalletRepository>.Create((method, _) =>
        {
            walletReads++;
            if (method.Name == "GetAsync") return Task.FromResult<WalletAccount?>(null);
            // Matches the FK enforced by WalletAccountConfiguration: a wallet requires a profile.
            throw new DbUpdateException("wallet_accounts user profile foreign key");
        });
        var service = new PaymentService(client, new UserContext(new CurrentUser(Guid.NewGuid()), profiles),
            null!, null!, wallets,
            Options.Create(new MomoOptions { PartnerCode = "test", AccessKey = "test", SecretKey = "test", RedirectUrl = "https://example.test/return", IpnUrl = "https://example.test/ipn" }),
            Options.Create(new PayOsOptions { ClientId = "test", ApiKey = "test", ChecksumKey = "test", ReturnUrl = "https://example.test/return", CancelUrl = "https://example.test/cancel" }),
            Options.Create(new PremiumSubscriptionOptions()), TimeProvider.System, null!);
        var error = await Assert.ThrowsAsync<ForbiddenAccessException>(async () =>
        {
            if (provider == "momo") await service.CreateMomoPaymentAsync(new CreateMomoPaymentDto(100_000, null));
            else await service.CreatePayOsPaymentAsync(new CreatePayOsPaymentDto(100_000, null));
        });
        Assert.Contains("Thông tin cơ bản", error.Message);
        Assert.Equal(0, walletReads);
    }

    private sealed class CurrentUser(Guid id) : ICurrentUser { public Guid? UserId => id; }
    private sealed class NoNetwork : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("No payment gateway may be called");
    }
}

public class Stub<T> : DispatchProxy where T : class
{
    public Func<MethodInfo, object?[]?, object?> Handler { get; set; } = null!;
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1000:Do not declare static members on generic types", Justification = "Test-only proxy factory.")]
    public static T Create(Func<MethodInfo, object?[]?, object?> handler)
    {
        var proxy = Create<T, Stub<T>>();
        ((Stub<T>)(object)proxy).Handler = handler;
        return proxy;
    }
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => Handler(targetMethod!, args);
}
