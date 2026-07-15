using Homeji.Application.Abstractions.Notifications;
using Homeji.Application.Common.Exceptions;
using Homeji.Application.DTOs.MarketplaceOrders;
using Homeji.Application.IRepositories.Marketplace;
using Homeji.Application.IRepositories.MarketplaceOrders;
using Homeji.Application.IRepositories.Notifications;
using Homeji.Application.IRepositories.Wallets;
using Homeji.Application.IServices.Marketplace;
using Homeji.Application.IServices.MarketplaceOrders;
using Homeji.Application.Services.Common;
using Homeji.Domain.Entities;
using Homeji.Domain.Enums;
using FluentValidation;
using Microsoft.Extensions.Options;
using Homeji.Application.Services.Marketplace;

namespace Homeji.Application.Services.MarketplaceOrders;

public sealed class MarketplaceOrderService : IMarketplaceOrderService
{
    private readonly UserContext _userContext;
    private readonly IMarketplaceOrderRepository _orders;
    private readonly IMarketplacePostRepository _posts;
    private readonly INotificationRepository _notifications;
    private readonly INotificationRealtimePublisher _realtimePublisher;
    private readonly TimeProvider _timeProvider;
    private readonly IWalletRepository _wallets;
    private readonly IMarketplaceSellerPlanService _sellerPlans;
    private readonly IValidator<CreateMarketplaceOrderDto> _validator;
    private readonly MarketplaceFinanceOptions _financeOptions;

    public MarketplaceOrderService(
        UserContext userContext,
        IMarketplaceOrderRepository orders,
        IMarketplacePostRepository posts,
        INotificationRepository notifications,
        INotificationRealtimePublisher realtimePublisher,
        TimeProvider timeProvider,
        IWalletRepository wallets,
        IMarketplaceSellerPlanService sellerPlans,
        IValidator<CreateMarketplaceOrderDto> validator,
        IOptions<MarketplaceFinanceOptions> financeOptions)
    {
        _userContext = userContext;
        _orders = orders;
        _posts = posts;
        _notifications = notifications;
        _realtimePublisher = realtimePublisher;
        _timeProvider = timeProvider;
        _wallets = wallets;
        _sellerPlans = sellerPlans;
        _validator = validator;
        _financeOptions = financeOptions.Value;
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
                ["postId"] = ["You already have an active purchase request for this item."],
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

        var commissionRate = await _sellerPlans.ResolveCommissionRateAsync(post.SellerId, now, cancellationToken);
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

    public async Task<IReadOnlyList<MarketplaceOrderDto>> GetMineAsync(CancellationToken cancellationToken = default)
    {
        var orders = await _orders.GetForUserAsync(_userContext.GetRequiredUserId(), cancellationToken);
        return orders.Select(ToDto).ToArray();
    }

    public Task<MarketplaceOrderDto> AcceptAsync(Guid id, CancellationToken cancellationToken = default) =>
        UpdateAsync(id, MarketplaceOrderStatus.Accepted, cancellationToken);

    public Task<MarketplaceOrderDto> RejectAsync(Guid id, CancellationToken cancellationToken = default) =>
        UpdateAsync(id, MarketplaceOrderStatus.Rejected, cancellationToken);

    public Task<MarketplaceOrderDto> CancelAsync(Guid id, CancellationToken cancellationToken = default) =>
        UpdateAsync(id, MarketplaceOrderStatus.Cancelled, cancellationToken);

    public Task<MarketplaceOrderDto> CompleteAsync(Guid id, CancellationToken cancellationToken = default) =>
        UpdateAsync(id, MarketplaceOrderStatus.Completed, cancellationToken);

    private async Task<MarketplaceOrderDto> UpdateAsync(
        Guid id,
        MarketplaceOrderStatus target,
        CancellationToken cancellationToken)
    {
        var userId = _userContext.GetRequiredUserId();
        var order = await _orders.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException(nameof(MarketplaceOrder), id);
        if (target == MarketplaceOrderStatus.Cancelled)
        {
            UserContext.EnsureOwner(userId, order.BuyerId);
            var now = _timeProvider.GetUtcNow();
            order.Cancel(now);
            await RefundAndReleaseStockAsync(order, now, cancellationToken);
        }
        else if (target is MarketplaceOrderStatus.Accepted or MarketplaceOrderStatus.Rejected)
        {
            UserContext.EnsureOwner(userId, order.SellerId);
            var now = _timeProvider.GetUtcNow();
            if (target == MarketplaceOrderStatus.Accepted)
            {
                var sellerWallet = await _wallets.GetAsync(order.SellerId, cancellationToken);
                if (sellerWallet is null || !sellerWallet.IsActivated || sellerWallet.Balance < _financeOptions.SellerReserve)
                {
                    throw WalletFundingRequired("Người bán phải duy trì quỹ đảm bảo trước khi nhận đơn.");
                }

                order.Accept(now);
            }
            else
            {
                order.Reject(now);
                await RefundAndReleaseStockAsync(order, now, cancellationToken);
            }
        }
        else
        {
            UserContext.EnsureOwner(userId, order.BuyerId);
            var now = _timeProvider.GetUtcNow();
            order.Complete(now);
            await ReleaseSaleProceedsAsync(order, now, cancellationToken);
        }

        var recipientId = userId == order.BuyerId ? order.SellerId : order.BuyerId;
        var notification = BuildNotification(order, recipientId, _timeProvider.GetUtcNow());
        await _notifications.AddAsync(notification, cancellationToken);
        await _orders.SaveChangesAsync(cancellationToken);
        await _realtimePublisher.PublishAsync(notification, cancellationToken);
        return ToDto(order);
    }

    private static Notification BuildNotification(MarketplaceOrder order, Guid recipientId, DateTimeOffset now) =>
        new(
            recipientId,
            NotificationType.MarketplaceOrderUpdated,
            "Đơn Chợ Homeji đã cập nhật",
            $"Đơn hàng hiện ở trạng thái {order.Status}.",
            order.Id,
            now);

    private static MarketplaceOrderDto ToDto(MarketplaceOrder order) =>
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
            order.FundsReleasedAt,
            order.RefundedAt);

