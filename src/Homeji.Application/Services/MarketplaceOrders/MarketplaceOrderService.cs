using Homeji.Application.Abstractions.Notifications;
using Homeji.Application.Common.Exceptions;
using Homeji.Application.DTOs.MarketplaceOrders;
using Homeji.Application.IRepositories.Marketplace;
using Homeji.Application.IRepositories.MarketplaceOrders;
using Homeji.Application.IRepositories.Notifications;
using Homeji.Application.IRepositories.Wallets;
using Homeji.Application.IServices.MarketplaceOrders;
using Homeji.Application.Services.Common;
using Homeji.Domain.Entities;
using Homeji.Domain.Enums;
using Homeji.Domain.Exceptions;
using FluentValidation;
using Microsoft.Extensions.Options;
using Homeji.Application.Services.Marketplace;
using Homeji.Application.IRepositories.Profiles;

namespace Homeji.Application.Services.MarketplaceOrders;

public sealed class MarketplaceOrderService : IMarketplaceOrderService, IMarketplaceOrderExpirationService
{
    private const int ExpirationBatchSize = 200;
    private readonly UserContext _userContext;
    private readonly IMarketplaceOrderRepository _orders;
    private readonly IMarketplacePostRepository _posts;
    private readonly INotificationRepository _notifications;
    private readonly INotificationRealtimePublisher _realtimePublisher;
    private readonly TimeProvider _timeProvider;
    private readonly IWalletRepository _wallets;
    private readonly IValidator<CreateMarketplaceOrderDto> _validator;
    private readonly IValidator<CreateMarketplaceCartOrderDto> _cartValidator;
    private readonly MarketplaceFinanceOptions _financeOptions;
    private readonly IUserProfileRepository _profiles;

    public MarketplaceOrderService(
        UserContext userContext,
        IMarketplaceOrderRepository orders,
        IMarketplacePostRepository posts,
        INotificationRepository notifications,
        INotificationRealtimePublisher realtimePublisher,
        TimeProvider timeProvider,
        IWalletRepository wallets,
        IValidator<CreateMarketplaceOrderDto> validator,
        IValidator<CreateMarketplaceCartOrderDto> cartValidator,
        IOptions<MarketplaceFinanceOptions> financeOptions,
        IUserProfileRepository profiles)
    {
        _userContext = userContext;
        _orders = orders;
        _posts = posts;
        _notifications = notifications;
        _realtimePublisher = realtimePublisher;
        _timeProvider = timeProvider;
        _wallets = wallets;
        _validator = validator;
        _cartValidator = cartValidator;
        _financeOptions = financeOptions.Value;
        _profiles = profiles;
    }

    public async Task<MarketplaceOrderDto> CreateAsync(
        Guid postId,
        CreateMarketplaceOrderDto request,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await _validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            throw new RequestValidationException(validationResult.Errors
                .GroupBy(error => error.PropertyName, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(error => error.ErrorMessage).Distinct().ToArray(),
                    StringComparer.Ordinal));
        }

        var buyerId = _userContext.GetRequiredUserId();
        var post = await _posts.GetByIdWithMediaAsync(postId, cancellationToken)
            ?? throw new NotFoundException(nameof(MarketplacePost), postId);
        if (post.Status != MarketplacePostStatus.Active)
        {
            throw new NotFoundException(nameof(MarketplacePost), postId);
        }

        if (await _orders.HasActiveAsync(postId, buyerId, cancellationToken))
        {
            throw new RequestValidationException(new Dictionary<string, string[]>
            {
                ["postId"] = ["Bạn đã có yêu cầu mua đang hoạt động cho sản phẩm này."],
            });
        }

        var now = _timeProvider.GetUtcNow();
        var grossAmount = post.Price * request.Quantity;
        if (post.ListingType == MarketplaceListingType.Food && grossAmount < _financeOptions.MinimumFoodOrder)
        {
            throw new RequestValidationException(new Dictionary<string, string[]>
            {
                ["quantity"] = [$"Food order total must be at least {_financeOptions.MinimumFoodOrder:0} VND."],
            });
        }

