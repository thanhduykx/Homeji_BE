# Email giao dịch, biên nhận và OTP Homeji

Kiểm tra 17/10/2026; đọc mã nguồn tại `C:/homeji/output/brevo-deploy` và `C:/EXE101_Homeji`. Chưa đổi code/config, đọc secret hay gửi email thật. Đây là ghi chú triển khai; lựa chọn biên nhận/VAT và OTP/link đang chờ người dùng xác nhận.

## Hướng triển khai

Tạo biên nhận có lịch sử bền vững trong database, cùng transaction cập nhật thanh toán/ví và email outbox. Worker gửi HTML + plain text qua Brevo HTTPS sau commit. Email lỗi không được rollback giao dịch tiền đã thành công. Biên nhận trong ứng dụng phải xem được ngay cả khi email đang chờ hoặc thất bại.

Gọi sản phẩm là **biên nhận giao dịch** cho đến khi xác nhận yêu cầu hóa đơn. HTML có logo, số chứng từ và thông tin thanh toán không tự tạo ra tích hợp hóa đơn thuế. Nếu người dùng yêu cầu hóa đơn điện tử/VAT, cần xác định đơn vị phát hành, loại giao dịch, dữ liệu doanh nghiệp, hệ thống hóa đơn và quy trình nghiệp vụ riêng; nghiên cứu này không kết luận nghĩa vụ pháp lý/thuế.

## Điểm phát sinh sự kiện đã kiểm tra

- `src/Homeji.Application/Services/Wallets/WalletWithdrawalService.cs`: `CreateAsync` trừ số dư, tạo withdrawal Pending và ledger rồi `SaveChangesAsync`; phát `WithdrawalRequested` sau khi lưu bền vững. `CompleteAsync` có admin note/mã chuyển khoản và trạng thái Completed; phát `WithdrawalCompleted`. `RejectAsync` hoàn tiền vào ví và tạo ledger; nên có email `WithdrawalRejected` kèm lý do và tiền hoàn.
- `src/Homeji.Infrastructure/External/PaymentService.cs`: callback MoMo và webhook PayOS kiểm tra chữ ký, số tiền, định danh phương thức; PayOS kiểm tra VND. Khi Paid: ghi payment, cấp premium hoặc credit ví, rồi SaveChanges. Đây là điểm tạo receipt/outbox; không dùng redirect thành công từ trình duyệt làm nguồn xác nhận tiền. Callback Paid lặp đang return sớm; unique key receipt/outbox vẫn cần để chống concurrency.
- `CreditWalletTopUpIfNeededAsync` có kiểm tra ledger TopUp theo payment. Kiểm tra tồn tại riêng lẻ không tự bảo đảm chống race: cần unique constraint và kiểm soát đồng thời trong transaction.
- `src/Homeji.Application/Services/Wallets/WalletService.cs`: lịch sử hiện trả ledger giới hạn 1–100 dòng, chưa phải kho biên nhận phân trang.
- Frontend `src/components/marketplace/wallet/WalletPage.tsx` ghép ledger và withdrawal qua ReferenceId, hiển thị trạng thái và filter; có thể thêm mở chi tiết receipt từ dòng lịch sử. `src/api/index.ts` đã có GET `/api/wallet/transactions`, `/api/wallet/withdrawals`, admin complete/reject; cần API receipt riêng kiểm tra owner/admin.

Sự kiện bổ sung đáng ưu tiên: premium thanh toán thành công; hoàn tiền/rút tiền bị từ chối; thông báo đổi mật khẩu thành công. Không gửi email mọi tin chat hoặc mọi trạng thái phụ nếu không có nhu cầu vì quota và nhiễu.

## Brevo: quota, retry và idempotency

