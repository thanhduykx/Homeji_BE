using System.Globalization;
using System.Net;
using Homeji.Domain.Entities;
using Homeji.Domain.Enums;

namespace Homeji.Infrastructure.External;

public static class AppointmentReminderEmail
{
    public const string Subject = "[Homeji] Nhắc lịch xem phòng";

    public static (string Html, string Text) Render(ViewingAppointment appointment, RentalPost post)
    {
        var time = appointment.ScheduledAt.ToOffset(TimeSpan.FromHours(7))
            .ToString("HH:mm 'ngày' dd/MM/yyyy", CultureInfo.GetCultureInfo("vi-VN"));
        var status = appointment.Status == ViewingAppointmentStatus.Confirmed
            ? "Chủ trọ đã xác nhận lịch hẹn."
            : "Lịch hẹn đang chờ chủ trọ xác nhận. Vui lòng liên hệ trước khi đến.";
        var url = $"{HomejiEmailLayout.Website}/?section=appointments";
        var text = $"Bạn có lịch xem phòng: {post.Title}. Thời gian: {time} (giờ Việt Nam, UTC+7). "
            + $"Địa chỉ: {post.Address}. {status} Xem lịch hoặc thay đổi lời nhắc: {url}";
        var body = $"""
            <h1 class="email-title">Nhắc lịch xem phòng</h1>
            <p>Bạn sắp có lịch xem phòng <strong>{WebUtility.HtmlEncode(post.Title)}</strong>.</p>
            <p><strong>Thời gian:</strong> {WebUtility.HtmlEncode(time)} (giờ Việt Nam, UTC+7)</p>
            <p><strong>Địa chỉ:</strong> {WebUtility.HtmlEncode(post.Address)}</p>
            <p>{WebUtility.HtmlEncode(status)}</p>
            <p><a href="{url}">Xem lịch hẹn và quản lý lời nhắc</a></p>
            <p>Email này được gửi vì bạn đã đặt lời nhắc cho lịch xem phòng trên Homeji.</p>
            """;
        return (HomejiEmailLayout.Wrap(Subject, "Lịch xem phòng của bạn sắp đến.", body), text);
    }
}
