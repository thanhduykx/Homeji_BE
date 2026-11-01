using Homeji.Application.Abstractions.Authentication;
using Homeji.Application.Common.Exceptions;
using Homeji.Application.IServices.Subscriptions;
using Homeji.Application.DTOs.Payments;
using Homeji.Application.Services.Common;
using Homeji.Application.Services.Subscriptions;
using Homeji.Domain.Entities;
using Homeji.Domain.Enums;
using Homeji.Infrastructure.Context;
using Homeji.Infrastructure.External;
using Homeji.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
namespace Homeji.Api.IntegrationTests;

public sealed class PremiumPricingDatabaseTests
{
    [LocalMarketplaceDatabaseFact]
    public async Task Admin_changes_are_persistent_idempotent_and_public_while_stale_edits_are_rejected()
    {
        await using var db = Open(); var admin = await Seed(db, UserRole.Admin);
        var service = Service(db, admin);
        var original = (await service.GetPlansAsync()).Single();
        Assert.Equal(49000, original.Price); Assert.Equal(99000, original.OriginalPrice);
        var request = new ChangePremiumPriceRequest(Guid.NewGuid(), 49000, 59000, "Ưu đãi mới cho sinh viên");
        var first = await service.ChangeAsync(original.Code, request);
        Assert.Equal(first.Id, (await service.ChangeAsync(original.Code, request)).Id);
        Assert.Equal(59000, (await service.GetPlansAsync()).Single().Price);
        Assert.Null((await service.GetPlansAsync()).Single().OfferEndsAt);
        var context = new UserContext(new CurrentUser(admin), new UserProfileRepository(db));
        var options = Options.Create(new PremiumSubscriptionOptions());
        var subscriptions = new SubscriptionService(context, null!, null!, options, TimeProvider.System, service);
        Assert.Equal(59000, (await subscriptions.GetPackagesAsync()).Single(row => row.Code == original.Code).Price);
        var wallet = new PremiumWalletPaymentService(db, context, options, TimeProvider.System, null!, pricing: service);
        await Assert.ThrowsAsync<RequestValidationException>(() => wallet.PurchaseAsync(original.Code, new WalletPremiumPaymentDto(Guid.NewGuid(), 49000)));
        using var http = new HttpClient();
        var gateways = new PaymentService(http, context, null!, null!, null!, Options.Create(new MomoOptions()), Options.Create(new PayOsOptions()), options, TimeProvider.System, null!, service);
        await Assert.ThrowsAsync<RequestValidationException>(() => gateways.CreatePremiumMomoPaymentAsync(original.Code, 49000));
        await Assert.ThrowsAsync<RequestValidationException>(() => gateways.CreatePremiumPayOsPaymentAsync(original.Code, 49000));
        Assert.Contains(await service.GetAnnouncementsAsync(), row => row.Id == first.Id && row.Reason == request.Reason);
        await Assert.ThrowsAsync<RequestValidationException>(() => service.ChangeAsync(original.Code, request with { RequestId = Guid.NewGuid(), Price = 69000 }));
        await Assert.ThrowsAsync<RequestValidationException>(() => service.ChangeAsync(original.Code, request with { Price = 79000 }));
        await using var reopened = Open();
        Assert.Equal(59000, (await Service(reopened, admin).GetPlansAsync()).Single().Price);
        Assert.True(await db.Database.SqlQueryRaw<bool>("SELECT relrowsecurity AS \"Value\" FROM pg_class WHERE oid='homeji.premium_price_changes'::regclass").SingleAsync());
    }
    [LocalMarketplaceDatabaseFact]
    public async Task Non_admins_and_invalid_amounts_or_reasons_cannot_change_prices()
    {
        await using var db = Open(); var admin = await Seed(db, UserRole.Admin); var renter = await Seed(db, UserRole.Renter);
        var request = new ChangePremiumPriceRequest(Guid.NewGuid(), 49000, 59000, "Lý do hợp lệ");
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => Service(db, renter).ChangeAsync("PREMIUM_MONTHLY", request));
        foreach (var price in new decimal[] { -1, 0, 999, 1000.5m, 100_000_001 })
            await Assert.ThrowsAsync<RequestValidationException>(() => Service(db, admin).ChangeAsync("PREMIUM_MONTHLY", request with { Price = price }));
        await Assert.ThrowsAsync<RequestValidationException>(() => Service(db, admin).ChangeAsync("PREMIUM_MONTHLY", request with { Reason = " " }));
    }
    [LocalMarketplaceDatabaseFact]
    public async Task Two_admins_cannot_overwrite_the_same_expected_price()
    {
        await using var db = Open(); var admin = await Seed(db, UserRole.Admin);
        var code = "RACE_" + Guid.NewGuid().ToString("N").ToUpperInvariant();
        var setup = Options.Create(new PremiumSubscriptionOptions { Plans = [new PremiumPlanOptions { Code = code, Name = "Gói thử", Price = 99000 }] });
        await using var first = Open(); await using var second = Open();
        var outcomes = await Task.WhenAll(Change(first, 59000), Change(second, 69000));
        Assert.Equal(1, outcomes.Count(success => success));
        async Task<bool> Change(ApplicationDbContext context, decimal amount)
        {
            var service = new PremiumPricing(context, new UserContext(new CurrentUser(admin), new UserProfileRepository(context)), setup, TimeProvider.System);
            try { await service.ChangeAsync(code, new ChangePremiumPriceRequest(Guid.NewGuid(), 99000, amount, "Cập nhật giá đồng thời")); return true; }
            catch (RequestValidationException) { return false; }
        }
    }
    private static ApplicationDbContext Open()
    {
        var connection = Environment.GetEnvironmentVariable("HOMEJI_TEST_DATABASE") ?? throw new InvalidOperationException("Local database required");
        var parsed = new NpgsqlConnectionStringBuilder(connection);
        if (parsed.Host != "127.0.0.1" || parsed.Database != "homeji_quality") throw new InvalidOperationException("Disposable local database only");
        return new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options);
    }
    private static PremiumPricing Service(ApplicationDbContext db, Guid id) => new(db, new UserContext(new CurrentUser(id), new UserProfileRepository(db)), Options.Create(new PremiumSubscriptionOptions { LaunchOfferStartsAt = DateTimeOffset.UtcNow.AddDays(-1), LaunchOfferEndsAt = DateTimeOffset.UtcNow.AddDays(1) }), TimeProvider.System);
    private static async Task<Guid> Seed(ApplicationDbContext db, UserRole role)
    {
        var id = Guid.NewGuid(); var email = id.ToString("N") + "@example.invalid";
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO auth.users(id,email,email_confirmed_at) VALUES ({id},{email},now())");
        var profile = UserProfile.Create(id, "Fixture " + id.ToString("N"), DateTimeOffset.UtcNow); profile.SetRole(role, DateTimeOffset.UtcNow);
        db.UserProfiles.Add(profile); await db.SaveChangesAsync(); return id;
    }
    private sealed record CurrentUser(Guid? UserId) : ICurrentUser;
}
