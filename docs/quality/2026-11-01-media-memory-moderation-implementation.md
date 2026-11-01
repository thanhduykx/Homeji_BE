# Giới hạn RAM media, AI 5 ảnh và kiểm duyệt đa phương thức

Ngày kiểm tra ban đầu: 2026-11-01; cập nhật 2026-11-02 (Asia/Saigon). **Đã qua các phép thử Linux 512 MiB được mô tả bên dưới; chưa triển khai production tại thời điểm ghi báo cáo này.** Không có cam kết không restart với mọi tải/sự cố hạ tầng, cũng không có bằng chứng accuracy AI production.

## Thay đổi

- Admission trước MVC model binding: chỉ 1 POST ảnh/upload/chatbot được xử lý tại một thời điểm trong một tiến trình, không xếp hàng body. Caller quá tải nhận 429/Retry-After: 5. Health/read endpoints không chiếm slot này. Từng bài vẫn giữ giới hạn upload 10 ảnh và xử lý upload tuần tự như trước; không giảm bài đăng xuống 1 ảnh.
- Chỉ 1 native image decode trên toàn tiến trình, dùng chung mọi DI scope và giữa chatbot/chat/kiểm duyệt. Tối đa 2 caller chờ, timeout 5 giây, hỗ trợ cancellation; đầy nhận lỗi có thể thử lại. Trong Linux container có hạn mức ≤512 MiB, dùng memory.current của cgroup để kiểm tra ngưỡng 350 MiB và giữ ít nhất 96 MiB dự phòng; môi trường khác fallback RSS tiến trình. Đây là bảo vệ ở thời điểm admission, không phải hard cap RSS/container.
- Kiểm tra source ≤20 MP; yêu cầu codec scaled decode trước cấp bitmap, kiểm tra kích thước codec thực trả về ≤4.194.304 pixel. Output cạnh dài ≤2.048 px. Không fallback decode toàn ảnh lớn nếu codec không hỗ trợ scale; PNG lớn gửi trực tiếp bằng client cũ sẽ nhận lỗi yêu cầu resize, thay vì cấp phát ngoài ngân sách. UI AI resize tuần tự ở browser trước khi gửi và encode JPEG, tránh trường hợp này trong luồng mới.
- Mỗi lượt AI hỗ trợ 5 ảnh + tối đa 1 audio, tổng byte đã decode vẫn ≤8 MiB, mỗi ảnh ≤4 MiB. Không tăng tổng budget theo số ảnh. Browser không giải mã 5 ảnh cùng lúc, retry giữ nguyên ảnh đã chuẩn bị.
- Tất cả typed Gemini HttpClient dùng chung tối đa 2 provider request đang chạy. Không xếp hàng vô hạn payload; response được giới hạn 256 KiB và giữ slot cho đến khi đọc xong body giới hạn. Lỗi/timeout/cancellation đều giải phóng slot. Cloudinary response cũng được giới hạn 256 KiB và dispose.

## Kiểm duyệt

- Trước sửa chỉ text; hiện snapshot bao gồm URL ảnh và thứ tự, policy/hash v2. Đổi ảnh tạo phiên bản mới, stale verdict không áp dụng lên nội dung đã đổi. Tin cũ vẫn qua cơ chế backfill do admin yêu cầu; không tự quét lại toàn bộ lịch sử.
- Duyệt tối đa 10 ảnh của tin, chia nhóm tối đa 5/provider request. Đặt trước ngân sách cho 1 hoặc 2 lượt gọi và dự phòng token ảnh. Budget là reservation bảo thủ, không phải hóa đơn đo chính xác.
- Chỉ fetch asset HTTPS đúng `res.cloudinary.com/<CloudName cấu hình>/image/upload/...`; không fetch URL tùy ý/IP/private host, không follow redirect. Đọc thumbnail 1.024 px, stream ≤512 KiB/ảnh, deadline 8 giây; kiểm chữ ký/kích thước qua codec và giải mã/encode lại bằng processor có cùng limiter. Không đủ ảnh/không tải được/định dạng không hợp lệ → không clear.
- Prompt riêng trong systemInstruction, output có JSON schema và vẫn parse/validate ở server. Quét nội dung chữ, chữ/chỉ thị trong ảnh, hàng cấm/đe dọa/bóc lột, lời xúc phạm/kỳ thị, ảnh nhạy cảm và ảnh khác sản phẩm/mô tả.
- Mismatch/ảnh nhạy cảm/ngôn ngữ xúc phạm/nghi gian lận → needs_review; auto-block vẫn chỉ áp dụng 3 nhóm nghiêm trọng với evidence text nguyên văn, ngữ cảnh không phủ định và confidence ≥0,98 theo policy hiện hữu. Confidence do model trả không phải xác suất đã được hiệu chuẩn.
- Bất kỳ batch clear có confidence <0,9 đều xuống review; batch khác clear không che khuất review. Safety block, JSON sai, timeout/provider lỗi được retry có backoff; ba lần không hoàn tất chuyển người duyệt. Không coi lỗi AI là phê duyệt.
- Deadline chung review 100 giây, nhỏ hơn lease 3 phút. Ảnh ngoài CDN được quản lý hoặc không xác minh được sẽ cần người duyệt; không tuyên bố kiểm tra đủ khi thiếu ảnh.

