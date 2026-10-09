# AI hữu dụng cho Homeji: cơ hội và thứ tự triển khai

Ngày nghiên cứu: 03/10/2026, múi giờ Việt Nam. Đây là ghi chú engineering research, không phải tính năng đã triển khai hay kết quả thử nghiệm người dùng. Chỉ đọc code hiện tại; không sửa code, database, cấu hình hay provider. Phạm vi sản phẩm giữ Quận 9/Thủ Đức theo cấu hình Homeji, không tự mở rộng ra toàn TP.HCM.

## Kết luận

Ưu tiên làm AI giúp người thuê quyết định bằng dữ liệu thật: hiểu câu tìm phòng → xác nhận tiêu chí → lấy tin phù hợp → giải thích có nguồn → so sánh tổng chi phí và đường đi. Không ưu tiên thêm chatbot nói chuyện chung chung hoặc tự thao tác giao dịch. Giữ Gemini và các interface đang có; chưa có bằng chứng cần đổi provider.

Nên làm ba phần đầu trước: chatbot có grounding vào tin thật, search có tiêu chí rõ ràng, gợi ý minh bạch. Tiện ích quanh ghim đã có nền tảng Google Places, nên mở rộng đúng nhu cầu thay vì viết lại. Dữ liệu 100 tin nhập từ nguồn ngoài cần nguồn gốc, ngày kiểm tra và trạng thái thiếu dữ liệu; AI không thể biến tin cào thành phòng đã được Homeji xác minh.

## Hiện trạng đọc trực tiếp từ code

- `C:/Homeji/src/Homeji.Application/Services/Chatbot/ChatbotService.cs`: đã có hội thoại thuộc người dùng, giới hạn message 1.000 ký tự, lịch sử giới hạn, fallback khi dịch vụ AI lỗi và navigation action. Nhưng tạo câu trả lời AI trước, rồi mới gọi `BuildSearchUpdateAsync`; các tin DB và lý do xếp hạng không được đưa vào lời trả lời trong flow này. Chatbot có thể cập nhật bản đồ nhưng chưa phải trợ lý tư vấn dựa trên kết quả truy vấn thực tế.
- `C:/Homeji/src/Homeji.Infrastructure/External/GeminiChatbotClient.cs`: có retry cho 429/503, timeout, prompt tiếng Việt, kiến thức vị trí và catalog tính năng. Prompt yêu cầu không bịa phòng, nhưng lịch sử và câu mới được nối vào một chuỗi; chưa thấy tool calling hay kết quả retrieval có cấu trúc trong client này. Đây là quan sát source, không xác nhận cấu hình Gemini production.
- `C:/Homeji/src/Homeji.Infrastructure/External/GeminiSearchTextParser.cs` và `C:/Homeji/src/Homeji.Application/DTOs/AI/AiParsedSearchCriteriaDto.cs`: đã chuyển ngôn ngữ tự nhiên sang location/keyword/giá/diện tích/criteria, dùng JSON MIME và parse JSON. Chưa có schema tổng chi phí, điểm đến, phương tiện, giờ đi, yêu cầu bắt buộc so với mong muốn, loại trừ hay trường chưa chắc cần hỏi lại.
- `C:/Homeji/src/Homeji.Application/Services/AI/AiSearchService.cs`: lọc giá/diện tích, lấy tối đa 100 ứng viên, xếp hạng bằng luật rồi trả lý do. Location dùng keyword trên địa chỉ/title, chưa tương đương khoảng cách hay thời gian đi học. Tiện ích/review được dò substring; ví dụ câu phủ định cũng có thể chứa từ cần tìm. Điểm Premium, lượt xem/lưu và độ mới có thể ảnh hưởng relevance. Nhãn “AI bảo thế đó” không giải thích dữ liệu nào là căn cứ.
- `C:/Homeji/src/Homeji.Infrastructure/Repositories/RentalPostRepository.cs`: search dùng `ILike` trên title/description/address và bộ lọc cấu trúc. Chưa thấy semantic retrieval trong path search này; không kết luận toàn database đã/ chưa có extension vector.
- `C:/Homeji/src/Homeji.Domain/Entities/RentalPost.cs`: đã có giá thuê, cọc, điện/nước/internet, tiện ích, số người và tọa độ. Các giá số có thể là zero mặc định; trước khi tính tiền phải phân biệt “miễn phí”, “chưa có thông tin” và dữ liệu đã xác nhận. Không tự lấy zero thành miễn phí.
- `C:/EXE101_Homeji/src/components/map/MapChatbot.tsx`: đã có chat popup, loading/error, navigation whitelist và callback cập nhật search. `C:/EXE101_Homeji/src/contexts/SearchContext.tsx` đã có search theo ngữ cảnh, lịch sử tìm và Places suggestions; chưa phải unified AI intent state.
- `C:/EXE101_Homeji/src/components/map/MapNearbyPanel.tsx`, `src/lib/nearbyPanelAnchor.ts`, `src/lib/placeAutocomplete.ts`: đã có popup tiện ích theo ghim, bốn nhóm ăn uống/cà phê/tạp hóa/y tế, bán kính 1.800 m, khoảng cách đường thẳng và cache RAM 5 phút. Đây là Google data không cần có trong project; không phải AI tìm ra quán và không phải quán Homeji xác minh.
- `C:/EXE101_Homeji/src/lib/mapRoutes.ts`: đã tính một tuyến lái xe để preview/navigation, không tương đương lọc nhiều phòng theo thời gian đi học. Không suy diễn thời gian xe máy từ kết quả `DRIVING`.
- `C:/Homeji/src/Homeji.Application/Services/Chatbot/ChatbotNavigationCatalog.cs`: đã cấm chatbot tự tạo đơn/thanh toán và chỉ mở tính năng whitelist. Giữ nguyên ranh giới này khi mở rộng.

