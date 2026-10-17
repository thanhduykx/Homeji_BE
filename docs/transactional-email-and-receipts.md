# Email và biên nhận giao dịch Homeji

Ngày triển khai: 17/10/2026. Frontend chuẩn: https://homeji.onrender.com.

## Hành vi

- Đăng ký có mật khẩu: Supabase tạo mã xác nhận signup, Brevo chuyển mã OTP tới email. Website nhập mã tại `/verify-email`, xác minh bằng `type=signup` rồi đăng nhập bằng session Supabase.
- OTP tối đa 10 phút, tối đa 5 lần thử cho mỗi lần cấp mã; gửi lại cách nhau ít nhất 60 giây. Bộ đếm và thời gian chờ được cập nhật nguyên tử trong PostgreSQL, tồn tại sau restart. Backend không lưu OTP hoặc mật khẩu trong bảng challenge.
- Gửi lại chỉ áp dụng cho email đã có challenge từ lần đăng ký Homeji. Admin generate_link chỉ nhận type/email; không truyền password hoặc metadata để tránh thay đổi tài khoản đang chờ xác nhận. Đã kiểm tra trực tiếp contract này trên project Supabase hiện tại; mã cũ bị từ chối, mã mới dùng được và tài khoản thử nghiệm được dọn sạch.
- Đặt lại mật khẩu giữ email liên kết khôi phục, chuyển về `/reset-password` trên domain mới.
- Nạp ví/thanh toán gói thành công: ghi biên nhận và công việc gửi email cùng lần SaveChanges với trạng thái Paid, sổ ví hoặc subscription.
- Rút tiền: ghi nhận yêu cầu, hoàn tất, hoặc từ chối kèm hoàn lại ví. Mỗi bước có biên nhận riêng; yêu cầu đang chờ không được trình bày như xác nhận đã nhận tiền tại ngân hàng.
- Email giao dịch có nội dung HTML và file `.html` đính kèm. Lịch sử `/receipts` có phân trang, xem chi tiết trong iframe sandbox và tải HTML. API chỉ trả biên nhận của người đang đăng nhập; ID của người khác trả 404.
- Migration tạo biên nhận từ thanh toán Paid và các trạng thái rút tiền cũ. Không xếp email cho lịch sử này; giao diện ghi rõ “Biên nhận lịch sử · không gửi email”. Không thay đổi số dư khi tạo biên nhận.

## Cấu hình

Giữ API key ở biến môi trường/secret local, không đưa vào frontend hoặc Git:

```text
Email__Provider=Brevo
Email__RegistrationOtpEnabled=true
Email__Receipts__Enabled=true
BackgroundJobs__Enabled=true
Supabase__RegistrationRedirectUrl=https://homeji.onrender.com/auth/callback
Supabase__PasswordResetRedirectUrl=https://homeji.onrender.com/reset-password
Supabase__GoogleRedirectUrl=https://homeji.onrender.com/auth/callback
Payments__Redirects__FrontendPaymentUrl=https://homeji.onrender.com/payments
Cors__AllowedOrigins__3=https://homeji.onrender.com
```

Supabase Site URL và Redirect URLs phải dùng domain mới. Google OAuth Authorized JavaScript origins phải có `https://homeji.onrender.com`; OAuth redirect server vẫn là callback của project Supabase.

Hai cờ OTP và email biên nhận mặc định tắt để rollout độc lập: triển khai migration/backend, triển khai frontend có trang OTP, rồi bật OTP. Biên nhận vẫn được ghi khi cờ gửi email tắt. Không tắt BackgroundJobs để ngừng riêng email; dùng `Email__Receipts__Enabled=false`.

## Hàng đợi và giới hạn

Unique `(ReferenceId, Event)` ngăn tạo biên nhận trùng. Dispatcher claim 5 công việc bằng một câu lệnh PostgreSQL `FOR UPDATE SKIP LOCKED`; lease 20 phút, tối đa 8 lần thử. Thông tin người nhận lấy từ email đã xác nhận trong `auth.users`. Số tài khoản chỉ hiện 4 số cuối; không gửi ghi chú nội bộ.

Brevo Free có 300 email/ngày dùng chung cho xác thực, reset và giao dịch. Hết quota có thể khiến email bị chậm hoặc dừng sau giới hạn retry; biên nhận vẫn xem được trên website. Theo dõi quota trước khi tăng người dùng. Render Free ngủ thì worker không chạy, công việc vẫn được giữ trong database.

`EmailSentAt` chỉ có nghĩa Brevo đã nhận yêu cầu, không phải bằng chứng thư đã tới inbox. Brevo idempotency dùng UUID job trong JSON `headers.idempotencyKey`, cửa sổ hiện công bố 30 phút. Timeout/lỗi mạng/HTTP 5xx được đánh dấu kết quả chưa xác định và dừng tự gửi lại. Lease bỏ dở quá cửa sổ an toàn cũng dừng để đối soát; tránh tự phát lại email có thể đã được nhận. Không cam kết exactly-once delivery.

Kiểm tra `homeji.receipt_email_deliveries` với `SentAt IS NULL AND Attempts >= 8`, đối chiếu Brevo trước khi vận hành gửi lại. Không reset hàng loạt vì có thể gửi trùng. Các bảng mới bật RLS không có policy công khai; truy cập qua backend có kiểm tra owner.

## Kiểm tra

```powershell
dotnet restore Homeji.sln
pwsh -NoProfile -File scripts/quality/Test-LocalBackend.ps1 -SkipLoadTest
```

Script tạo PostgreSQL loopback riêng `homeji_quality`, áp dụng migration và chạy unit/integration tests, không sử dụng database production. Bao gồm webhook PayOS hợp lệ gọi lặp, nạp ví, yêu cầu rút, từ chối/hoàn ví, hoàn tất, rollback biên nhận/outbox, claim đồng thời, kết quả gửi chưa xác định, giới hạn OTP, purpose signup, không đổi mật khẩu khi resend, HTML escaping và file đính kèm UTF-8.

Không dùng thử rút/nạp tiền thật để kiểm tra tính năng email trên production. Kiểm chứng giao dịch tiền thật cần có giao dịch hợp lệ và đối soát riêng.