        var buyerWallet = await _wallets.GetAsync(buyerId, cancellationToken);
        if (buyerWallet is null || !buyerWallet.IsActivated || buyerWallet.Balance < grossAmount)
        {
            var missing = grossAmount - (buyerWallet?.Balance ?? 0);
            throw WalletFundingRequired($"Nạp thêm ít nhất {Math.Max(0, missing):0} đồng để thanh toán đơn hàng.");
        }

        var sellerWallet = await _wallets.GetAsync(post.SellerId, cancellationToken);
        if (sellerWallet is null || !sellerWallet.IsActivated || sellerWallet.Balance < _financeOptions.SellerReserve)
        {
            throw new RequestValidationException(new Dictionary<string, string[]>
            {
                ["sellerWallet"] = ["Người bán chưa duy trì đủ quỹ đảm bảo để nhận đơn."],
            });
        }

        var commissionRate = _financeOptions.CommissionRate;
        var order = new MarketplaceOrder(
            post.Id,
            buyerId,
            post.SellerId,
            post.Price,
            request.PickupAt,
            request.PickupAddress!,
            request.Note,
            now,
            request.Quantity,
            commissionRate);
        post.Reserve(request.Quantity, now);
        buyerWallet.DebitPurchase(order.AgreedPrice, now);
        await _wallets.AddTransactionAsync(new WalletTransaction(
            buyerId,
            WalletTransactionKind.Purchase,
            -order.AgreedPrice,
            buyerWallet.Balance,
            order.Id,
            $"Thanh toán đơn {post.Title}",
            now), cancellationToken);
        var notification = BuildNotification(order, post.SellerId, now);
        await _orders.AddAsync(order, cancellationToken);
        await _notifications.AddAsync(notification, cancellationToken);
        await _orders.SaveChangesAsync(cancellationToken);
        await _realtimePublisher.PublishAsync(notification, cancellationToken);
        return ToDto(order);
    }

    public async Task<IReadOnlyList<MarketplaceOrderDto>> CreateCartAsync(
        CreateMarketplaceCartOrderDto request,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await _cartValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            throw new RequestValidationException(validationResult.Errors
                .GroupBy(error => error.PropertyName, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(error => error.ErrorMessage).Distinct().ToArray(),
                    StringComparer.Ordinal));
        }

        var buyerId = _userContext.GetRequiredUserId();
        var itemByPostId = request.Items.ToDictionary(item => item.PostId);
        var postIds = itemByPostId.Keys.ToArray();
        var posts = await _posts.GetByIdsForUpdateAsync(postIds, cancellationToken);
        if (posts.Count != postIds.Length
            || posts.Any(post => post.Status != MarketplacePostStatus.Active
                || post.ListingType != MarketplaceListingType.Food))
        {
            throw new RequestValidationException(new Dictionary<string, string[]>
            {
                ["items"] = ["Một hoặc nhiều món không còn được bán."],
            });
        }

        var sellerIds = posts.Select(post => post.SellerId).Distinct().ToArray();
        if (sellerIds.Length != 1)
        {
            throw new RequestValidationException(new Dictionary<string, string[]>
            {
                ["items"] = ["Mỗi lần thanh toán chỉ áp dụng cho món của cùng một bếp."],
            });
        }

        var sellerId = sellerIds[0];
        if (sellerId == buyerId)
        {
            throw new RequestValidationException(new Dictionary<string, string[]>
            {
                ["items"] = ["Bạn không thể mua món do chính mình đăng bán."],
            });
        }

        var activePostIds = await _orders.GetActivePostIdsAsync(postIds, buyerId, cancellationToken);
        if (activePostIds.Count > 0)
        {
            throw new RequestValidationException(new Dictionary<string, string[]>
            {
                ["items"] = ["Giỏ có món đang nằm trong một yêu cầu mua chưa hoàn tất."],
            });
        }

        var grossAmount = posts.Sum(post => post.Price * itemByPostId[post.Id].Quantity);
        if (grossAmount < _financeOptions.MinimumFoodOrder)
        {
            throw new RequestValidationException(new Dictionary<string, string[]>
            {
                ["items"] = [$"Tổng giỏ hàng tối thiểu {_financeOptions.MinimumFoodOrder:0} đồng."],
            });
        }

        var buyerWallet = await _wallets.GetAsync(buyerId, cancellationToken);
        if (buyerWallet is null || !buyerWallet.IsActivated || buyerWallet.Balance < grossAmount)
        {
            var missing = grossAmount - (buyerWallet?.Balance ?? 0);
            throw WalletFundingRequired($"Nạp thêm ít nhất {Math.Max(0, missing):0} đồng để thanh toán giỏ hàng.");
        }

        var sellerWallet = await _wallets.GetAsync(sellerId, cancellationToken);
        if (sellerWallet is null || !sellerWallet.IsActivated || sellerWallet.Balance < _financeOptions.SellerReserve)
        {
            throw new RequestValidationException(new Dictionary<string, string[]>
            {
                ["sellerWallet"] = ["Người bán chưa duy trì đủ quỹ đảm bảo để nhận đơn."],
            });
        }

        var now = _timeProvider.GetUtcNow();
        var commissionRate = _financeOptions.CommissionRate;
        var createdOrders = new List<(MarketplaceOrder Order, MarketplacePost Post)>(posts.Count);
        var notifications = new List<Notification>(posts.Count);
        foreach (var post in posts)
        {
            var item = itemByPostId[post.Id];
            var order = new MarketplaceOrder(
                post.Id,
                buyerId,
                sellerId,
                post.Price,
                request.PickupAt,
                request.PickupAddress!,
                request.Note,
                now,
                item.Quantity,
                commissionRate);
            post.Reserve(item.Quantity, now);
            buyerWallet.DebitPurchase(order.AgreedPrice, now);
            await _wallets.AddTransactionAsync(new WalletTransaction(
                buyerId,
                WalletTransactionKind.Purchase,
                -order.AgreedPrice,
                buyerWallet.Balance,
                order.Id,
                $"Thanh toán món {post.Title}",
                now), cancellationToken);
            await _orders.AddAsync(order, cancellationToken);
            var notification = BuildNotification(order, sellerId, now);
            notifications.Add(notification);
            createdOrders.Add((order, post));
        }

        await _notifications.AddRangeAsync(notifications, cancellationToken);
        await _orders.SaveChangesAsync(cancellationToken);
        foreach (var notification in notifications)
        {
            await _realtimePublisher.PublishAsync(notification, cancellationToken);
        }

        return createdOrders.Select(item => ToDto(item.Order, item.Post)).ToArray();
    }

    public async Task<IReadOnlyList<MarketplaceOrderDto>> GetMineAsync(CancellationToken cancellationToken = default)
    {
        var requestedCutoff = _timeProvider.GetUtcNow()
            .AddMinutes(-_financeOptions.OrderRequestTimeoutMinutes);
        var orders = await _orders.GetForUserAsync(
            _userContext.GetRequiredUserId(),
            requestedCutoff,
            cancellationToken);
        var posts = await _posts.GetByIdsAsync(
            orders.Select(order => order.MarketplacePostId).Distinct().ToArray(),
            cancellationToken);
        var postsById = posts.ToDictionary(post => post.Id);
        var profiles = await _profiles.GetByIdsAsync(
            orders.SelectMany(order => new[] { order.BuyerId, order.SellerId }).Distinct().ToArray(),
            cancellationToken);
        var profilesById = profiles.ToDictionary(profile => profile.Id);
        return orders.Select(order => ToDto(
            order,
            postsById.GetValueOrDefault(order.MarketplacePostId),
            profilesById.GetValueOrDefault(order.BuyerId)?.DisplayName,
            profilesById.GetValueOrDefault(order.SellerId)?.DisplayName)).ToArray();
    }

    public async Task<int> ExpireOverdueAsync(CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow();
        var cutoff = now.AddMinutes(-_financeOptions.OrderRequestTimeoutMinutes);
        var overdueOrders = await _orders.GetExpiredRequestedAsync(
            cutoff,
            ExpirationBatchSize,
            cancellationToken);
        if (overdueOrders.Count == 0)
        {
            return 0;
        }

        var notifications = new List<Notification>(overdueOrders.Count * 2);
        var expiredOrderCount = 0;
        foreach (var overdueGroup in overdueOrders.GroupBy(order => new
                 {
                     order.BuyerId,
                     order.SellerId,
                     order.CreatedAt,
                 }))
        {
            var orderGroup = await _orders.GetGroupByIdAsync(overdueGroup.First().Id, cancellationToken);
            if (orderGroup.Count == 0
                || orderGroup.Any(order => order.Status != MarketplaceOrderStatus.Requested
                    || order.CreatedAt > cutoff))
            {
                continue;
            }

            foreach (var order in orderGroup)
            {
                order.Expire(now);
                notifications.AddRange(BuildExpirationNotifications(
                    order,
                    now,
                    _financeOptions.OrderRequestTimeoutMinutes));
            }

            await RefundOrderGroupAndReleaseStockAsync(orderGroup, now, cancellationToken);
            expiredOrderCount += orderGroup.Count;
        }

        await _notifications.AddRangeAsync(notifications, cancellationToken);
        await _orders.SaveChangesAsync(cancellationToken);
        foreach (var notification in notifications)
        {
            await _realtimePublisher.PublishAsync(notification, cancellationToken);
        }

        return expiredOrderCount;
    }

    public async Task<int> ReleaseOverdueFundsAsync(CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow();
        var deliveredCutoff = now.AddHours(-_financeOptions.EscrowHoldHours);
        var dueOrders = await _orders.GetFundsReleaseDueAsync(
            deliveredCutoff,
            ExpirationBatchSize,
            cancellationToken);
        if (dueOrders.Count == 0)
        {
            return 0;
        }

        var notifications = new List<Notification>(dueOrders.Count * 2);
        var releasedCount = 0;
        foreach (var dueGroup in dueOrders.GroupBy(order => new
                 {
                     order.BuyerId,
                     order.SellerId,
                     order.CreatedAt,
                 }))
        {
            var orderGroup = await _orders.GetGroupByIdAsync(dueGroup.First().Id, cancellationToken);
            if (orderGroup.Count == 0
                || orderGroup.Any(order => order.FundsReleasedAt.HasValue
                    || !order.DeliveredAt.HasValue
                    || order.DeliveredAt > deliveredCutoff
                    || order.Status is not (MarketplaceOrderStatus.Delivered or MarketplaceOrderStatus.Completed)))
            {
                continue;
            }

            foreach (var groupOrder in orderGroup)
            {
                groupOrder.AutoComplete(now);
                await ReleaseSaleProceedsAsync(groupOrder, now, cancellationToken);
                notifications.AddRange(BuildFundsReleasedNotifications(
                    groupOrder,
                    now,
                    _financeOptions.EscrowHoldHours));
            }

            releasedCount += orderGroup.Count;
        }

        await _notifications.AddRangeAsync(notifications, cancellationToken);
        await _orders.SaveChangesAsync(cancellationToken);
        foreach (var notification in notifications)
        {
            await _realtimePublisher.PublishAsync(notification, cancellationToken);
        }

        return releasedCount;
    }

    public Task<MarketplaceOrderDto> AcceptAsync(Guid id, CancellationToken cancellationToken = default) =>
        UpdateAsync(id, MarketplaceOrderStatus.Accepted, cancellationToken);

    public Task<MarketplaceOrderDto> RejectAsync(Guid id, CancellationToken cancellationToken = default) =>
        UpdateAsync(id, MarketplaceOrderStatus.Rejected, cancellationToken);

    public Task<MarketplaceOrderDto> CancelAsync(Guid id, CancellationToken cancellationToken = default) =>
        UpdateAsync(id, MarketplaceOrderStatus.Cancelled, cancellationToken);

    public Task<MarketplaceOrderDto> MarkDeliveredAsync(Guid id, CancellationToken cancellationToken = default) =>
        UpdateAsync(id, MarketplaceOrderStatus.Delivered, cancellationToken);

    public Task<MarketplaceOrderDto> CompleteAsync(Guid id, CancellationToken cancellationToken = default) =>
        UpdateAsync(id, MarketplaceOrderStatus.Completed, cancellationToken);

    private async Task<MarketplaceOrderDto> UpdateAsync(
        Guid id,
        MarketplaceOrderStatus target,
        CancellationToken cancellationToken)
    {
        var userId = _userContext.GetRequiredUserId();
        var orderGroup = await _orders.GetGroupByIdAsync(id, cancellationToken);
        if (orderGroup.Count == 0)
        {
            throw new NotFoundException(nameof(MarketplaceOrder), id);
        }

        var order = orderGroup.Single(groupOrder => groupOrder.Id == id);
        var currentTime = _timeProvider.GetUtcNow();
        if (orderGroup.All(groupOrder => groupOrder.Status == MarketplaceOrderStatus.Requested)
            && order.CreatedAt.AddMinutes(_financeOptions.OrderRequestTimeoutMinutes) <= currentTime)
        {
            var expirationNotifications = new List<Notification>(orderGroup.Count * 2);
            foreach (var groupOrder in orderGroup)
            {
                groupOrder.Expire(currentTime);
                expirationNotifications.AddRange(BuildExpirationNotifications(
                    groupOrder,
                    currentTime,
                    _financeOptions.OrderRequestTimeoutMinutes));
            }
            await RefundOrderGroupAndReleaseStockAsync(orderGroup, currentTime, cancellationToken);

            await _notifications.AddRangeAsync(expirationNotifications, cancellationToken);
            await _orders.SaveChangesAsync(cancellationToken);
            foreach (var expirationNotification in expirationNotifications)
            {
                await _realtimePublisher.PublishAsync(expirationNotification, cancellationToken);
            }

            throw new DomainException(
                $"Đơn hàng đã hết hạn sau {_financeOptions.OrderRequestTimeoutMinutes} phút chờ xác nhận và đã được hoàn tiền.");
        }

        if (target == MarketplaceOrderStatus.Cancelled)
        {
            UserContext.EnsureOwner(userId, order.BuyerId);
            EnsureGroupStatus(
                orderGroup,
                MarketplaceOrderStatus.Requested,
                "Chỉ có thể hủy đơn trước khi bếp xác nhận.");
            foreach (var groupOrder in orderGroup)
            {
                groupOrder.Cancel(currentTime);
            }
            await RefundOrderGroupAndReleaseStockAsync(orderGroup, currentTime, cancellationToken);
        }
        else if (target is MarketplaceOrderStatus.Accepted or MarketplaceOrderStatus.Rejected)
        {
            UserContext.EnsureOwner(userId, order.SellerId);
            EnsureGroupStatus(
                orderGroup,
                MarketplaceOrderStatus.Requested,
                "Chỉ có thể xử lý đơn khi toàn bộ món đang chờ xác nhận.");
            if (target == MarketplaceOrderStatus.Accepted)
            {
                var sellerWallet = await _wallets.GetAsync(order.SellerId, cancellationToken);
                if (sellerWallet is null || !sellerWallet.IsActivated || sellerWallet.Balance < _financeOptions.SellerReserve)
                {
                    throw WalletFundingRequired("Người bán phải duy trì quỹ đảm bảo trước khi nhận đơn.");
                }

                foreach (var groupOrder in orderGroup)
                {
                    groupOrder.Accept(currentTime);
                }
            }
            else
            {
                foreach (var groupOrder in orderGroup)
                {
                    groupOrder.Reject(currentTime);
                }
                await RefundOrderGroupAndReleaseStockAsync(orderGroup, currentTime, cancellationToken);
            }
        }
        else if (target == MarketplaceOrderStatus.Delivered)
        {
            UserContext.EnsureOwner(userId, order.SellerId);
            EnsureGroupStatus(
                orderGroup,
                MarketplaceOrderStatus.Accepted,
                "Chỉ có thể báo đã giao khi toàn bộ đơn đã được người bán nhận.");
            foreach (var groupOrder in orderGroup)
            {
                groupOrder.MarkDelivered(currentTime);
            }
        }
        else
        {
            UserContext.EnsureOwner(userId, order.BuyerId);
            EnsureGroupStatus(
                orderGroup,
                MarketplaceOrderStatus.Delivered,
                "Chỉ có thể xác nhận khi người bán đã báo giao toàn bộ đơn.");
            foreach (var groupOrder in orderGroup)
            {
                groupOrder.ConfirmReceived(currentTime);
            }
        }

        var notifications = orderGroup
            .Select(groupOrder => BuildNotification(
                groupOrder,
                userId == groupOrder.BuyerId ? groupOrder.SellerId : groupOrder.BuyerId,
                currentTime))
            .ToArray();
        await _notifications.AddRangeAsync(notifications, cancellationToken);
        await _orders.SaveChangesAsync(cancellationToken);
        foreach (var notification in notifications)
        {
            await _realtimePublisher.PublishAsync(notification, cancellationToken);
        }

        return ToDto(order);
    }

    private static void EnsureGroupStatus(
        IReadOnlyList<MarketplaceOrder> orderGroup,
        MarketplaceOrderStatus expectedStatus,
        string message)
    {
        if (orderGroup.Any(order => order.Status != expectedStatus))
        {
            throw new DomainException(message);
        }
    }

    private static Notification BuildNotification(MarketplaceOrder order, Guid recipientId, DateTimeOffset now) =>
        new(
            recipientId,
            NotificationType.MarketplaceOrderUpdated,
            "Đơn Chợ Homeji đã cập nhật",
            $"Đơn hàng hiện ở trạng thái {order.Status}.",
            order.Id,
            now);

    private static IReadOnlyList<Notification> BuildExpirationNotifications(
        MarketplaceOrder order,
        DateTimeOffset now,
        int timeoutMinutes) =>
        [
            new Notification(
                order.BuyerId,
                NotificationType.MarketplaceOrderUpdated,
                "Đơn Chợ Homeji đã hết hạn",
                $"Đơn chờ quá {timeoutMinutes} phút đã được hủy tự động và hoàn tiền vào Số dư Homeji.",
                order.Id,
                now),
            new Notification(
                order.SellerId,
                NotificationType.MarketplaceOrderUpdated,
                "Đơn Chợ Homeji đã hết hạn",
                $"Đơn chờ quá {timeoutMinutes} phút đã được hủy tự động và trả lại tồn kho.",
                order.Id,
                now),
        ];

    private static IReadOnlyList<Notification> BuildFundsReleasedNotifications(
        MarketplaceOrder order,
        DateTimeOffset now,
        int holdHours) =>
        [
            new Notification(
                order.BuyerId,
                NotificationType.MarketplaceOrderUpdated,
                "Đơn Chợ Homeji đã tự hoàn tất",
                $"Đơn đã qua thời gian đảm bảo {holdHours} giờ và được hoàn tất tự động.",
                order.Id,
                now),
            new Notification(
                order.SellerId,
                NotificationType.MarketplaceOrderUpdated,
                "Tiền bán hàng đã về ví",
                $"Tiền đơn hàng đã được giải ngân sau thời gian đảm bảo {holdHours} giờ.",
                order.Id,
                now),
        ];

    private MarketplaceOrderDto ToDto(
        MarketplaceOrder order,
        MarketplacePost? post = null,
        string? buyerDisplayName = null,
        string? sellerDisplayName = null) =>
        new(
            order.Id,
            order.MarketplacePostId,
            order.BuyerId,
            order.SellerId,
            order.AgreedPrice,
            order.PickupAt,
            order.PickupAddress,
            order.Note,
            order.Status,
            order.CreatedAt,
            order.UpdatedAt,
            order.UnitPrice,
            order.Quantity,
            order.PlatformFeeRate,
            order.PlatformFeeAmount,
            order.SellerNetAmount,
            order.DeliveredAt,
            order.DeliveredAt?.AddHours(_financeOptions.EscrowHoldHours),
            order.FundsReleasedAt,
            order.RefundedAt,
            post?.Title,
            post?.Media.OrderBy(media => media.SortOrder).Select(media => media.Url).FirstOrDefault(),
            buyerDisplayName,
            sellerDisplayName,
            post?.Address);

    private async Task RefundOrderGroupAndReleaseStockAsync(
        IReadOnlyList<MarketplaceOrder> orders,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (orders.Count == 0)
        {
            throw new InvalidOperationException("Marketplace order refund group must not be empty.");
        }

        var buyerId = orders[0].BuyerId;
        if (orders.Any(order => order.BuyerId != buyerId))
        {
            throw new InvalidOperationException("Marketplace order refund group must belong to one buyer.");
        }

        var buyerWallet = await _wallets.GetAsync(buyerId, cancellationToken)
            ?? throw new InvalidOperationException("Buyer wallet was not found for a funded marketplace order.");
        var refundAmount = orders.Sum(order => order.AgreedPrice);
        buyerWallet.CreditRefund(refundAmount, now);

        foreach (var order in orders)
        {
            var post = await _posts.GetByIdWithMediaAsync(order.MarketplacePostId, cancellationToken)
                ?? throw new NotFoundException(nameof(MarketplacePost), order.MarketplacePostId);
            post.ReleaseReservation(order.Quantity, now);
            order.MarkRefunded(now);
        }

        var referenceId = orders.MinBy(order => order.Id)!.Id;
        await _wallets.AddTransactionAsync(new WalletTransaction(
            buyerId,
            WalletTransactionKind.Refund,
            refundAmount,
            buyerWallet.Balance,
            referenceId,
            orders.Count == 1
                ? "Hoàn tiền đơn chợ Homeji"
                : $"Hoàn tổng đơn chợ Homeji · {orders.Count} món",
            now), cancellationToken);
    }

    private async Task ReleaseSaleProceedsAsync(
        MarketplaceOrder order,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var sellerWallet = await _wallets.GetAsync(order.SellerId, cancellationToken)
            ?? throw new InvalidOperationException("Seller wallet was not found for a funded marketplace order.");
        var post = await _posts.GetByIdWithMediaAsync(order.MarketplacePostId, cancellationToken)
            ?? throw new NotFoundException(nameof(MarketplacePost), order.MarketplacePostId);
        sellerWallet.CreditSaleProceeds(order.SellerNetAmount, now);
        post.CompleteReservation(order.Quantity, now);
        order.MarkFundsReleased(now);
        await _wallets.AddTransactionAsync(new WalletTransaction(
            order.SellerId,
            WalletTransactionKind.SaleProceeds,
            order.SellerNetAmount,
            sellerWallet.Balance,
            order.Id,
            $"Tiền bán hàng sau phí {order.PlatformFeeRate:P0} (đã trừ {order.PlatformFeeAmount:0} đồng)",
            now), cancellationToken);
    }

    private static RequestValidationException WalletFundingRequired(string message) =>
        new(new Dictionary<string, string[]> { ["wallet"] = [message] });
}