Các nhận định trên là kiểm tra tĩnh tại thời điểm nghiên cứu, không thay cho E2E production. Worktree đang có thay đổi nên cần đọc lại trước mỗi lần triển khai.

## Doanh nghiệp tương đương: học điều gì

Zillow công bố search ngôn ngữ tự nhiên theo thời gian đi làm, khả năng chi trả, trường học và điểm quan tâm. Bài học cho Homeji là dịch “gần trường, dưới ngân sách” thành tiêu chí có thể kiểm chứng, không chỉ keyword. Công bố không chứng minh hiệu quả tương tự tại Việt Nam. [Zillow, 04/09/2024](https://www.zillow.com/news/zillows-ai-powered-home-search-gets-smarter-with-new-natural-language-features/).

Airbnb công bố 30/09/2026 search/filter bằng lời nói, mô tả và so sánh nhà bằng AI; AI planning bắt đầu tại Mỹ. Một số insight cho host được công bố là tính năng dự kiến ra sau trong mùa thu, không coi đã phổ biến toàn cầu. Homeji có thể học so sánh shortlist, hướng dẫn người dùng và insight có hành động rõ ràng, không sao chép toàn bộ nghiệp vụ du lịch. [Airbnb fall update](https://news.airbnb.com/airbnb-2026-fall-update).

## Bảy đề xuất ưu tiên

### 1. P0 — Chatbot tìm phòng có nguồn và nhớ tiêu chí

Hỏi “2 người, dưới 4 triệu cả phí, gần FPT, có bếp” → hiện chip tiêu chí để sửa → hỏi điểm trường cụ thể nếu mơ hồ → trả tối đa 3–5 card tin thật và lý do. Câu tiếp “rẻ hơn, không cần máy lạnh” cập nhật preference state, không nối tất cả tin nhắn cũ thành một câu dài mãi mãi.

Tách conversation orchestration khỏi provider: intent có schema; service đọc `SearchActiveAsync` với constraints; reply chỉ được tham chiếu ID nằm trong kết quả đã authorize, kèm field evidence và ngày tin được cập nhật. Không cho model tự sinh SQL/URL tin. Nếu hết kết quả, hỏi người dùng có muốn nới một điều kiện cụ thể; không tự bỏ ngân sách hoặc ra ngoài phạm vi.

Gemini hỗ trợ structured output và function calling; ứng dụng vẫn cần validate ý nghĩa dữ liệu và thực thi function phía server. Có thể bắt đầu schema parser + orchestration có luật, rồi mới tool calling nếu thực sự cần; không phải đổi provider. [Structured output](https://ai.google.dev/gemini-api/docs/structured-output), [function calling](https://ai.google.dev/gemini-api/docs/function-calling).

Acceptance đề xuất, chưa đạt: 50 câu tiếng Việt có đáp án chuẩn, ít nhất 90% parse đúng trường bắt buộc; 100% card là ID tin đang công khai đúng phạm vi; 0 số tiền/tiện ích không có evidence trong bộ kiểm thử; 20 hội thoại sửa tiêu chí không giữ điều kiện đã bỏ; fallback dùng bộ lọc thường khi AI lỗi.

### 2. P0 — Search bằng nhu cầu với hard/soft constraints

Một intent chung cho thanh search và chatbot: ngân sách tiền thuê hay tổng, khu vực, số người, tiện ích bắt buộc/mong muốn, loại trừ, trường/điểm đến. Hiện “Homeji hiểu bạn muốn…” trước khi áp dụng; nhận dấu/không dấu, “3tr”, “3 triệu”, câu phủ định và các tên trường địa phương. Các hard constraints phải lọc bằng application/database; model không được quyết định bỏ qua.

Với quy mô khoảng 100 tin, bắt đầu bộ lọc cấu trúc + keyword chuẩn hóa/alias. PostgreSQL full-text hỗ trợ document/query matching và ranking; phải thử tokenizer, dấu và tiếng Việt thực tế, không giả định cấu hình tiếng Anh phù hợp. [PostgreSQL full-text](https://www.postgresql.org/docs/current/textsearch-intro.html).

Chỉ thêm multilingual embeddings khi benchmark chứng minh cải thiện nhu cầu như “góc học yên tĩnh” mà keyword bỏ sót. pgvector có exact/approximate search, có caveat lọc sau ANN khiến thiếu kết quả; với dataset nhỏ, exact search và index filter đơn giản có thể đủ. Embedding không thay bộ lọc địa lý, giá hay quyền xem. [pgvector chính thức](https://github.com/pgvector/pgvector#filtering).

Acceptance: 100% query có budget ceiling không trả phòng vượt ceiling; loại trừ xử lý đúng test “không ở ghép”, “không cần máy lạnh”; có trường `unknown` thay vì ép giá trị; semantic/ranking chỉ bật nếu recall trên tập gán nhãn tăng và không giảm tuân thủ constraints.

### 3. P0 — Gợi ý giải thích được, không trộn tài trợ

Card “Phù hợp vì: đúng ngân sách; có bếp theo tin; cách điểm trường X theo tuyến Y”. Tách `userFit` khỏi `commercialBoost`; nếu Premium được ưu tiên, hiển thị nhãn thương mại, không mô tả là AI cho rằng phù hợp hơn. Chưa cần học mô hình recommendation từ dữ liệu hành vi ít và lệch.

Chỉ dùng “theo mô tả chủ tin”, “review đề cập”, “Homeji đã xác minh” đúng provenance; review không trở thành xác minh bằng substring. Không suy đoán an toàn khu phố, tính cách bạn ở ghép, đặc điểm nhạy cảm hay chống lừa đảo tuyệt đối. Chỉ nêu tín hiệu kiểm tra được, ví dụ nguồn cũ/thiếu ảnh, và gợi ý hỏi chủ phòng.

Acceptance: mọi reason có `sourceType`, `field`, `postId`; negative review không được tính là tiện ích đáp ứng; thêm/bỏ Premium không đổi score user-fit; không dùng danh tính/thuộc tính nhạy cảm để xếp hạng. Người dùng có thể chọn “ít quan tâm tiêu chí này”.

### 4. P1 — Tổng chi phí và thời gian tới trường

Tổng tháng = tiền thuê + phí cố định + điện/nước theo lượng người dùng nhập; tiền ban đầu = cọc + tiền thuê đầu kỳ + khoản thu có nguồn. Hiện kịch bản và khoản chưa biết, không gọi là giá chính thức nếu thiếu đơn giá/đơn vị. Tiền cọc không trộn thành chi phí hàng tháng.

Điểm đến do người dùng chọn, không tự dùng vị trí hiện tại. Prefilter phạm vi/giá/tọa độ, chỉ tính Routes cho shortlist (ví dụ 10 phòng × 1 điểm đến), ghi phương tiện/thời điểm tính. Route Matrix trả khoảng cách/thời gian theo cặp, có giới hạn request và lỗi từng phần nên không loại nhầm phòng do một route lỗi. Không lấy đường thẳng chia tốc độ thành thời gian thực. [Google Route Matrix](https://developers.google.com/maps/documentation/routes/compute_route_matrix).

Tradeoff: thêm request Maps, dữ liệu trường/nơi làm việc có yếu tố riêng tư, thời gian thay đổi theo giao thông. Chỉ gọi khi người dùng chọn commute filter; không mặc định gửi vị trí thiết bị. Chưa hứa xe máy/transit phù hợp tại mọi địa điểm; xác nhận mode/coverage trước.

Acceptance: test tiền phí/đơn vị/người và missing values; route lỗi hiện “chưa tính được” không thành 0 phút; không gọi matrix cho mọi phòng trên mỗi keystroke; tất cả “≤20 phút” dùng mode/thời điểm rõ và kết quả Routes thật.

### 5. P1 — Tiện ích quanh ghim theo nhu cầu sống

Giữ popup bên phải khác màu. Khi nhấn ghim, mở ăn uống quanh ghim như hiện có; câu “quán ăn gần phòng này” lấy anchor của phòng, không kéo map về GPS. Thêm chip “cửa hàng”, “nhà thuốc”, “cà phê để học”; câu cuối chỉ nên tìm category có thật, không gán yên tĩnh/ổ cắm nếu Google không cung cấp evidence.

Google Nearby Search hỗ trợ giới hạn vòng tròn, loại địa điểm, xếp hạng distance/popularity và field mask. Bắt đầu field tối thiểu; opening status, rating hay giá chỉ thêm nếu nhu cầu rõ và đã kiểm tra SKU/quota. [Google Nearby Search](https://developers.google.com/maps/documentation/places/web-service/nearby-search).

Không lưu hàng loạt tên/ảnh/review Google vào DB như dữ liệu riêng. Chính sách Places có giới hạn storage/cache và yêu cầu attribution; `place_id` được ngoại lệ lưu lâu dài. Cache RAM 5 phút hiện có cần được audit theo API/điều khoản áp dụng, không mặc nhiên coi TTL ngắn là được phép. Kiểm tra yêu cầu nếu gửi Maps content tới LLM trước khi làm AI tóm tắt quán; có thể chỉ trình bày dữ liệu bằng template và giữ AI chọn category. [Places policies](https://developers.google.com/maps/documentation/places/web-service/policies).

Acceptance: 20 lần click các ghim khác nhau anchor đúng phòng, không nhảy GPS; stale response không thay kết quả ghim mới; có empty/error/retry; ghi Google Maps attribution rõ; không gọi lại vô hạn khi pan; distance đường thẳng khác biệt với quãng đường tuyến.

### 6. P1 — So sánh shortlist, câu hỏi trước khi xem phòng

Chọn 2–3 tin đã lưu → compare giá/tổng ước tính/diện tích/số người/tiện ích/đường đi/nguồn/ngày kiểm tra. AI tóm tắt tradeoff dựa trên dữ liệu so sánh có cấu trúc và preference hiện tại, không tự tuyên bố một phòng tốt tuyệt đối. Nút “Những điều cần hỏi” nêu khoản thiếu: điện theo kWh hay đầu người, nước, giờ giấc, hợp đồng/cọc, có được nuôi thú cưng.

Có thể hiển thị thông tin so sánh bằng component thường trước, AI chỉ viết tóm tắt optional; giảm latency và luôn dùng được khi model lỗi. Mẫu tham khảo sản phẩm là AI comparison được Airbnb công bố, không bằng chứng cần dùng cùng kiến trúc. [Airbnb comparison](https://news.airbnb.com/airbnb-2026-fall-update).

Acceptance: so sánh không trộn đơn vị; missing hiển thị “chưa có thông tin”; tất cả số liệu link đúng tin; tin bị ẩn/xóa được refresh và bỏ khỏi so sánh; không gọi API đặt lịch/cọc chỉ vì câu “chọn phòng này”.

### 7. P2 — Trợ lý đăng tin và trợ lý điều hành dễ hiểu

Chủ tin: từ ghi chú/ảnh do họ có quyền sử dụng, gợi ý draft tiêu đề/mô tả, chỉ ra thiếu giá/địa chỉ/đơn giá/ảnh; review và xác nhận trước khi lưu/đăng. AI không tự thêm “đã xác minh”, tiện ích, diện tích đo từ ảnh hay quyền pass phòng.

Admin là khách hàng không biết code: hỏi “Hôm nay có gì cần xử lý?” → server tính số liệu thật → trả summary đơn giản + link mở đúng hàng/tin, không hiện API key, traceback hay thuật ngữ kỹ thuật. Giải thích định nghĩa lượt xem/người thăm/kỳ so sánh; traffic không đủ để khẳng định nguyên nhân giảm thuê. Không tự duyệt/xóa tin/đổi giá/khóa tài khoản; chỉ đề xuất và nút mở màn hình hiện có.

Acceptance: 100% insight số học khớp API và kỳ báo cáo; role renter không đọc dữ liệu admin; chỉnh sửa AI có preview trước/sau và xác nhận; test prompt injection trong tin nguồn không mở quyền hay thực thi hành động. Dùng RBAC/scoped tools và coi nội dung retrieval là dữ liệu không tin cậy, phù hợp guidance defense-in-depth của Microsoft. [Prompt injection và least privilege](https://learn.microsoft.com/en-us/security/zero-trust/catalog-ai-attack-techniques/prompt-injection).

## Flow mẫu thống nhất

1. Người dùng: “Mình học FPT, 2 người, cả phí tối đa 4 triệu, có bếp, đi học dưới 20 phút.”
2. AI trả intent; UI hỏi chọn đúng cơ sở FPT và phương tiện. Chip cho thấy budget là tổng chi phí, không chỉ giá thuê.
3. Server validate constraints và vùng cho phép; query tin công khai; loại tin hết hạn/không hợp lệ. Tin chưa biết phí được nhóm riêng “cần xác nhận”, không giả là đáp ứng tổng budget.
4. Chỉ với shortlist có tọa độ đủ tin cậy, gọi Routes. Reply gắn card/link đúng ID, giải thích trên field có nguồn; số liệu không qua model để tính.
5. Click ghim card → popup tiện ích quanh phòng. Click quán Google chỉ mở địa điểm/đường đi, không tạo món marketplace hay đơn hàng.
6. “So sánh hai phòng đầu” → đọc lại trạng thái tin và bảng dữ liệu có cấu trúc; summary nêu thiếu thông tin và tradeoff.
7. “Chốt/đặt cọc giúp mình” → hướng tới quy trình người dùng xác nhận; chatbot không có tool chuyển tiền, tạo đơn, tự đặt cọc hay tự duyệt lịch.

## Chi phí, latency, privacy và module boundary

- Không có số tiền/ROI ước đoán trong nghiên cứu này. Trước bật thật đo provider request/token, Maps SKU/field, quota và thời gian từng stage bằng cấu hình tài khoản thực tế. Đặt budget limit và feature flag, không để retry vô hạn.
- Tránh flow hiện tại gọi model trả lời rồi model parse tuần tự nếu có thể gộp intent extraction/retrieval-first. Cache dữ liệu Homeji theo quyền và version tin; cache Google chỉ trong giới hạn chính sách đã xác minh. Hủy request cũ, giữ request ID và debounce input.
- Đề xuất SLO thử nghiệm, chưa phải kết quả: p95 <=5 giây tới card tìm phòng không Routes; <=8 giây khi thêm commute trên hạ tầng warmed-up. Cold-start Render đo và báo riêng. Nếu vượt thì trả card/filter trước, explanation sau; không giữ spinner vô hạn.
- Chỉ gửi dữ liệu cần cho tác vụ tới model; không gửi điện thoại/chứng từ/tin nhắn giữa người thuê và chủ, token/password hoặc toàn bộ profile. Trước khi cá nhân hóa qua lịch sử có cơ chế consent, xóa lịch sử/preference và retention policy; log redacted, không lưu raw prompt mặc định cho dashboard.
- Boundary đề xuất: Application có `SearchIntent`, conversation state, constraint validation, retrieval và fit evaluator; Infrastructure có Gemini/Places/Routes adapter; API chịu auth/rate limit, DTO; Frontend hiển thị chip/card/source/confirmation. Controller hoặc model không được bypass domain service.
- Nội dung tin cào/ảnh/OCR/review là dữ liệu không tin cậy. System instruction tách khỏi nội dung, tool allowlist và auth enforce trước thực thi; escaped renderer, không chạy HTML/SQL/model URL. Prompt “không bịa” một mình không đủ kiểm soát. [Microsoft prompt-injection guidance](https://learn.microsoft.com/en-us/security/zero-trust/catalog-ai-attack-techniques/prompt-injection).

## Rollout và kiểm chứng

Phase 0: chất lượng tin, provenance, title/ảnh/phạm vi, định nghĩa unknown/zero. Ghi dataset fixture tiếng Việt và baseline hiện tại, kiểm tra nguồn còn hoạt động. Không suy ra verified/available từ việc cào thành công.

Phase 1: P0 intent/search/grounded chatbot/reasons, chỉ read-only. Feature flag nhỏ, fallback bộ lọc thường, test API ownership/authorization và prompt injection. Không đưa vector DB vào critical path nếu keyword/filter đủ.

Phase 2: commute/total budget, popup theo nhu cầu và compare. Bổ sung contract tests Maps/mock lỗi từng phần, browser test click ghim nhanh/mobile/late response. Recheck terms/pricing/API version trước deploy.

Phase 3: draft đăng tin và summary admin, chỉ preview/confirmation. Đo latency, retrieval recall, grounded-answer error, tỷ lệ sửa chip, mở chi tiết/lưu/đặt lịch và câu hỏi không có kết quả. So sánh với baseline cùng thời gian/đối tượng; chưa khẳng định tăng conversion. Không tối ưu số lượt chat nếu không cải thiện tìm được phòng phù hợp.

Trước quyết định triển khai cần chốt: đơn vị các phí hiện có và cách đánh dấu missing, cơ sở trường/điểm đến được hỗ trợ, budget thực cho Gemini/Maps, retention/consent và provenance nhập tin. Các thiếu sót này là gate dữ liệu/sản phẩm, không lý do đổi provider hoặc tự bịa dữ liệu.
