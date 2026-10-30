# Lời nhắc xem phòng qua email

Người thuê mở **Lịch xem phòng**, chọn **Đặt lời nhắc qua email**, chọn nhắc trước 15 phút / 30 phút / 1 giờ / 1 ngày, rồi **Lưu lời nhắc**. Có thể đổi hoặc hủy lời nhắc trên cùng thẻ lịch hẹn. Chỉ người đã đặt lịch được sửa lời nhắc của mình; email tài khoản phải được xác thực.

## API và hành vi

`PUT /api/viewing-appointments/{id}/reminder`

```json
{ "minutesBefore": 30 }
```

Gửi `{ "minutesBefore": null }` để hủy. API trả lại `ViewingAppointmentDto`, bổ sung `reminderMinutesBefore`, `reminderDueAt`, `reminderSentAt`, `reminderFailed`.

- Chỉ cho đặt vào tương lai trên lịch Pending/Confirmed. Lịch Pending được ghi rõ là chưa được chủ trọ xác nhận trong email.
- Đổi lịch giữ khoảng nhắc và tạo mã gửi mới. Nếu giờ nhắc mới đã qua thì tự hủy lời nhắc; người thuê có thể chọn khoảng ngắn hơn.
- Hủy, từ chối hoặc hoàn tất lịch sẽ xóa lời nhắc. Không gửi khi lịch đã qua, phòng đã ngừng công khai hoặc email chưa xác thực.
- Lưu trong PostgreSQL; không phụ thuộc trình duyệt. Worker kiểm tra mỗi 15 giây, có thể gửi muộn hơn khi server hoặc nhà cung cấp bị gián đoạn, nhưng không bắt đầu gửi sau giờ xem phòng.
- Claim bằng `FOR UPDATE SKIP LOCKED` để nhiều worker không nhận cùng tác vụ. Gửi thất bại rõ ràng được thử lại sau một phút, tối đa 5 lần và trước giờ hẹn. Kết quả không chắc chắn hoặc claim hết hạn sau crash sẽ dừng tự thử lại để tránh gửi lặp. Không cam kết email vào inbox: `reminderSentAt` ghi nhận nhà cung cấp đã nhận yêu cầu.
- Nội dung HTML dùng layout email Homeji có sẵn, escape thông tin tin đăng, thời gian hiển thị UTC+7.

## Chạy trên môi trường triển khai

Áp dụng migration `20261030045044_AddViewingAppointmentEmailReminders` vào đúng database của môi trường trước khi chạy bản API mới:

```powershell
dotnet ef database update --project src/Homeji.Infrastructure --startup-project src/Homeji.Api --context ApplicationDbContext
```

Tái sử dụng `Email:Provider` và cấu hình SMTP/Brevo đang có; không cần khóa email mới. Với SMTP, cần `Email:Smtp:Enabled=true` và TLS/các thông số SMTP hợp lệ. Với Brevo, dùng cấu hình `Email:Brevo` hiện có. `BackgroundJobs:Enabled` phải là `true` để worker gửi tự động. Các giá trị bí mật tiếp tục đặt qua môi trường hoặc cấu hình local đã bỏ qua Git.

## Kiểm thử

```powershell
dotnet test tests/Homeji.Application.UnitTests --filter FullyQualifiedName~ViewingAppointmentReminder
# PostgreSQL cô lập: script riêng chạy domain tests, migration và test API/email/database.
./scripts/quality/Test-AppointmentReminders.ps1
```

Frontend:

```powershell
npm run build
npm run dev -- --host 127.0.0.1 --port 5187 --strictPort
# Terminal khác, giữ dev server đang chạy:
node tests/appointment-reminders.e2e.mjs
```

E2E dùng fixture API và Chrome local; `CHROME_PATH` cho phép chọn đường dẫn Chrome khác, `HOMEJI_UI_BASE` cho phép đổi cổng local. Ảnh desktop/mobile và kết quả nằm tại `output/verification/appointment-reminders/` trong frontend.

Các test mới kiểm tra domain, HTTP 401/403/422, lưu/hủy thực tế trong PostgreSQL, phục hồi sau tạo DbContext mới, nhiều worker, bỏ qua lịch hủy/đổi/quá giờ, retry/uncertain delivery, template và SMTP tắt/không TLS. Test dùng email sender giả lập; không gửi đến hộp thư thật.
