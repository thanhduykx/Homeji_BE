using Homeji.Application.Common;
using Homeji.Application.Common.Exceptions;
using Homeji.Application.DTOs.AI;
using Homeji.Application.IRepositories.RentalPosts;
using Homeji.Application.IRepositories.Profiles;
using Homeji.Application.IServices.AI;
using Homeji.Application.IRepositories.Admin;
using Homeji.Application.Mappers.RentalPosts;
using Homeji.Application.Services.Common;
using Homeji.Domain.Entities;
using Homeji.Domain.Enums;

namespace Homeji.Application.Services.AI;

public sealed class RentalDecisionService(IRentalPostRepository posts, UserContext userContext,
    ICommuteClient commuteClient, IAdminWorkQueueRepository workQueue, TimeProvider timeProvider,
    IUserProfileRepository profiles) : IRentalDecisionService
{
    public async Task<RentalDecisionResponseDto> CompareAsync(RentalDecisionRequestDto request, CancellationToken cancellationToken)
    {
        var currentUserId = userContext.GetRequiredUserId();
        var ids = request.PostIds.Distinct().ToArray();
        if (ids.Length is < 1 or > 10 || ids.Contains(Guid.Empty))
            throw Validation("postIds", "Chọn từ 1 đến 10 tin cho danh sách ngắn.");
        var now = timeProvider.GetUtcNow();
        var found = await posts.GetByIdsWithMediaAsync(ids, cancellationToken);
        var active = found.Where(post => RentalPublicAvailability.IsPublicActive(post, RentalPublicAvailability.VietnamDate(now)) && !post.IsSynthetic
            && HomejiServiceArea.Contains(post.Latitude, post.Longitude))
            .ToDictionary(post => post.Id);
        var owners = active.Count == 0 ? [] : await profiles.GetByIdsAsync(active.Values.Select(post => post.OwnerId).Distinct().ToArray(), cancellationToken);
        var ownerRoles = owners.ToDictionary(profile => profile.Id, profile => profile.Role);
        var visible = active.ToDictionary(entry => entry.Key, entry => RentalPostVisibility.ForViewer(
            RentalPostMapper.ToDto(entry.Value) with { OwnerRole = ownerRoles.TryGetValue(entry.Value.OwnerId, out var role) ? role : null }, currentUserId));
        IReadOnlyCollection<CommuteEstimateDto> routes = [];
        if (request.Destination is { } destination)
        {
            if (!HomejiServiceArea.Contains(destination.Latitude, destination.Longitude)
                || string.IsNullOrWhiteSpace(destination.Label) || destination.Label.Length > 200
                || destination.Mode is not ("DRIVE" or "WALK")
                || destination.DepartureTime.HasValue && (destination.DepartureTime < now || destination.DepartureTime > now.AddDays(7)))
                throw Validation("destination", "Chọn điểm đến trong phạm vi Homeji, đi bộ hoặc ô tô, và giờ đi trong 7 ngày tới.");
            routes = await commuteClient.ComputeAsync(visible.Values.Select(post => new CommuteOriginDto(post.Id, post.Latitude, post.Longitude)).ToArray(), destination, cancellationToken);
        }
        var items = ids.Where(active.ContainsKey).Select(id => new RentalDecisionItemDto(
            visible[id], RentalCostCalculator.Calculate(active[id], request.Scenario ?? new(), visible[id].OwnerRole),
            routes.FirstOrDefault(route => route.PostId == id))).ToArray();
        var comparable = items.Length > 1 && items.All(item => item.Post.Price > 0 && item.Cost.PriceBasis != "unknown")
            && items.Select(item => item.Cost.PriceBasis).Distinct().Count() == 1;
        var summary = comparable
            ? $"Chênh lệch giá thuê giữa các tin: {items.Max(item => item.Post.Price) - items.Min(item => item.Post.Price):N0} đồng/{(items[0].Cost.PriceBasis == "person" ? "người" : "phòng")}/tháng. So sánh thêm diện tích và tiện ích theo nhu cầu; phí chưa đủ dữ liệu để kết luận phòng nào có tổng chi phí thấp nhất."
            : items.Length > 1 ? "Không thể so sánh chênh lệch giá thuê: các tin khác đơn vị theo phòng/người hoặc chưa xác nhận giá/đơn vị. Cần xác nhận cùng đơn vị trước khi so sánh."
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
        var snapshot = await workQueue.GetSnapshotAsync(cancellationToken);
        return new(timeProvider.GetUtcNow(), snapshot.PendingPosts, snapshot.PendingReports,
            snapshot.Posts.Take(10).Select(post => new AdminAssistantTaskDto("posts", post.Id, post.Title))
                .Concat(snapshot.Reports.Take(10).Select(report => new AdminAssistantTaskDto("reports", report.Id,
                    $"{ReportLabel(report.TargetType)}: {report.Reason[..Math.Min(report.Reason.Length, 120)]}"))).ToArray(),
            $"Hiện có {snapshot.PendingPosts} tin chờ duyệt và {snapshot.PendingReports} báo cáo chờ xử lý. Đây là số đang chờ tại thời điểm tính, không phải số phát sinh riêng hôm nay. Mở từng mục để kiểm tra và quyết định.");
    }

    private static string ReportLabel(ReportTargetType type) => type switch
    {
        ReportTargetType.RentalPost => "Báo cáo tin phòng",
        ReportTargetType.User => "Báo cáo người dùng",
        ReportTargetType.RoommateInvitation => "Báo cáo lời mời ở ghép",
        ReportTargetType.MarketplacePost => "Báo cáo tin chợ",
        ReportTargetType.RentalReview => "Báo cáo đánh giá",
        ReportTargetType.RentalWantedPost => "Báo cáo tin tìm phòng",
        _ => "Báo cáo cần xem xét"
    };

    private static RequestValidationException Validation(string field, string message) => new(new Dictionary<string, string[]> { [field] = [message] });
}
