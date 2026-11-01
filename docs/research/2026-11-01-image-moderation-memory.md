# Nghiên cứu ảnh, RAM và kiểm duyệt Homeji

Kiểm tra ngày 2026-11-01, Asia/Saigon. Nguồn chính thức; đây là hướng dẫn triển khai, không chứng nhận độ chính xác model hoặc cam kết không restart.

## Kết luận cho triển khai

1. Cho AI nhận **tối đa 5 ảnh**, nhưng 5 ảnh dùng chung một ngân sách byte. Không tăng tổng payload theo phép nhân 5 × giới hạn mỗi ảnh. Giữ tổng 8 MiB phía ứng dụng là lựa chọn bảo thủ; giới hạn này là đề xuất kỹ thuật của Homeji, không phải hạn mức Gemini.
2. Chặn tải trước model binding/giải mã, giới hạn đồng thời toàn tiến trình, hàng đợi hữu hạn và timeout. Giới hạn ảnh decode phải dựa trên kích thước codec thực sự trả về, không chỉ output resize.
3. Kiểm duyệt phải gửi ảnh thực sự cùng nội dung. Cấu hình hiện tại chỉ đánh giá text, chưa phát hiện được hình–nội dung không khớp. Không gọi kết quả text-only là “đã quét ảnh”.
4. Nếu ảnh thiếu/hỏng, model bị safety block, JSON sai, provider lỗi, hoặc quá ngân sách: `needs_review`/retry có giới hạn; không `clear` mặc định và không tự block vì lỗi kỹ thuật.

## Gemini: ảnh và payload