## Bằng chứng

- 42 test integration không cần DB đã pass: image codec/resize, Gemini client/deadline, resource admission, queue cancellation, provider cap, SSRF/asset size, 10 ảnh thành hai batch, mismatch aggregation, safety block và low-confidence.
- 22 unit test liên quan media validation/policy/budget đã pass.
- 1 integration test database PostgreSQL cô lập đã pass, bao gồm lease/retry/stale verdict, backfill và **chỉ thay ảnh** tạo hash/job mới. Evidence: `output/quality/local-761475cb72d44192b5433da0a5ddb7f3/tests/`.
- FE production build pass. E2E Playwright 390 px/light và 1.280 px/dark pass: chọn 5 ảnh, body đúng 5 ảnh JPEG đã chuẩn bị, lỗi giả 503 và retry giữ bytes, audio/dictation/validation/cancel. Provider/API writes đều intercepted local trong test UI.
- Harness Windows x64 gọi code xử lý thật; Gemini transport giả lập, không gọi Google, không dùng quota. 4 caller ×2 vòng, 20 MP: image peak 73,79 MiB (5 hoàn thành, 3 từ chối), AI 5 ảnh/lượt peak 85,99 MiB (5 hoàn thành, 3 từ chối). Các scenario khác: [JSON](../../output/memory-concurrency-2026-11-01/after-memory-limits.json).
- Thêm 200 MiB native ballast giả lập RAM nền, 4 caller ×10 vòng: image peak **278,01 MiB** (22 hoàn thành/18 từ chối); AI 5 ảnh peak **296,27 MiB** (28/12). Ballast không phải API thật hay dữ liệu production. Không so trực tiếp với phép đo trước 3 ảnh để tuyên bố cùng workload. [JSON](../../output/memory-concurrency-2026-11-01/after-memory-limits-with-ballast.json).
- [Script Linux 512 MiB](../../output/memory-concurrency-2026-11-01/Test-Linux512.ps1): 1 CPU, không swap, network none, 200 MiB ballast, kiểm exit, OOMKilled và phải có work hoàn thành. Đã chạy ngày 2026-11-02; kết quả và phạm vi dưới đây.

## Cập nhật Linux và Gemini thật — 2026-11-02

