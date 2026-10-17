# Thiết lập Brevo cho Homeji

## Trạng thái

Đã tích hợp API HTTPS cho email xác nhận đăng ký và khôi phục mật khẩu. Ngày 16/10/2026 đã kiểm tra API key và sender active qua Brevo API, lưu credential chỉ trong file local được Git ignore. Đã gửi một thư kiểm tra cấu hình tới chính email sender; Brevo events ghi nhận `requests`, `delivered`, `opened`. Đây là kiểm chứng tuyến gửi thư thật, chưa phải kiểm chứng end-to-end đăng ký/quên mật khẩu.

Đã bổ sung service_role hiện có của đúng dự án Homeji, kiểm tra admin API trả HTTP 200 và bật `Email:Provider=Brevo` trong `appsettings.Local.json`. Không tạo/rotate key Supabase hoặc thay đổi chính sách truy cập. Backend local đã khởi động thành công và `/health/live` trả HTTP 200.

Đã kiểm tra trực tiếp `POST /api/account/forgot-password` với email sender có tài khoản Homeji: HTTP 200; Brevo events ghi nhận email “Đặt lại mật khẩu Homeji” ở trạng thái `requests`, `delivered`, `opened`. Không mở link reset hoặc thay đổi mật khẩu. Hai callback đăng ký và đặt lại mật khẩu mặc định đã có trong allowlist Supabase. Chưa tạo thêm người dùng thật để kiểm tra signup; luồng này được kiểm tra tự động bằng HTTP handler giả.

Đã push mã tích hợp lên `main` trong commit `64bd7a3dea02f3fd3b633aa51cf4f50e9a7c3169` và cập nhật Environment của Render Homeji_BE. Deployment cuối `dep-db96hjft5n8s73e6bpkg` có trạng thái **Deploy succeeded | Live**, dùng cùng commit trên.

Ngày 17/10/2026 đã kiểm chứng production: `/health/live` HTTP 200; `POST /api/account/forgot-password` HTTP 200 với thông báo “Đã gửi email khôi phục mật khẩu.” Thao tác từ trang quên mật khẩu của frontend production cũng tạo email mới trong Brevo, có `requests`, `delivered`, sau đó `opened`. Không đổi mật khẩu của người dùng. 146 kiểm thử API Release đạt, 13 ca yêu cầu database test được bỏ qua. Nhánh deploy dựa trên `origin/main` mới nhất trước khi ghép Brevo; các chỉnh sửa AI đang dở trong checkout chính được giữ nguyên.

Deployment đầu đã phát hiện `Invalid API key` do giá trị credential bị che được lưu thành placeholder. Đã thay bằng credential thật đã kiểm chứng, kiểm tra khớp giá trị trước khi lưu và redeploy thành công. Không lưu key trong Git hoặc tài liệu. File local chứa credential được Git ignore và không publish. Sender Gmail đã xác minh; log thư thật cho thấy Brevo dùng địa chỉ gửi thuộc `brevosend.com` thay cho domain Gmail. Domain riêng vẫn là hướng nâng cấp cho nhận diện và deliverability.

## Tạo tài khoản và sender

