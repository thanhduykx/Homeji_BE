using Homeji.Domain.Entities;
using Homeji.Domain.Enums;
using Homeji.Application.Common.Exceptions;

namespace Homeji.Application.Common;

/// <summary>A transfer remains available through its lease-end day in Vietnam. Unknown dates remain unknown.</summary>
public static class RentalPublicAvailability
{
    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);
    public static DateOnly VietnamDate(DateTimeOffset now) => DateOnly.FromDateTime(now.ToOffset(VietnamOffset).DateTime);
    public static DateOnly Today(TimeProvider clock) => VietnamDate(clock.GetUtcNow());
    public static bool IsExpiredTransfer(RentalPostType type, DateOnly? leaseEndsOn, DateOnly today) =>
        type == RentalPostType.RoomTransfer && leaseEndsOn.HasValue && leaseEndsOn.Value < today;
    public static bool IsPublicActive(RentalPost post, DateOnly today) => post.Status == RentalPostStatus.Active
        && !IsExpiredTransfer(post.Type, post.OriginalLeaseEndsOn, today);

    public static void EnsureTransferNotExpired(RentalPost post, DateOnly today)
    {
        if (IsExpiredTransfer(post.Type, post.OriginalLeaseEndsOn, today))
        {
            throw new RequestValidationException(new Dictionary<string, string[]>
            {
                ["originalLeaseEndsOn"] = ["Hợp đồng gốc đã hết hạn, không thể gửi duyệt hoặc công khai bài đăng pass phòng."],
            });
        }
    }
}
