using Homeji.Application.Common;
using Homeji.Application.Common.Exceptions;
using Homeji.Application.DTOs.AI;
using Homeji.Application.IRepositories.RentalPosts;
using Homeji.Application.IServices.AI;
using Homeji.Application.IServices.Admin;
using Homeji.Application.Mappers.RentalPosts;
using Homeji.Application.Services.Common;
using Homeji.Domain.Entities;
using Homeji.Domain.Enums;

namespace Homeji.Application.Services.AI;

public sealed class RentalDecisionService(IRentalPostRepository posts, UserContext userContext,
    ICommuteClient commuteClient, IAdminModerationService moderation, TimeProvider timeProvider) : IRentalDecisionService
{
    public async Task<RentalDecisionResponseDto> CompareAsync(RentalDecisionRequestDto request, CancellationToken cancellationToken)
    {
        _ = userContext.GetRequiredUserId();
        var ids = request.PostIds.Distinct().ToArray();
        if (ids.Length is < 1 or > 10 || ids.Contains(Guid.Empty))
            throw Validation("postIds", "Chọn từ 1 đến 10 tin cho danh sách ngắn.");
        var now = timeProvider.GetUtcNow();
        var found = await posts.GetByIdsWithMediaAsync(ids, cancellationToken);
        var active = found.Where(post => post.Status == RentalPostStatus.Active && !post.IsSynthetic
            && HomejiServiceArea.Contains(post.Latitude, post.Longitude)
            && !(post.Type == RentalPostType.RoomTransfer && post.OriginalLeaseEndsOn <= DateOnly.FromDateTime(now.UtcDateTime)))
            .ToDictionary(post => post.Id);
        IReadOnlyCollection<CommuteEstimateDto> routes = [];
        if (request.Destination is { } destination)
        {
            if (!HomejiServiceArea.Contains(destination.Latitude, destination.Longitude)
                || string.IsNullOrWhiteSpace(destination.Label) || destination.Label.Length > 200
                || destination.Mode is not ("DRIVE" or "WALK")
                || destination.DepartureTime.HasValue && (destination.DepartureTime < now || destination.DepartureTime > now.AddDays(7)))
                throw Validation("destination", "Chọn điểm đến trong phạm vi Homeji, đi bộ hoặc ô tô, và giờ đi trong 7 ngày tới.");
            routes = await commuteClient.ComputeAsync(active.Values.Select(post => new CommuteOriginDto(post.Id, post.Latitude, post.Longitude)).ToArray(), destination, cancellationToken);
        }
        var items = ids.Where(active.ContainsKey).Select(id => new RentalDecisionItemDto(
            RentalPostMapper.ToDto(active[id]), RentalCostCalculator.Calculate(active[id], request.Scenario ?? new()),
            routes.FirstOrDefault(route => route.PostId == id))).ToArray();
        var summary = items.Length > 1
            ? $"Chênh lệch giá thuê giữa các tin: {items.Max(item => item.Post.Price) - items.Min(item => item.Post.Price):N0} đồng/tháng. So sánh thêm diện tích và tiện ích theo nhu cầu; phí chưa đủ dữ liệu để kết luận phòng nào có tổng chi phí thấp nhất."
            : "Dữ liệu theo chủ tin; chi phí là kịch bản tham khảo, cần xác nhận các khoản thiếu trước khi quyết định.";
        return new(items, ids.Where(id => !active.ContainsKey(id)).ToArray(), summary);
    }

    public async Task<RentalDraftPreviewDto> PreviewDraftAsync(RentalDraftPreviewRequestDto request, CancellationToken cancellationToken)
    {
        var userId = userContext.GetRequiredUserId();
        var post = await posts.GetByIdWithMediaAsync(request.PostId, cancellationToken)
            ?? throw new NotFoundException(nameof(RentalPost), request.PostId);
        UserContext.EnsureOwner(userId, post.OwnerId);
        if (string.IsNullOrWhiteSpace(request.Notes) || request.Notes.Length > 3000)
            throw Validation("notes", "Nhập ghi chú từ 1 đến 3.000 ký tự.");
        var missing = new List<string>();
        if (post.Price <= 0) missing.Add("Giá thuê");
        if (post.Area <= 0) missing.Add("Diện tích");
        if (string.IsNullOrWhiteSpace(post.Address)) missing.Add("Địa chỉ và vị trí");
        if (post.Media.Count(item => item.MediaType == MediaType.Image) < 3) missing.Add("Ít nhất 3 ảnh bạn có quyền sử dụng");
        missing.Add("Xác nhận đơn giá/đơn vị điện, nước, internet và các khoản phí");
        // Template preserves user notes verbatim as data; never executes instructions in notes or images.
        var title = string.IsNullOrWhiteSpace(post.Title) ? "Thông tin phòng cho thuê" : post.Title;
        return new(title, request.Notes.Trim(), missing, "ownerNotes");
    }

    public async Task<AdminAssistantSummaryDto> GetAdminSummaryAsync(CancellationToken cancellationToken)
    {
        UserContext.EnsureAdmin(await userContext.GetRequiredProfileAsync(cancellationToken));
        var pending = await moderation.GetPendingRentalPostsAsync(cancellationToken);
        var reports = await moderation.GetReportsAsync(ReportStatus.New, cancellationToken);
        return new(timeProvider.GetUtcNow(), pending.Count, reports.Count,
            pending.Take(10).Select(post => new AdminAssistantTaskDto("posts", post.Id, post.Title))
                .Concat(reports.Take(10).Select(report => new AdminAssistantTaskDto("reports", report.Id, "Báo cáo cần xem xét"))).ToArray(),
            $"Hiện có {pending.Count} tin chờ duyệt và {reports.Count} báo cáo chờ xử lý. Đây là số đang chờ tại thời điểm tính, không phải số phát sinh riêng hôm nay. Mở từng mục để kiểm tra và quyết định.");
    }

    private static RequestValidationException Validation(string field, string message) => new(new Dictionary<string, string[]> { [field] = [message] });
}
