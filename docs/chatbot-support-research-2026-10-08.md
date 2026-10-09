# Chatbot hỗ trợ Homeji: tham khảo Fin và Zendesk

Ngày kiểm tra: 2026-10-08. Phạm vi: tài liệu chính thức, đối chiếu mã nguồn Homeji; không gọi thanh toán thật, không thử gửi tin nhắn đến đội hỗ trợ của dịch vụ khác.

## Đã triển khai và giới hạn còn lại

- Backend `0c03a25` đã Live trên Render. `ChatbotSupportKnowledge` xử lý lời chào, giới thiệu, cảm ơn, hướng dẫn PayOS/Premium/đặt đồ ăn và câu hỏi cần kiểm tra trạng thái trước khi gọi Gemini. Tìm phòng vẫn dùng retrieval hiện có. History và Routes vẫn tắt.
- Test service cho `hi` và câu PayOS đi từ 2 failed sang 2 passed, đồng thời kiểm tra không gọi provider và không lưu repository. Tổng 303 unit tests, 84 API tests passed / 8 skipped; Release publish đạt.
- Browser production tại `/privacy` gửi đúng `hi` và “Cách thanh toán bằng PayOS như thế nào?”: trả lời chào và hướng dẫn 4 bước, CTA mở gói, không còn thông báo unavailable cho hai câu này. Không tạo thanh toán hoặc đăng tin. Ảnh frontend `output/verification/chatbot-payos-fixed-20261008.jpg`.
- Provider vẫn chưa khôi phục: log Render gọi model cấu hình `gemini-3.5-flash` và trả HTTP 401. Credential sẵn có trong môi trường máy được thử riêng với một prompt không chứa dữ liệu người dùng, Google trả `API_KEY_INVALID`. Không in/lưu key hoặc payload bí mật. Render có `Ai__Gemini__ApiKey` và `Ai__Gemini__Endpoint`, chưa tìm thấy key hợp lệ để thay thế. Không kết luận các credential ở máy và Render giống nhau. Cần credential Google Gemini hợp lệ để kiểm chứng lại phần sinh câu trả lời mở rộng. Google mô tả 401 là vấn đề xác thực trong [API errors](https://ai.google.dev/gemini-api/docs/api-errors).

Luồng FAQ đã sửa không đồng nghĩa Gemini đã hoạt động; câu hỏi ngoài kiến thức được duyệt vẫn dùng provider và báo sự cố trung thực khi provider lỗi.

## Những gì tài liệu chính thức xác nhận

**Intercom Fin** trả lời từ nội dung hỗ trợ và dữ liệu được cung cấp. Khi chưa tìm được câu trả lời rõ ràng, Fin có thể nêu phần thông tin tìm được, thể hiện sự chưa chắc chắn và hỏi làm rõ. Tài liệu cũng phân biệt không biết câu trả lời với sự cố LLM; khi lỗi lặp lại, Fin chuyển hội thoại sang đội hỗ trợ đã được tích hợp. Không nên hiểu việc chuyển giao là một khả năng mặc định của mọi chatbot. [Fin AI Agent FAQs](https://www.intercom.com/help/en/articles/7837535-fin-ai-agent-faqs).

**Zendesk AI agents** có các loại phản hồi riêng cho chào hỏi, kiến thức, không tìm thấy trường hợp phù hợp, lỗi kỹ thuật, chuyển sang người hỗ trợ và chuyển giao thất bại. Knowledge reply tìm trong nguồn kiến thức cho câu hỏi không phải small talk; nếu không có thông tin phù hợp, agent thông báo không trả lời được. Lỗi API tích hợp thuộc technical error reply. Điều này hỗ trợ cách thiết kế tách trạng thái hội thoại thay vì dùng một thông báo lỗi cho tất cả. [Viewing and editing system replies for AI agents](https://support.zendesk.com/hc/en-us/articles/8357749481882-Viewing-and-editing-system-replies-for-AI-agents).

Các tài liệu mô tả khả năng sản phẩm của hai nhà cung cấp, không chứng minh hiệu năng hoặc độ chính xác của Homeji. Không sao chép lời hứa chuyển giao sang nhân viên nếu Homeji chưa có kênh vận hành tương ứng.

## Đối chiếu luồng PayOS trong Homeji

Nguồn nội bộ đọc trực tiếp: `C:/EXE101_Homeji/src/pages/PaymentPage.tsx`, `PaymentWaitingPage.tsx`, `src/lib/paymentLifecycle.ts`, `src/api/index.ts`.

- Người dùng chọn gói Premium rồi chọn PayOS. `startCheckout` gọi API tạo thanh toán của gói và mở `/payments/wait?paymentId=...`.
- Trang chờ có nút **Thanh toán qua PayOS**, mở liên kết thanh toán do hệ thống trả về. Có nút **Tôi đã thanh toán · Kiểm tra lại** và đường dẫn về lịch sử giao dịch.
- Trang chờ hiển thị thời hạn theo `expiresAt`, hoặc mặc định 15 phút từ `createdAt`. Hết hạn thì không còn nút thanh toán và hướng dẫn không thanh toán đơn đó.
- Trạng thái hoàn tất/kích hoạt gói dựa trên dữ liệu hệ thống. Chatbot không có căn cứ để xác nhận đơn đã trả tiền chỉ vì người dùng nói đã trả.

Đây là mô tả code hiện tại; không phải kiểm chứng giao dịch thật hoặc bằng chứng dịch vụ PayOS đang khả dụng ở thời điểm trả lời.

## Đề xuất áp dụng cho Homeji

1. Phân luồng trước provider: lời chào, giới thiệu khả năng, FAQ vận hành đã được đối chiếu và câu hỏi cần dữ liệu trực tiếp. FAQ có nội dung và hành động điều hướng được quản lý cùng nhau; không chỉ gửi nút sau một thông báo lỗi.
2. Với câu hỏi phức tạp, dùng kiến thức được kiểm soát và dữ liệu truy xuất có quyền truy cập. Hỏi một câu làm rõ khi chưa biết ý định. Tách “chưa đủ thông tin” với “dịch vụ tạo câu trả lời đang lỗi”.
3. Nếu provider lỗi, lời chào và FAQ đã xác minh vẫn trả lời được. Câu hỏi cần sinh nội dung mới được thông báo lỗi ngắn và có hành động phù hợp. Không dùng FAQ gần nghĩa để giả vờ trả lời một vấn đề khác.
4. Không nhận OTP, mật khẩu hoặc tự tạo đơn thanh toán trong hội thoại. Đưa người dùng đến giao diện hiện có để họ tự chọn gói/phương thức và xác nhận.
5. Chỉ mở điều hướng tới kênh hỗ trợ có thật. Không viết “đã chuyển cho nhân viên” khi chưa thực hiện được chuyển giao.
6. Giữ Routes nâng cao và lưu lịch sử dài hạn tắt theo yêu cầu. Xử lý lời chào/FAQ không cần bật lại hai tính năng này.

Các đề xuất trên là quyết định thiết kế cho Homeji, không phải thuật toán nội bộ của Fin hoặc Zendesk.

## Câu trả lời đề xuất, dựa trên Homeji

**Người dùng: “hi”**

> Chào bạn! Mình là trợ lý Homeji. Bạn muốn tìm phòng gần một đường hoặc trường học, so sánh phòng, hay xem hướng dẫn dùng Homeji?

Hành động phù hợp: **Tìm phòng**, **Gói đăng ký**. Không gọi provider chỉ để chào hỏi.

**Người dùng: “Cách thanh toán bằng PayOS như thế nào?”**

> Bạn mở Gói đăng ký, chọn gói Premium và chọn PayOS. Tại trang chờ, bấm “Thanh toán qua PayOS” để mở cổng thanh toán. Sau khi thanh toán, Homeji sẽ cập nhật khi nhận được xác nhận; bạn cũng có thể bấm “Tôi đã thanh toán · Kiểm tra lại”. Đơn có thời hạn hiển thị trên trang chờ; đừng thanh toán đơn đã hết hạn.

Hành động: **Mở Gói đăng ký** → `/?section=payments&tab=plans`. Không khẳng định gói đã kích hoạt hoặc báo giá cụ thể từ FAQ.

**Người dùng: “Đã trả rồi mà chưa có gói”**

> Bạn mở giao dịch đó và bấm “Tôi đã thanh toán · Kiểm tra lại”. Mình chưa có dữ liệu để xác nhận trạng thái đơn của bạn. Trang đang hiển thị “Chờ thanh toán”, “Hoàn tất” hay báo lỗi?

Hành động: **Lịch sử giao dịch** → `/?section=payments&tab=history`. Câu hỏi này cần trạng thái có thẩm quyền; không trả lời “đã thanh toán thành công” bằng suy đoán.

## Kiểm chứng cần có khi triển khai

- `hi`, `xin chào`, `hello` trả lời ngắn khi provider timeout/429/503 hoặc chưa cấu hình.
- FAQ PayOS có bước thao tác và link đúng; câu hỏi hoàn tiền/trạng thái đơn riêng không bị FAQ che mất.
- Câu hỏi mơ hồ được hỏi làm rõ; câu hỏi ngoài phạm vi không bị điều hướng sang một mục không liên quan.
- Không trả mã OTP, trạng thái đơn/tài khoản hoặc dữ liệu không có quyền; không hứa liên hệ nhân viên giả.
- Ghi loại lỗi provider và request ID cho vận hành, không ghi khóa API hoặc dữ liệu nhạy cảm của câu hỏi.
