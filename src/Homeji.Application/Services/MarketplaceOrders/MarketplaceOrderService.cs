using Homeji.Application.Abstractions.Notifications;
using Homeji.Application.Common.Exceptions;
using Homeji.Application.DTOs.MarketplaceOrders;
using Homeji.Application.IRepositories.Marketplace;
using Homeji.Application.IRepositories.MarketplaceOrders;
using Homeji.Application.IRepositories.Notifications;
using Homeji.Application.IServices.MarketplaceOrders;
using Homeji.Application.Services.Common;
using Homeji.Domain.Entities;
using Homeji.Domain.Enums;

namespace Homeji.Application.Services.MarketplaceOrders;

public sealed class MarketplaceOrderService : IMarketplaceOrderService
{
    private readonly UserContext _userContext;
    private readonly IMarketplaceOrderRepository _orders;
    private readonly IMarketplacePostRepository _posts;
    private readonly INotificationRepository _notifications;
    private readonly INotificationRealtimePublisher _realtimePublisher;
    private readonly TimeProvider _timeProvider;

    public MarketplaceOrderService(
        UserContext userContext,
        IMarketplaceOrderRepository orders,
        IMarketplacePostRepository posts,
        INotificationRepository notifications,
        INotificationRealtimePublisher realtimePublisher,
        TimeProvider timeProvider)
    {
        _userContext = userContext;
        _orders = orders;
        _posts = posts;
        _notifications = notifications;
        _realtimePublisher = realtimePublisher;
        _timeProvider = timeProvider;
    }

    public async Task<MarketplaceOrderDto> CreateAsync(
        Guid postId,
        CreateMarketplaceOrderDto request,
        CancellationToken cancellationToken = default)
    {
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
        var order = new MarketplaceOrder(
            post.Id,
            buyerId,
            post.SellerId,
            post.Price,
            request.PickupAt,
            request.PickupAddress!,
            request.Note,
            now);
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
            order.Cancel(_timeProvider.GetUtcNow());
        }
        else
        {
            UserContext.EnsureOwner(userId, order.SellerId);
            if (target == MarketplaceOrderStatus.Accepted) order.Accept(_timeProvider.GetUtcNow());
            else if (target == MarketplaceOrderStatus.Rejected) order.Reject(_timeProvider.GetUtcNow());
            else
            {
                var now = _timeProvider.GetUtcNow();
                order.Complete(now);
                var post = await _posts.GetByIdWithMediaAsync(order.MarketplacePostId, cancellationToken)
                    ?? throw new NotFoundException(nameof(MarketplacePost), order.MarketplacePostId);
                post.MarkSold(now);
            }
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
            "Yêu cầu mua đồ pass đã cập nhật",
            $"Yêu cầu nhận đồ hiện ở trạng thái {order.Status}.",
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
            order.UpdatedAt);
}
