using Homeji.Application.Common.Exceptions;
using Homeji.Application.DTOs.Marketplace;
using Homeji.Application.IRepositories.Marketplace;
using Homeji.Application.IRepositories.Profiles;
using Homeji.Application.IRepositories.RentalPosts;
using Homeji.Application.IRepositories.Wallets;
using Homeji.Application.IServices.Marketplace;
using Homeji.Application.Services.Common;
using Homeji.Application.Services.Moderation;
using Homeji.Domain.Entities;
using Homeji.Domain.Enums;
using Microsoft.Extensions.Options;

namespace Homeji.Application.Services.Marketplace;

public sealed class MarketplacePostService : IMarketplacePostService
{
    private const int MaxPageSize = 50;
    private const decimal DefaultRadiusKm = 5;
    private const decimal MaxRadiusKm = 50;

    private readonly UserContext _userContext;
    private readonly IMarketplacePostRepository _marketplacePosts;
    private readonly IRentalPostRepository _rentalPosts;
    private readonly IUserProfileRepository _profiles;
    private readonly ContentModerationService _moderation;
    private readonly TimeProvider _timeProvider;
    private readonly IWalletRepository _wallets;
    private readonly MarketplaceFinanceOptions _financeOptions;

    public MarketplacePostService(
        UserContext userContext,
        IMarketplacePostRepository marketplacePosts,
        IRentalPostRepository rentalPosts,
        IUserProfileRepository profiles,
        ContentModerationService moderation,
        TimeProvider timeProvider,
        IWalletRepository wallets,
        IOptions<MarketplaceFinanceOptions> financeOptions)
    {
        _userContext = userContext;
        _marketplacePosts = marketplacePosts;
        _rentalPosts = rentalPosts;
        _profiles = profiles;
        _moderation = moderation;
        _timeProvider = timeProvider;
        _wallets = wallets;
        _financeOptions = financeOptions.Value;
    }

    public async Task<IReadOnlyList<MarketplacePostDto>> SearchAsync(
        MarketplaceSearchDto request,
        CancellationToken cancellationToken = default)
    {
        var search = await NormalizeSearchAsync(request, cancellationToken);
        var posts = await _marketplacePosts.SearchActiveAsync(
            search.Keyword,
            search.Category,
            search.MinPrice,
            search.MaxPrice,
            search.MinLatitude,
            search.MaxLatitude,
            search.MinLongitude,
            search.MaxLongitude,
            (search.Page - 1) * search.PageSize,
            search.PageSize,
            cancellationToken);
        var profiles = await _profiles.GetByIdsAsync(
            posts.Select(post => post.SellerId).Distinct().ToArray(),
            cancellationToken);
        var profilesById = profiles.ToDictionary(profile => profile.Id);

        return posts
            .Select(post => ToDto(
                post,
                profilesById.GetValueOrDefault(post.SellerId),
                search.CenterLatitude,
                search.CenterLongitude))
            .OrderBy(post => post.DistanceKm ?? decimal.MaxValue)
            .ThenByDescending(post => post.UpdatedAt)
            .ToArray();
    }

    public async Task<MarketplacePostDto> GetDetailAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var post = await _marketplacePosts.GetByIdWithMediaAsync(id, cancellationToken)
            ?? throw new NotFoundException(nameof(MarketplacePost), id);
        var currentUserId = _userContext.TryGetUserId();
        if (post.Status != MarketplacePostStatus.Active && currentUserId != post.SellerId)
        {
            throw new NotFoundException(nameof(MarketplacePost), id);
        }

