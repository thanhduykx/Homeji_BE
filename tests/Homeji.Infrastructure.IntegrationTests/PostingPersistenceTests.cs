using Homeji.Application.Abstractions.Authentication;
using Homeji.Application.Common.Exceptions;
using Homeji.Application.DTOs.RentalPosts;
using Homeji.Application.Services.Common;
using Homeji.Application.Services.Moderation;
using Homeji.Application.Services.RentalPosts;
using Homeji.Application.Services.RentalPosts.Validation;
using Homeji.Domain.Entities;
using Homeji.Domain.Enums;
using Homeji.Domain.Exceptions;
using Homeji.Infrastructure.Context;
using Homeji.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Npgsql;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Homeji.Infrastructure.IntegrationTests;

// Explicit opt-in: this project never uses the application's production configuration.
public sealed class PostingPersistenceTests
{
    [Fact]
    public async Task RentalDraft_ValidatesPersistsMediaAndSubmitsLatestDetails()
    {
        await using var db = await OpenTestDatabaseAsync();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var now = DateTimeOffset.UtcNow;
        var owner = UserProfile.Create(Guid.NewGuid(), "Người thuê kiểm thử", now);
        var other = UserProfile.Create(Guid.NewGuid(), "Tài khoản khác", now);
        db.UserProfiles.AddRange(owner, other);
        await db.SaveChangesAsync();
        var service = CreateService(db, owner.Id);
        var draft = await service.CreateDraftAsync(new CreateRentalPostDraftDto(RentalPostType.RoomTransfer));
        db.ChangeTracker.Clear();
        Assert.Equal(RentalPostStatus.Draft, (await db.RentalPosts.SingleAsync(p => p.Id == draft.Id)).Status);

        var details = new UpdateRentalPostDto(
            RentalPostType.RoomTransfer, "Phòng chuyển nhượng kiểm thử", "Phòng có cửa sổ và chỗ để xe.",
            2_500_000, 2_500_000, 18, "Linh Trung, Thủ Đức", 10.87m, 106.80m, ["wifi"],
            AvailableFrom: DateOnly.FromDateTime(now.UtcDateTime.AddDays(1)),
            TransferKind: RoomTransferKind.LeaseAssignment,
            OriginalLeaseEndsOn: DateOnly.FromDateTime(now.UtcDateTime.AddMonths(3)),
            TransferReason: "Chuyển nơi học", OwnerConsentConfirmed: true,
            OwnerConsentContact: "owner@example.test");
        await Assert.ThrowsAsync<RequestValidationException>(() => service.UpdateAsync(draft.Id, details with { Latitude = 0 }));
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => CreateService(db, other.Id).UpdateAsync(draft.Id, details));
        await service.UpdateAsync(draft.Id, details);
        await Assert.ThrowsAsync<DomainException>(() => service.SubmitAsync(draft.Id));
        await Assert.ThrowsAsync<RequestValidationException>(() => service.AddMediaAsync(draft.Id,
            new AddRentalPostMediaDto(MediaType.Image, "homeji-media", "other-user/image.jpg", true, 0)));

        for (var index = 0; index < RentalPost.MinimumImageCountForSubmit; index++)
        {
            var ownedPath = $"rental-posts/{owner.Id:D}/{draft.Id:D}/image-{index}.jpg";
            await service.AddMediaAsync(draft.Id, new AddRentalPostMediaDto(
                MediaType.Image, index == 0 ? "cloudinary" : "homeji-media",
                index == 0 ? $"https://res.cloudinary.com/homeji-qa/image/upload/f_webp/q_auto/v1/{ownedPath}" : ownedPath,
                index == 0, index));
        }
        await service.UpdateAsync(draft.Id, details with { Title = "Nội dung mới nhất trước khi gửi" });
        await service.SubmitAsync(draft.Id);
        db.ChangeTracker.Clear();
        var persisted = await new RentalPostRepository(db).GetByIdWithMediaAsync(draft.Id);
        Assert.NotNull(persisted);
        Assert.Equal(RentalPostStatus.Pending, persisted.Status);
        Assert.Equal("Nội dung mới nhất trước khi gửi", persisted.Title);
        Assert.Equal(3, persisted.Media.Count);
        Assert.Equal(10.87m, persisted.Latitude);
        Assert.Equal(106.80m, persisted.Longitude);
        // Transaction disposal rolls back only these test rows, not the schema.
    }

    [Fact]
    public async Task OwnInventory_IncludesAllStatusesAndNeverIncludesAnotherSeller()
    {
        await using var db = await OpenTestDatabaseAsync();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var now = DateTimeOffset.UtcNow;
        var seller = UserProfile.Create(Guid.NewGuid(), "Người bán kiểm thử", now);
        var other = UserProfile.Create(Guid.NewGuid(), "Người bán khác", now);
        db.UserProfiles.AddRange(seller, other);
        var active = CreateGoods(seller.Id, "Đang bán", now);
        var sold = CreateGoods(seller.Id, "Đã bán", now.AddMinutes(1));
        sold.MarkSold(now.AddMinutes(2));
        var archived = CreateGoods(seller.Id, "Đã ẩn", now.AddMinutes(2));
        archived.Archive(now.AddMinutes(3));
        db.MarketplacePosts.AddRange(active, sold, archived, CreateGoods(other.Id, "Không thuộc tài khoản", now));
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var result = await new MarketplacePostRepository(db).GetBySellerAsync(seller.Id);
        Assert.Equal(new[] { archived.Id, sold.Id, active.Id }, result.Select(post => post.Id));
        Assert.All(result, post => Assert.Equal(seller.Id, post.SellerId));
        Assert.Equal(3, result.Select(post => post.Status).Distinct().Count());
        Assert.All(result, post => Assert.Single(post.Media));
    }

    private static MarketplacePost CreateGoods(Guid sellerId, string title, DateTimeOffset now) =>
        new(sellerId, title, "Đồ dùng kiểm thử", 200_000, "Đã sử dụng", "Nội thất",
            "Linh Trung, Thủ Đức", 10.87m, 106.80m, null, ["https://example.test/item.jpg"], now);

    private static RentalPostService CreateService(ApplicationDbContext db, Guid ownerId)
    {
        var profiles = new UserProfileRepository(db);
        return new RentalPostService(
            new UserContext(new TestCurrentUser(ownerId), profiles), new RentalPostRepository(db),
            subscriptions: null!, new UpdateRentalPostDtoValidator(), new AddRentalPostMediaDtoValidator(),
            new ContentModerationService(new BadWordRepository(db)), activities: null!, reviews: null!,
            profiles, conversations: null!, appointments: null!, TimeProvider.System);
    }

    private static async Task<ApplicationDbContext> OpenTestDatabaseAsync()
    {
        var connection = Environment.GetEnvironmentVariable("HOMEJI_TEST_DB")
            ?? throw new InvalidOperationException("Set HOMEJI_TEST_DB to an isolated local homeji_ui_qa database.");
        var settings = new NpgsqlConnectionStringBuilder(connection);
        if (settings.Host is not ("127.0.0.1" or "localhost") || settings.Database != "homeji_ui_qa")
            throw new InvalidOperationException("Integration tests only accept loopback host and database homeji_ui_qa.");
        var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options);
        // The loopback QA role owns this dedicated schema and cannot create other databases.
        if (!await db.Database.CanConnectAsync())
        {
            await db.DisposeAsync();
            throw new InvalidOperationException("The isolated local QA database is unavailable.");
        }
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    private sealed record TestCurrentUser(Guid? UserId) : ICurrentUser;
}