- Docker Desktop 4.90 không mở engine do AF_UNIX socket cũ. Đã giữ bản sao thư mục socket tạm (`Docker/run` và `docker-secrets-engine`) bằng đổi tên, không reset, không xóa image/volume. Docker Engine 29.7.2 sau đó hoạt động. Tham khảo lỗi upstream [Docker #15064](https://github.com/docker/for-win/issues/15064).
- Phép thử Linux đầu tiên sống nhưng AI bị từ chối toàn bộ vì RSS dùng ngưỡng bảo vệ khác với bộ nhớ cgroup được charge. Không tính đó là thành công. Đã đổi sang cgroup current cho container nhỏ và thêm điều kiện phải có lời gọi hoàn thành.
- Sau sửa, 4 caller ×10 vòng với 200 MiB ballast: xử lý ảnh hoàn thành 21/40, từ chối 19/40; AI 5 ảnh hoàn thành 9/40, từ chối 31/40. Cả hai exit 0, OOMKilled=false, giới hạn memory=memorySwap=536.870.912 byte (không có swap bổ sung).
- Đỉnh cgroup image **364.875.776 byte (~347,97 MiB)**; AI **376.184.832 byte (~358,76 MiB)**. Đây là RAM cgroup, không lấy RSS so thay. [Kết quả harness](../../output/memory-concurrency-2026-11-01/linux-512-results.json).
- Pipeline HTTP ứng dụng thật với TestServer, MVC JSON, auth fixture và chatbot service adapter gọi processor/Gemini client thật, transport Gemini giả: 32 lượt cạnh tranh (mỗi lượt 5 ảnh 20 MP), 8 HTTP 200 /24 HTTP 429, health giữa các vòng vẫn 200. Cgroup peak **343.011.328 byte (~327,12 MiB)**, exit 0, không OOM. Business/DB của chatbot được thay bằng fixture, TestServer không kiểm tra được giới hạn body của Kestrel. [Kết quả HTTP](../../output/memory-concurrency-2026-11-01/http-linux-results.json).
- Image Docker production với PostgreSQL 18 tạm, migration thật, 48 request health/danh sách public đều 200, peak cgroup **135.987.200 byte (~129,69 MiB)**, restartCount=0, oom/oom_kill=0. Background workers tắt, DB rỗng; không phải workload production đầy đủ và không chứng minh authenticated media cùng DB/Gemini thật. [Kết quả API](../../output/memory-concurrency-2026-11-01/api-container-512-results.json).
- Fixture 20 MP được lưu sẵn cho test thay vì tạo bitmap 20 MP ngay trong tiến trình test, tránh native allocation của bộ tạo dữ liệu làm sai lệch phép đo ứng dụng. Các test native memory dùng collection cô lập; test concurrency vẫn có caller chạy song song riêng.
- Thử Gemini thật bằng dữ liệu tổng hợp/ảnh vẽ đơn giản, không dữ liệu người dùng: có valid-chair clear và explicit-threat block nhưng policy bảo thủ giữ human review do ngữ cảnh "không mua". Các lần khác gặp MAX_TOKENS/503. Đây không phải tập đánh giá có thể suy ra accuracy.
- Tìm thấy MAX_TOKENS vì giới hạn 512 token dùng chung phần suy luận và kết quả; tăng reservation lên **2.048 output token**, cấu hình reasoning low chỉ cho Gemini 3, vẫn giới hạn response 256 KiB. Nguồn: [Google thinking và output budget](https://ai.google.dev/gemini-api/docs/generate-content/thinking). Không thay model tự động.
- Sau sửa, valid-chair trả STOP/clear; lời gọi tiếp theo nhận **429 quota Free trong ngày**. Đã dừng đánh giá, không đổi sang gói trả phí, không lặp thử vượt backoff. Do đó chưa xác minh đủ mismatch/profanity/injection bằng model thật. [Kết quả cuối](../../output/memory-concurrency-2026-11-01/real-gemini-moderation-final-results.json); giữ cả các lần lỗi trước trong output để không chọn lọc kết quả thuận lợi.
- Snapshot bổ sung nội quy, lý do nhượng phòng và địa chỉ public để các trường chữ này cũng được kiểm tra. Không đưa thông tin xác minh chủ nhà riêng tư vào AI.

## Phần chưa xác minh và bước tiếp

1. Sau deploy, theo dõi dưới tải thật và thời gian dài: cgroup current/peak, error/rejection rate, thời gian chờ, restart/OOM. Ba phép thử Linux phía trên không bảo đảm uptime cho mọi tải hoặc sự cố hạ tầng.
2. Đánh giá Gemini thật với tập nội dung có nhãn: hợp lệ, cảnh báo/phủ định, tiếng lóng, ảnh không khớp, phụ kiện hợp lệ, ảnh mờ và prompt injection. Test hiện tại chứng minh pipeline/policy, **không chứng minh tỷ lệ phát hiện hay tỷ lệ chặn nhầm**.
3. Deploy riêng backend và frontend sau khi qua gate tài nguyên; smoke 5 ảnh và retry. Chưa chỉnh environment, schema production hay gói Render. Không bật thử nghiệm nặng trên production.

Nguồn primary và giới hạn nghiên cứu: [research](../research/2026-11-01-image-moderation-memory.md).
