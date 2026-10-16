# Gửi email xác thực / OTP cho Homeji

Ngày kiểm tra: 16/10/2026. Phạm vi: nguồn chính thức, gửi thực tế khoảng 100 lần/ngày, miễn phí lâu dài; không coi credit dùng thử là quota miễn phí duy trì. Chưa đăng ký tài khoản nhà cung cấp, đổi cấu hình hay gửi tin thật.

## Kết luận

Chọn **Brevo qua HTTPS API** cho email xác thực giai đoạn hiện tại: quota miễn phí 300 email/ngày có khoảng trống cho gửi lại và quên mật khẩu. Resend là lựa chọn thay thế khi tổng lưu lượng không vượt 100/ngày và 3.000/tháng. Chưa xác minh được phương án Zalo/SMS chính thức miễn phí lâu dài 100 tin/ngày; Zalo công bố phí theo tin, Twilio chỉ có trial giới hạn.

Email OTP hoặc link xác thực chỉ chứng minh quyền truy cập **địa chỉ email**. OTP qua Zalo/SMS chứng minh quyền truy cập kênh liên quan đến **số điện thoại**; email không thay thế yêu cầu xác thực số điện thoại của nghiệp vụ.

## Email miễn phí

### Brevo — đề xuất

- Free: 300 email/ngày, không hết hạn gói, quota không cộng dồn. Khi quá quota, transactional email có thể bị giữ trong hàng đợi tối đa 1.000 email; không nên để OTP hết hạn tiếp tục nằm chờ. [Giới hạn Free](https://help.brevo.com/hc/en-us/articles/208580669-FAQs-What-are-the-limits-of-the-Free-plan)
- Gói Free có transactional email. [Các gói Brevo](https://help.brevo.com/hc/en-us/articles/208589409-About-Brevo-s-pricing-plans)
- Xác thực domain do mình kiểm soát và cấu hình sender. Domain Gmail/Yahoo công cộng không xác thực được; miễn phí dịch vụ gửi không bao gồm mua domain. [Xác thực domain](https://help.brevo.com/hc/en-us/articles/12163873383186-Authenticate-your-domain-with-Brevo-Brevo-code-DKIM-DMARC)
- SMTP mới có thể cần yêu cầu hỗ trợ kích hoạt transactional platform. Quota công bố không bảo đảm tài khoản vừa tạo gửi được ngay. [Khắc phục SMTP](https://help.brevo.com/hc/en-us/articles/115000188150-Troubleshooting-Issues-with-Brevo-SMTP)
- Hỗ trợ gửi transactional bằng HTTPS API. [Hướng dẫn API](https://developers.brevo.com/docs/send-a-transactional-email)

Đánh giá: đủ 100 email/ngày trong cả tháng 31 ngày theo quota ngày công bố, còn 200/ngày cho lưu lượng khác. Cần theo dõi bounce, giới hạn gửi lại và kiểm tra inbox thật; không có bằng chứng bảo đảm mọi email vào inbox.

### Resend — vừa ngưỡng yêu cầu

Free có 100 email/ngày, 3.000/tháng và 3 domain. [Bảng giá](https://resend.com/pricing)

Quota tính cả email nhận vào, từng người nhận To/CC/BCC; reset ngày lúc 00:00 UTC, tương đương **07:00 giờ Việt Nam**. [Tài liệu quota](https://resend.com/docs/knowledge-base/account-quotas-and-limits)

Cần thêm và xác minh ít nhất một domain mình sở hữu. [Verified domains](https://resend.com/docs/dashboard/domains/introduction)

Đánh giá: 100 × 30 = 3.000 phù hợp nếu không có inbound/email khác. 100 × 31 = 3.100 vượt quota tháng. Gửi lại, reset password và nhiều người nhận cũng tiêu quota nên không có khoảng dự phòng khi đạt đúng 100/ngày.

### SMTP2GO — không đủ nhu cầu duy trì

Free không giới hạn thời gian nhưng chỉ 1.000 email/tháng và 200/ngày. Vượt tháng thì email mới bị từ chối. [Free plan](https://support.smtp2go.com/hc/en-gb/articles/223087947-Free-Plan)

Đánh giá: đủ cho demo nhỏ, không đủ 3.000 email/tháng.

## Zalo OTP

Zalo có Mẫu Xác thực của ZBS Template Message, dùng OTP tạo tài khoản, đăng nhập, đổi mật khẩu; đây là tin trong ứng dụng Zalo, không phải SMS nhà mạng. [Mẫu Xác thực](https://zalo.solutions/blog/cach-su-dung-va-ung-dung-cua-mau-tin-zns-xac-thuc-/g8yv8lcpvmj0c0wonqbpibgj)

Nguồn chính thức đang **mâu thuẫn về đơn giá**:

- Trang pricing: template xác thực 300 đồng/tin. [Bảng giá](https://zalo.solutions/business-message/pricing)
- Điều khoản cập nhật 09/07/2026: 400 đồng/tin thành công, chưa VAT. Cần OA xác thực, tài khoản ZBS/AppID, đăng ký template; duyệt 2–3 ngày làm việc. Người nhận cần số điện thoại gắn tài khoản Zalo. [Điều khoản](https://zalo.solutions/terms)

Chưa thể chốt đơn giá áp dụng cho Homeji. Dự trù sơ bộ 3.000 tin/tháng là **900.000–1.200.000 đồng**, trước VAT và chi phí OA/đối tác có thể phát sinh; đây là phép tính theo hai mức công bố, không phải báo giá. Cần xác nhận trong dashboard/hợp đồng trước tích hợp. Không tìm thấy bằng chứng quota miễn phí lặp lại 100 OTP/ngày trong các nguồn đã kiểm tra.

## SMS Việt Nam

Twilio công bố SMS ra Việt Nam 0,2852 USD/segment; giá tùy tuyến và có thể đổi. [Giá SMS Việt Nam](https://www.twilio.com/en-us/sms/pricing/vn)

Verify có thêm 0,05 USD cho mỗi xác thực thành công, ngoài phí kênh. [Verify pricing](https://www.twilio.com/en-us/verify/pricing)

Trial hết hạn sau 30 ngày, chỉ gửi đến số đã xác minh, tối đa 5 người nhận; không phải phương án phục vụ người dùng production miễn phí dài hạn. [Trial restrictions](https://www.twilio.com/docs/usage/trials)

Kết luận giới hạn ở bằng chứng đã kiểm tra: chưa có nhà cung cấp chính thức được xác minh đáp ứng SMS miễn phí lâu dài 100/ngày. Không khẳng định mọi nhà cung cấp trên thị trường đều không có khuyến mại.

## Liên hệ kiến trúc backend hiện tại

Theo kiểm tra mã nguồn của phiên nghiên cứu này: `render.yaml` dùng plan free; đăng ký ở `SupabaseAccountService.RegisterAsync` có nhánh admin `generate_link` + `IAccountEmailSender` khi có ServiceRoleKey, DI hiện dùng `SmtpAccountEmailSender`; nhánh còn lại dùng `/signup`. `ForgotPasswordAsync` luôn gọi `/recover` qua Supabase nên đổi sender của backend đơn lẻ chưa thay đổi email quên mật khẩu.

Render free chặn SMTP outbound cổng 25/465/587. [Render free](https://render.com/docs/free)

Supabase xác nhận `generateLink` dùng để tạo link/OTP cho nhà cung cấp email tùy chọn; thao tác này không tự thay thế bộ gửi thư của Homeji. [generateLink](https://supabase.com/docs/reference/javascript/auth-admin-generatelink)

SMTP mặc định của Supabase hiện chỉ gửi tới email thành viên dự án và giới hạn 2 thư/giờ, không phù hợp nhu cầu người dùng thật 100/ngày. Custom SMTP khởi đầu ở 30 thư/giờ, có thể điều chỉnh trong Auth Rate Limits. Nếu chọn hướng Supabase trực tiếp gửi, cấu hình tại Supabase nên kết nối từ Supabase tới nhà cung cấp, không chịu chặn cổng outbound của Render. [Custom SMTP](https://supabase.com/docs/guides/auth/auth-smtp)

Hai hướng cần lựa chọn có chủ đích khi triển khai:

1. **Thay adapter gửi email bằng Brevo HTTPS**: ít thay đổi nhánh đăng ký hiện tại, tránh chặn SMTP của Render. Phải xử lý riêng hoặc đồng bộ đường gửi quên mật khẩu để không còn hai cơ chế khó theo dõi.
2. **Đưa toàn bộ email auth về Supabase với custom SMTP**: gom trách nhiệm phát hành/gửi token, nhưng phải refactor nhánh admin `generate_link` hiện tại và kiểm tra quota auth riêng của Supabase; cấu hình SMTP không tự thay thế luồng gửi thủ công của backend.

Không triển khai trong nghiên cứu này. Trước triển khai phải kiểm tra domain/DNS, activation tài khoản, quota thực trong dashboard; tính một lần resend là một email mới; đặt giới hạn theo IP/người nhận, TTL và single-use cho mã/token, đồng thời tránh log mã xác thực hoặc secret.

## Trình tự triển khai đề xuất

1. Chốt mục tiêu xác thực email hay số điện thoại. Nếu email đủ cho nghiệp vụ, tạo tài khoản Brevo Free và xác nhận khả năng gửi transactional của tài khoản.
2. Dùng domain/subdomain do Homeji quản lý; thêm bản ghi DNS theo chính dashboard nhà cung cấp, xác thực domain và sender. Không thay hoặc thêm SPF trùng lặp tùy tiện.
3. Tạo API key giới hạn quyền phù hợp, lưu secret ở môi trường backend; chọn adapter HTTPS và kiểm tra cùng đường gửi cho đăng ký, gửi lại, quên mật khẩu. Không đưa key xuống frontend.
4. Test email thật với Gmail/Outlook, link về đúng frontend/redirect cho phép, token hết hạn, token dùng lại, lỗi provider, quota hết, resend lặp và request song song. Kiểm tra cả trường hợp không có ServiceRoleKey để tránh nhánh fallback bị bỏ sót.
5. Theo dõi accepted/delivered/bounced riêng biệt. Provider nhận request không chứng minh người dùng nhận email. Với lỗi mạng không rõ kết quả, cần tránh retry tạo nhiều email/token độc lập; bỏ job khi mã đã hết hạn.

Các bước trên là khuyến nghị kỹ thuật; chưa phải cấu hình triển khai được xác nhận. Chưa kiểm tra quyền DNS hiện có, approval tài khoản provider, quota Supabase Auth thực tế trong project, sender reputation, hay chi phí gói OA áp dụng cho Homeji. Hai trang Zalo có giá khác nhau nên không được chốt giá bằng một trong hai con số mà không xác nhận thêm.