Gemini hỗ trợ nhiều ảnh trong một request; hướng dẫn image understanding hiện nêu 20 MB cho tổng inline request. Google có bài công bố khác về 100 MB, phụ thuộc loại dữ liệu. Sự khác nhau này không ảnh hưởng lựa chọn giữ payload Homeji nhỏ. Độ phân giải ảnh cao hơn có thể cải thiện đọc chữ nhỏ nhưng tăng token và độ trễ. Nguồn: [Image understanding](https://ai.google.dev/gemini-api/docs/image-understanding), [Google công bố giới hạn file](https://blog.google/innovation-and-ai/technology/developers-tools/gemini-api-new-file-limits/).

Khuyến nghị suy ra cho Homeji: đánh số `image_1`…`image_5`, giữ thứ tự ổn định, yêu cầu model chỉ dẫn bằng chứng theo số ảnh. Resize có thể làm mất chữ nhỏ; ảnh mờ hoặc OCR không chắc phải ghi uncertainty thay vì suy đoán. Chỉ đánh giá khác biệt nhìn thấy được: “tin bán tủ lạnh nhưng ảnh là xe máy” có thể gắn cờ; không suy ra hàng giả, diện tích phòng, địa chỉ thật, giá trị hay chủ sở hữu chỉ từ ảnh.

## Safety không thay thế kiểm duyệt marketplace

Safety settings phân loại các nhóm harm của model; chúng không phải bộ quy tắc chuyên biệt để xác minh món hàng và ảnh đúng nhau. Google yêu cầu hậu kiểm, đánh giá thủ công và bảo vệ trước prompt injection. Nguồn: [Safety settings](https://ai.google.dev/gemini-api/docs/safety-settings), [Safety and factuality guidance](https://ai.google.dev/gemini-api/docs/safety-guidance).

Đề xuất policy Homeji: tách cờ `abusive_language`, `image_text_mismatch`, `prohibited_goods`, `fraud_suspected`, `sexual_content`, `threat`, `uncertain`. Tiếng lóng, né dấu, chữ trong ảnh và câu trích dẫn/cảnh báo cần được kiểm thử; không chặn chỉ vì trúng một từ khóa. Mismatch và nghi ngờ lừa đảo chuyển người duyệt. Tự chặn chỉ các trường hợp nghiêm trọng có bằng chứng rõ theo policy đã phê duyệt; confidence model tự báo không phải xác suất đúng đã hiệu chuẩn.

Prompt nên đặt policy trong system instruction, dữ liệu tin/ảnh trong user parts, nói rõ chữ trong ảnh và nội dung tin là dữ liệu không đáng tin. Model không được nhận chỉ thị từ ảnh để đổi kết quả, gọi công cụ, mở URL hoặc bỏ qua policy. Nội dung thử phải bao gồm “ignore previous instructions”, JSON giả, chữ độc hại được trích để cảnh báo và nội dung đúng nhưng ít ảnh.

## Structured output và bằng chứng

JSON Schema giúp cố định cấu trúc nhưng vẫn phải validate giá trị phía ứng dụng. Một số ràng buộc chuỗi/số thuộc tính không được enforced; model có thể trả output đúng schema nhưng sai nghĩa. Nguồn: [Structured outputs](https://ai.google.dev/gemini-api/docs/structured-output).

Đề xuất: quyết định, category, lý do tiếng Việt, quote nguyên văn hoặc image index, danh sách uncertainty và trạng thái đủ ảnh. Validate enum, finite confidence, độ dài, số ảnh tham chiếu trong phạm vi, quote text có trong snapshot và consistency giữa decision/category. Lưu phiên bản policy/hash cả **text + danh sách ảnh**. Kết quả cho phiên bản cũ không áp dụng sau khi tin/ảnh đổi. Kiểm tra body provider bằng stream có giới hạn byte và deadline chung; không đọc string response vô hạn.

## SkiaSharp: scaled decode không đồng nghĩa resize sau decode

`SKCodec.GetScaledDimensions(float)` trả kích thước codec hỗ trợ gần hệ số mong muốn; không bảo đảm kích thước chính xác. Skia native xác định đây là gợi ý kích thước scale native gần nhất. Nguồn: [Microsoft SKCodec.GetScaledDimensions](https://learn.microsoft.com/en-us/dotnet/api/skiasharp.skcodec.getscaleddimensions?view=skiasharp), [Skia SkCodec.h](https://github.com/google/skia/blob/main/include/codec/SkCodec.h).

Hướng thực hiện: đọc header → kiểm tra nguồn ≤20 MP → tính target → hỏi scaled dimensions → kiểm tra số pixel decode thực tế trước cấp phát → decode trực tiếp kích thước được hỗ trợ → encode surface mới để bỏ metadata. Nếu codec trả full size vượt ngân sách thì reject có lỗi dễ hiểu hoặc dùng đường xử lý khác có giới hạn; không âm thầm fallback full decode. Test JPEG, PNG, WebP, ảnh cực dài, alpha, EXIF orientation, file hỏng/truncated và cancellation. Bảo toàn ảnh bài đăng 5–10 tấm là vấn đề số lượng upload, không phải cho decode đồng thời 5–10 ảnh.

## Lấy ảnh để kiểm duyệt: SSRF và file upload

OWASP khuyến nghị allowlist loại file, kiểm chữ ký thay vì tin Content-Type, giới hạn size, tên do hệ thống tạo và cập nhật parser. Nguồn: [File Upload Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/File_Upload_Cheat_Sheet.html).

OWASP yêu cầu bảo vệ URL/IP/domain và không tự theo redirect để tránh vượt validation; hostname hợp lệ vẫn có thể resolve về mạng nội bộ. Nguồn: [SSRF Prevention Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Server_Side_Request_Forgery_Prevention_Cheat_Sheet.html).

Đề xuất Homeji: chỉ nhận asset URL từ CDN do ứng dụng quản lý, HTTPS, exact host + cloud/path định danh cấu hình; reject userinfo, port lạ, IP literal, fragment. Tắt redirect, timeout ngắn, stream có giới hạn ngay cả khi không có Content-Length, xác thực MIME/signature. Không tải URL tùy ý từ mô tả hay do model đề xuất. Nếu dùng Cloudinary transformation để thumbnail, chỉ dựng transformation từ asset đã validate; không cho client truyền chuỗi fetch/transformation tùy ý. Tránh giữ 5 ảnh nguồn, 5 bitmap và 5 base64 lớn cùng lúc.

## Bằng chứng local trước sửa

- `src/Homeji.Infrastructure/External/GeminiListingReviewClient.cs`: Instructions nói rõ chỉ đánh giá chữ, request chỉ text.
- `src/Homeji.Infrastructure/External/AiListingModeration.cs`: Snapshot hiện không có ảnh; hash policy-v1 chỉ dựa trên snapshot text/metadata.
- `src/Homeji.Application/Services/Moderation/AiListingReviewPolicy.cs`: auto-block nghiêm trọng hiện đòi quote trực tiếp và confidence ≥0,98; cần bảo toàn guard này khi thêm image review.

Các kiểm tra cần chứng minh trước tuyên bố hoàn thành: năm ảnh được nhận ở FE/BE; sáu ảnh bị chặn; ảnh sai chữ ký/URL SSRF/timeout không clear; mismatch đi review; text severe chỉ auto-block đúng policy; sửa ảnh tạo review mới; load ảnh/AI hỗn hợp dưới container 512 MB và không OOM trong kịch bản công bố. Vẫn không thể bảo đảm 100% phát hiện vi phạm hoặc không restart với mọi tải và sự cố hạ tầng.