Free có **300 email/ngày dùng chung tài khoản**. Đăng ký, resend, reset, nạp tiền, rút yêu cầu và rút hoàn tất đều dùng chung ngân sách. 100 người mỗi người nhận 3 email đã dùng 300; không phải mỗi loại có riêng 300. [Free limits](https://help.brevo.com/hc/en-us/articles/208580669-FAQs-What-are-the-limits-of-the-Free-plan)

POST `https://api.brevo.com/v3/smtp/email` hỗ trợ HTML, plain text và template; đăng ký domain/sender trước. [Transactional email](https://developers.brevo.com/docs/send-a-transactional-email)

Rate limit API khác quota gửi ngày. General tier của endpoint gửi công bố 1.000 RPS/3.600.000 RPH; 429 khi quá giới hạn. Theo dõi response headers và backoff. Với Homeji, quota ngày là giới hạn quan trọng hơn throughput. [API limits](https://developers.brevo.com/docs/api-limits)

Tài liệu idempotency hiện công bố **TTL 30 phút**, UUID ở `headers.idempotencyKey` trong JSON; retry cùng request dùng cùng key; key trùng trong cửa sổ trả `duplicate_parameter`. Trang có tiêu đề batch nhưng ví dụ có một người nhận; cần contract test payload dùng thực tế. Không dùng kiến thức cũ TTL 15 phút. [Idempotency](https://developers.brevo.com/docs/heterogenous-versions-batch-emails)

Outbox đề xuất: unique `(EventType, AggregateId, RecipientUserId)`; receipt unique theo payment/event; trạng thái Pending/Processing/Accepted/Failed/Uncertain, attempt count, next attempt, lease owner/expiry, stable UUID send key, provider message ID và lỗi đã lọc PII. Worker claim atomically; retry cùng nội dung/key trong TTL. Không coi `duplicate_parameter` là delivered. `Accepted` chỉ xác nhận provider nhận request; delivery/bounce cần webhook hoặc đối soát. [Webhooks](https://developers.brevo.com/docs/how-to-use-webhooks)

**Không bảo đảm exactly-once delivery.** Timeout sau provider accept hoặc crash trước khi lưu Accepted có thể gây duplicate; retry sau TTL không còn provider dedup. Cần giữ trạng thái Uncertain để đối soát hoặc chính sách retry có ghi rõ rủi ro. Unique event ngăn enqueue trùng nhưng không tự giải quyết ranh giới DB/provider. Không nhúng OTP vào outbox dài hạn dạng plaintext; không để job hết hạn tiếp tục gửi.

`BrevoAccountEmailSender.cs` hiện gửi link registration/reset, chưa parse messageId/idempotency, không tự retry vì timeout có thể xảy ra sau accept. Giữ nguyên ý nghĩa an toàn này khi bổ sung worker; tránh retry mù.

## OTP đăng ký với mật khẩu hiện tại

Giữ đăng ký email + password, dùng Supabase tạo OTP xác nhận, Brevo chỉ chuyển email. Không thay toàn bộ bằng `signInWithOtp` nếu nghiệp vụ vẫn yêu cầu mật khẩu.

Admin generateLink nhận `type: signup`, email, password lúc tạo mới; response có `email_otp`, `action_link`, `verification_type`. Parser backend hiện chỉ lấy action_link nên cần mở rộng; OTP không được trả cho browser hoặc log. [Admin API source](https://github.com/supabase/supabase-js/blob/master/packages/core/auth-js/src/GoTrueAdminApi.ts)

**Resend qua custom Brevo:** source GoTrue hiện tại cho `type: signup` với user đã tồn tại chưa confirmed quay lại tạo ConfirmationToken/SentAt mà không đổi password; có thể bỏ password và Data. User confirmed bị từ chối. OTP dùng cấu hình `Mailer.OtpLength`; không hardcode 6 ký tự. Admin handler không có cooldown của `sendConfirmation`; cần tự giới hạn. Đây là đọc source master, phải kiểm tra tương thích hosted version. [mail.go](https://raw.githubusercontent.com/supabase/auth/master/internal/api/mail.go)

Ràng buộc resend vào pending challenge do backend tạo, gắn user/email và purpose, không cho client tự chọn metadata/password của tài khoản đang chờ. Resend không xóa user cũ khi email lỗi. Serial hóa resend cùng challenge để mã mới không bị email gửi đến sai thứ tự. Chỉ gửi mã còn hạn và đánh dấu thế hệ mã để job cũ không gửi sau mã mới.

POST `/auth/v1/verify` nhận `{ type: 'signup', email, token }`, kiểm tra confirmation token và trả session khi thành công. `type: email` trong tài liệu SDK cũng hỗ trợ, nhưng source hiện chấp nhận cả confirmation và recovery/magiclink; signup dùng purpose cụ thể hơn. [verify.go](https://raw.githubusercontent.com/supabase/auth/master/internal/api/verify.go), [SDK types](https://raw.githubusercontent.com/supabase/auth-js/master/src/lib/types.ts)

Tài liệu `auth.resend({type:'signup',email})` gửi lại qua cơ chế mail của Supabase. Không gọi đường này nếu muốn giữ custom Brevo ở backend mà chưa cấu hình SMTP/hook tương ứng. [Resend docs](https://supabase.com/docs/reference/javascript/auth-resend)

Supabase `/verify` có rate limit mặc định 30 request/5 phút/IP. Backend proxy có thể gom mọi người vào cùng IP; vẫn cần limit theo challenge/email/IP ở Homeji. Giới hạn Supabase/project thực tế phải kiểm tra dashboard. [Auth rate limits](https://supabase.com/docs/guides/auth/rate-limits)

Frontend hiện `AuthPage.tsx` và `components/auth/AuthModal.tsx` hiển thị thông báo kiểm tra email khi `emailConfirmationRequired`, chưa có OTP confirmation/resend. Cần cập nhật mọi entry point, điều hướng nhất quán, cooldown hiển thị, mã sai/hết hạn/đã dùng, xử lý session thành công, phục hồi trang refresh; tránh lưu password hoặc OTP lâu dài trên client.

Nếu giữ link reset password, tắt click tracking cho auth email; email scanner có thể tiêu thụ link. OTP là lựa chọn thay thế khi người dùng xác nhận. [Email templates and limitations](https://supabase.com/docs/guides/auth/auth-email-templates)

## Dữ liệu biên nhận và kiểm thử

Receipt nên có mã ổn định, user owner, event/purpose, amount VND integer, provider/mã giao dịch, paid/processed timestamp UTC, bản hiển thị giờ Việt Nam, trạng thái, bank mask khi rút, template version và snapshot đủ để xem lại nhất quán. Không email full số tài khoản, raw webhook, token hay secrets. Nạp ví ghi đúng “nạp ví”, không tự gán tên hàng hóa hay VAT. Lịch sử có filter loại/trạng thái/thời gian, pagination cursor, mở chi tiết/in; phân biệt trạng thái nghiệp vụ và trạng thái email.

Kiểm thử trọng yếu: callback lặp/song song không cộng tiền/phát receipt lặp; admin complete/reject race; rollback không có outbox mồ côi; worker crash/lease expiry; timeout uncertain; quota hết; truy cập receipt người khác; HTML escape tên/note; OTP sai/hết hạn/reused/resend và account confirmed; không log token. Render Free có idle/restart nên worker trong web process không bảo đảm gửi tức thì; database outbox giữ backlog, cần phương án worker/scheduler có độ sẵn sàng phù hợp.

Chưa xác nhận cấu hình OTP length/expiry thực tế, quota dashboard, sender reputation, yêu cầu VAT, hay lựa chọn OTP/link cuối cùng. Changelog Supabase `.md` không fetch được trong lần kiểm tra; nguồn API/source được kiểm tra trực tiếp, không suy luận hosted version trùng master.