        var seller = await _profiles.GetByIdAsync(post.SellerId, cancellationToken);
        return ToDto(post, seller, null, null);
    }

    public async Task<MarketplacePostDto> CreateAsync(
        UpsertMarketplacePostDto request,
        CancellationToken cancellationToken = default)
    {
        await ValidateAsync(request, cancellationToken);
        var seller = await _userContext.GetRequiredProfileAsync(cancellationToken);
        await EnsureSellerFundedAsync(seller.Id, cancellationToken);
        await EnsureLinkedRentalPostExistsAsync(request.LinkedRentalPostId, cancellationToken);
        var post = new MarketplacePost(
            seller.Id,
            request.Title!,
            request.Description!,
            request.Price,
            request.Condition!,
            request.Category!,
            request.Address!,
            request.Latitude,
            request.Longitude,
            request.LinkedRentalPostId,
            request.MediaUrls,
            _timeProvider.GetUtcNow(),
            request.ListingType,
            request.AvailableQuantity,
            request.Unit ?? "món",
            request.PreparationMinutes);
        await _marketplacePosts.AddAsync(post, cancellationToken);
        await _marketplacePosts.SaveChangesAsync(cancellationToken);
        return ToDto(post, seller, null, null);
    }

    public async Task<MarketplacePostDto> UpdateAsync(
        Guid id,
        UpsertMarketplacePostDto request,
        CancellationToken cancellationToken = default)
    {
        await ValidateAsync(request, cancellationToken);
        await EnsureSellerFundedAsync(_userContext.GetRequiredUserId(), cancellationToken);
        var post = await GetOwnedPostAsync(id, cancellationToken);
        await EnsureLinkedRentalPostExistsAsync(request.LinkedRentalPostId, cancellationToken);
        post.Update(
            request.Title!,
            request.Description!,
            request.Price,
            request.Condition!,
            request.Category!,
            request.Address!,
            request.Latitude,
            request.Longitude,
            request.LinkedRentalPostId,
            request.MediaUrls,
            _timeProvider.GetUtcNow(),
            request.ListingType,
            request.AvailableQuantity,
            request.Unit ?? "món",
            request.PreparationMinutes);
        await _marketplacePosts.SaveChangesAsync(cancellationToken);
        var seller = await _profiles.GetByIdAsync(post.SellerId, cancellationToken);
        return ToDto(post, seller, null, null);
    }

    public async Task MarkSoldAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var post = await GetOwnedPostAsync(id, cancellationToken);
        post.MarkSold(_timeProvider.GetUtcNow());
        await _marketplacePosts.SaveChangesAsync(cancellationToken);
    }

    public async Task ArchiveAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var post = await GetOwnedPostAsync(id, cancellationToken);
        post.Archive(_timeProvider.GetUtcNow());
        await _marketplacePosts.SaveChangesAsync(cancellationToken);
    }

    private async Task<MarketplacePost> GetOwnedPostAsync(Guid id, CancellationToken cancellationToken)
    {
        var userId = _userContext.GetRequiredUserId();
        var post = await _marketplacePosts.GetByIdWithMediaAsync(id, cancellationToken)
            ?? throw new NotFoundException(nameof(MarketplacePost), id);
        UserContext.EnsureOwner(userId, post.SellerId);
        return post;
    }

    private async Task ValidateAsync(UpsertMarketplacePostDto request, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        AddRequiredError(errors, "title", request.Title, MarketplacePost.MaxTitleLength);
        AddRequiredError(errors, "description", request.Description, MarketplacePost.MaxDescriptionLength);
        AddRequiredError(errors, "condition", request.Condition, MarketplacePost.MaxConditionLength);
        AddRequiredError(errors, "category", request.Category, MarketplacePost.MaxCategoryLength);
        AddRequiredError(errors, "address", request.Address, MarketplacePost.MaxAddressLength);
        if (request.Price <= 0 || decimal.Truncate(request.Price) != request.Price)
        {
            errors["price"] = ["Price must be a positive whole VND value."];
        }

        if (request.ListingType == MarketplaceListingType.Food && request.Price < _financeOptions.MinimumFoodPrice)
        {
            errors["price"] = [$"Food price must be at least {_financeOptions.MinimumFoodPrice:0} VND."];
        }

        var maxQuantity = request.ListingType == MarketplaceListingType.Food ? MarketplacePost.MaxFoodStock : 1;
        if (request.AvailableQuantity is < 1 || request.AvailableQuantity > maxQuantity)
        {
            errors["availableQuantity"] = [$"Available quantity must be between 1 and {maxQuantity}."];
        }

        AddRequiredError(errors, "unit", request.Unit, MarketplacePost.MaxUnitLength);
        if (request.PreparationMinutes is < 0 or > 240)
        {
            errors["preparationMinutes"] = ["Preparation time must be between 0 and 240 minutes."];
        }

        if (request.Latitude is < -90 or > 90)
        {
            errors["latitude"] = ["Latitude must be between -90 and 90."];
        }

        if (request.Longitude is < -180 or > 180)
        {
            errors["longitude"] = ["Longitude must be between -180 and 180."];
        }

        if (request.MediaUrls is null
            || request.MediaUrls.Count is < 1 or > MarketplacePost.MaxMediaCount)
        {
            errors["mediaUrls"] = [$"Provide between 1 and {MarketplacePost.MaxMediaCount} images."];
        }

        if (errors.Count > 0)
        {
            throw new RequestValidationException(errors);
        }

        var violations = await _moderation.ValidateAsync(
            $"{request.Title} {request.Description}",
            cancellationToken);
        if (violations.Count > 0)
        {
            throw new RequestValidationException(new Dictionary<string, string[]>
            {
                ["description"] = violations.ToArray(),
            });
        }
    }

    private async Task EnsureLinkedRentalPostExistsAsync(Guid? rentalPostId, CancellationToken cancellationToken)
    {
        if (rentalPostId.HasValue
            && await _rentalPosts.GetByIdAsync(rentalPostId.Value, cancellationToken) is null)
        {
            throw new NotFoundException(nameof(RentalPost), rentalPostId.Value);
        }
    }

    private async Task<NormalizedSearch> NormalizeSearchAsync(
        MarketplaceSearchDto request,
        CancellationToken cancellationToken)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, MaxPageSize);
        if (request.MinPrice is < 0 || request.MaxPrice is < 0 || request.MinPrice > request.MaxPrice)
        {
            throw new RequestValidationException(new Dictionary<string, string[]>
            {
                ["price"] = ["Price range is invalid."],
            });
        }

        decimal? latitude = request.Latitude;
        decimal? longitude = request.Longitude;
        if (request.NearRentalPostId.HasValue)
        {
            var rentalPost = await _rentalPosts.GetByIdAsync(request.NearRentalPostId.Value, cancellationToken)
                ?? throw new NotFoundException(nameof(RentalPost), request.NearRentalPostId.Value);
            if (rentalPost.Status != RentalPostStatus.Active)
            {
                throw new NotFoundException(nameof(RentalPost), request.NearRentalPostId.Value);
            }

            latitude = rentalPost.Latitude;
            longitude = rentalPost.Longitude;
        }

        if (latitude is null != longitude is null)
        {
            throw new RequestValidationException(new Dictionary<string, string[]>
            {
                ["location"] = ["Latitude and longitude must be provided together."],
            });
        }

        decimal? minLatitude = null;
        decimal? maxLatitude = null;
        decimal? minLongitude = null;
        decimal? maxLongitude = null;
        if (latitude.HasValue && longitude.HasValue)
        {
            if (latitude is < -90 or > 90 || longitude is < -180 or > 180)
            {
                throw new RequestValidationException(new Dictionary<string, string[]>
                {
                    ["location"] = ["Coordinates are out of range."],
                });
            }

            var radiusKm = Math.Clamp(request.RadiusKm ?? DefaultRadiusKm, 0.1m, MaxRadiusKm);
            var latitudeDelta = radiusKm / 111m;
            var cosine = Math.Max(0.01, Math.Cos((double)latitude.Value * Math.PI / 180));
            var longitudeDelta = radiusKm / (111m * (decimal)cosine);
            minLatitude = latitude.Value - latitudeDelta;
            maxLatitude = latitude.Value + latitudeDelta;
            minLongitude = longitude.Value - longitudeDelta;
            maxLongitude = longitude.Value + longitudeDelta;
        }

        return new NormalizedSearch(
            request.Keyword?.Trim(),
            request.Category?.Trim(),
            request.MinPrice,
            request.MaxPrice,
            latitude,
            longitude,
            minLatitude,
            maxLatitude,
            minLongitude,
            maxLongitude,
            page,
            pageSize);
    }

    private static MarketplacePostDto ToDto(
        MarketplacePost post,
        UserProfile? seller,
        decimal? centerLatitude,
        decimal? centerLongitude)
    {
        decimal? distanceKm = centerLatitude.HasValue && centerLongitude.HasValue
            ? CalculateDistanceKm(centerLatitude.Value, centerLongitude.Value, post.Latitude, post.Longitude)
            : null;
        return new MarketplacePostDto(
            post.Id,
            post.SellerId,
            seller?.DisplayName ?? "Homeji user",
            seller?.Phone,
            post.Status,
            post.Title,
            post.Description,
            post.Price,
            post.Condition,
            post.Category,
            post.Address,
            post.Latitude,
            post.Longitude,
            post.LinkedRentalPostId,
            post.Media.OrderBy(media => media.SortOrder).Select(media => media.Url).ToArray(),
            distanceKm,
            post.CreatedAt,
            post.UpdatedAt,
            post.ListingType,
            post.AvailableQuantity,
            post.ReservedQuantity,
            post.Unit,
            post.PreparationMinutes);
    }

    private static decimal CalculateDistanceKm(decimal lat1, decimal lon1, decimal lat2, decimal lon2)
    {
        const double earthRadiusKm = 6371;
        var latitudeDelta = DegreesToRadians((double)(lat2 - lat1));
        var longitudeDelta = DegreesToRadians((double)(lon2 - lon1));
        var firstLatitude = DegreesToRadians((double)lat1);
        var secondLatitude = DegreesToRadians((double)lat2);
        var a = (Math.Sin(latitudeDelta / 2) * Math.Sin(latitudeDelta / 2))
            + (Math.Cos(firstLatitude) * Math.Cos(secondLatitude)
                * Math.Sin(longitudeDelta / 2) * Math.Sin(longitudeDelta / 2));
        var distance = earthRadiusKm * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return Math.Round((decimal)distance, 2);
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180;

    private static void AddRequiredError(
        Dictionary<string, string[]> errors,
        string field,
        string? value,
        int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors[field] = [$"{field} is required."];
        }
        else if (value.Trim().Length > maxLength)
        {
            errors[field] = [$"{field} must not exceed {maxLength} characters."];
        }
    }

    private async Task EnsureSellerFundedAsync(Guid sellerId, CancellationToken cancellationToken)
    {
        var wallet = await _wallets.GetAsync(sellerId, cancellationToken);
        if (wallet is null || !wallet.IsActivated || wallet.Balance < _financeOptions.SellerReserve)
        {
            var missing = _financeOptions.SellerReserve - (wallet?.Balance ?? 0);
            throw new RequestValidationException(new Dictionary<string, string[]>
            {
                ["wallet"] = [$"Nạp thêm ít nhất {Math.Max(0, missing):0} đồng để duy trì quỹ đảm bảo người bán."],
            });
        }
    }

    private sealed record NormalizedSearch(
        string? Keyword,
        string? Category,
        decimal? MinPrice,
        decimal? MaxPrice,
        decimal? CenterLatitude,
        decimal? CenterLongitude,
        decimal? MinLatitude,
        decimal? MaxLatitude,
        decimal? MinLongitude,
        decimal? MaxLongitude,
        int Page,
        int PageSize);
}