    private async Task RefundAndReleaseStockAsync(
        MarketplaceOrder order,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var buyerWallet = await _wallets.GetAsync(order.BuyerId, cancellationToken)
            ?? throw new InvalidOperationException("Buyer wallet was not found for a funded marketplace order.");
        var post = await _posts.GetByIdWithMediaAsync(order.MarketplacePostId, cancellationToken)
            ?? throw new NotFoundException(nameof(MarketplacePost), order.MarketplacePostId);
        buyerWallet.CreditRefund(order.AgreedPrice, now);
        post.ReleaseReservation(order.Quantity, now);
        order.MarkRefunded(now);
        await _wallets.AddTransactionAsync(new WalletTransaction(
            order.BuyerId,
            WalletTransactionKind.Refund,
            order.AgreedPrice,
            buyerWallet.Balance,
            order.Id,
            "Hoàn tiền đơn chợ Homeji",
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
        var balanceBefore = sellerWallet.Balance;
        sellerWallet.CreditSale(order.AgreedPrice, order.PlatformFeeAmount, now);
        post.CompleteReservation(order.Quantity, now);
        order.MarkFundsReleased(now);
        await _wallets.AddTransactionAsync(new WalletTransaction(
            order.SellerId,
            WalletTransactionKind.SaleProceeds,
            order.AgreedPrice,
            balanceBefore + order.AgreedPrice,
            order.Id,
            "Doanh thu đơn chợ Homeji",
            now), cancellationToken);
        await _wallets.AddTransactionAsync(new WalletTransaction(
            order.SellerId,
            WalletTransactionKind.PlatformFee,
            -order.PlatformFeeAmount,
            sellerWallet.Balance,
            order.Id,
            $"Phí nền tảng {order.PlatformFeeRate:P0}",
            now), cancellationToken);
    }

    private static RequestValidationException WalletFundingRequired(string message) =>
        new(new Dictionary<string, string[]> { ["wallet"] = [message] });
}