1. [Đăng ký Brevo Free](https://onboarding.brevo.com/account/register), xác minh email đăng nhập và hoàn tất thông tin tài khoản bằng thông tin thực tế của chủ tài khoản.
2. Trong phần Senders & Domains, thêm sender Homeji với email bạn quản lý và xác minh theo email của Brevo. Nếu có domain riêng, xác thực domain bằng DNS mà dashboard cung cấp, gồm DKIM/DMARC theo hướng dẫn. Không đoán DNS và không tạo thêm SPF trùng lặp. Phí domain không nằm trong quota email miễn phí. [Hướng dẫn domain](https://help.brevo.com/hc/en-us/articles/12163873383186-Authenticate-your-domain-with-Brevo-Brevo-code-DKIM-DMARC).
3. Tạo **API key**, không dùng SMTP key. [Brevo API authentication](https://developers.brevo.com/docs/authentication). Hoàn tất yêu cầu kích hoạt transactional sending nếu tài khoản yêu cầu. Key cần quyền gửi transactional; script thiết lập cũng cần đọc account và senders. Nếu hạn chế IP, cho phép IP gửi thực tế của backend theo chính sách Brevo.
4. Không dán key vào chat, frontend hoặc file được Git theo dõi.

## Thiết lập local

Mở PowerShell 7 và chạy:

```powershell
cd C:/homeji
pwsh -File ./scripts/setup-brevo.ps1
```

Script hỏi sender, nhập ẩn Brevo API key và Supabase service role key nếu chưa có. Nó kiểm tra read-only API account và sender active rồi mới cập nhật `src/Homeji.Api/appsettings.Local.json` được Git ignore, giữ các cấu hình khác. Script không gửi email. Nếu key chỉ có quyền gửi, đọc account/senders có thể bị từ chối; khi đó cấu hình các giá trị tương ứng bằng secret môi trường theo phần dưới và kiểm tra sender trong dashboard.

Supabase service role/secret key phải lấy từ project của Homeji, chỉ dùng ở backend. Publishable/anon key không thay thế quyền admin để tạo link. Restart backend sau khi thay cấu hình. `appsettings.Local.json` không được publish nên không thay thế Environment trên Render.

## Thiết lập Render khi deploy

Nhập các biến trong Environment của backend:

```text
Email__Provider=Brevo
Email__Brevo__ApiKey=<Brevo API key>
Email__Brevo__FromEmail=<sender đã xác minh>
Email__Brevo__FromName=Homeji
Email__Brevo__TimeoutSeconds=30
Supabase__ServiceRoleKey=<Supabase service role/secret key>
```

Giữ `Supabase__ProjectUrl` và `Supabase__ApiKey` đúng project. Nếu chưa có biến tương ứng, callback mặc định của repository là:

```text
Supabase__RegistrationRedirectUrl=https://homeji.onrender.com/auth/callback
Supabase__PasswordResetRedirectUrl=https://homeji.onrender.com/reset-password
```

Thêm hai URL này vào Redirect URLs được cho phép trong Supabase Auth. Local phải cấu hình callback localhost phù hợp frontend. Với Brevo, `redirectTo` do client gửi phải khớp chính xác callback đã cấu hình hoặc để trống; không nhận địa chỉ chuyển hướng tùy ý trong luồng dùng quyền admin.

Backend từ chối khởi động khi chọn Brevo mà thiếu API key/sender/service role key. Không tự fallback về SMTP hoặc gửi mặc định Supabase, giúp tránh đăng ký đi nhầm tuyến. Nếu rollback về `Email__Provider=Smtp`, luồng đăng ký SMTP và quên mật khẩu `/recover` của Supabase được giữ; cần SMTP hợp lệ cho môi trường đó.

## Luồng gửi và kiểm chứng

`POST /api/account/register` → Supabase admin `generate_link` (signup) → Brevo HTTPS `POST /v3/smtp/email` → người dùng bấm link → Supabase xác nhận email và chuyển về frontend. Supabase tiếp tục quản lý expiry/single-use/session; Brevo chỉ chuyển thư.

`POST /api/account/forgot-password` → kiểm tra tài khoản → tạo recovery link ở Supabase → gửi cùng Brevo adapter. Với email không tồn tại, trả cùng thông báo thành công mà không gửi. Không xóa tài khoản khi gửi recovery thất bại. Luồng đăng ký giữ cơ chế cleanup user chưa xác nhận khi gửi thất bại của code hiện tại.

Không tự retry request gửi: timeout có thể xảy ra sau khi Brevo đã nhận thư. HTTP thành công nghĩa là provider nhận request, chưa chứng minh thư tới inbox. Kiểm tra Transactional logs để phân biệt accepted/delivered/bounced. Không log API key, link xác thực hoặc response body của Brevo.

Sau khi có tài khoản và credential:

1. Đăng ký bằng một địa chỉ kiểm thử do bạn quản lý; xác nhận nhận thư, đúng sender và link về frontend.
2. Bấm link, đăng nhập; kiểm tra link dùng lại/hết hạn theo Supabase.
3. Gửi quên mật khẩu cho tài khoản đó; nhận thư Brevo, đặt mật khẩu mới và đăng nhập.
4. Kiểm tra quota, trường hợp provider từ chối hoặc timeout; không chỉ dựa vào HTTP 200 từ backend.

Free có 300 email/ngày; gửi lại và khôi phục đều tiêu quota, hết quota có thể bị xếp hàng nên cần theo dõi trước khi OTP/link hết hạn. [Giới hạn Brevo](https://help.brevo.com/hc/en-us/articles/208580669-FAQs-What-are-the-limits-of-the-Free-plan). Rate limit public auth theo IP đã có trong backend; đây không phải quota toàn hệ thống 100/ngày.

## Kiểm thử tự động

```powershell
dotnet test tests/Homeji.Api.IntegrationTests --no-restore --filter "FullyQualifiedName~BrevoAccountEmailSenderTests|FullyQualifiedName~SmtpAccountEmailSenderTests|FullyQualifiedName~SupabaseAccountServiceTests"
```

Các kiểm thử dùng HTTP handler giả, không gửi thư thật: payload/auth header, HTML escaping, provider 401/429/500, timeout/cancellation, recovery đúng loại token, giữ tài khoản khi gửi recovery lỗi và đường SMTP cũ. Kiểm thử inbox/callback thật vẫn cần tài khoản và quyền truy cập dashboard.
