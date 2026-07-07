# Homeji Backend Feature Map

Nguồn: Google Doc “User Manual” do team cung cấp.

## Legend màu trong Google Doc

DOCX/PDF export giữ được màu chữ. Bản text thường không giữ màu, vì vậy các nhóm sau phải được đọc kèm màu:

| Màu trong doc | Ý nghĩa |
|---|---|
| Xanh lá `#188038` | Người dùng làm |
| Đỏ `#ff0000` | App/hệ thống làm |
| Tím `#9900ff` | AI làm |
| Xanh dương `#0000ff` | Optional |
| Hồng đậm `#ff00ff` | Quan trọng |
| Vàng `#ffff00` | Technical |
| Hồng nhạt `#d5a6bd` | Đang cân nhắc |
| Nâu/cam `#b45f06` | Chủ hệ thống/admin làm |

Các màu này không phải chỉ để trang trí. Chúng ảnh hưởng tới priority, ownership và phân quyền backend.

## Feature code theo màu/priority

| Feature | Màu/nhãn | Ý nghĩa backend |
|---|---|---|
| `FEAT-AUTH-01` Đăng nhập / Đăng ký | Quan trọng | Bắt buộc. Supabase Auth đang là nguồn identity. |
| `FEAT-AUTH-02` Khảo sát thói quen onboarding | Đang cân nhắc | Product còn cân nhắc bắt buộc hay không, nhưng data này cần cho roommate matching. Nên làm lightweight profile/lifestyle API sớm. |
| `FEAT-MAP-01` Định vị & quyền GPS | Quan trọng | FE xử lý quyền GPS; BE nhận lat/long để search. |
| `FEAT-MAP-02` Hiển thị ghim | Không tô priority | Cần cho MVP map. |
| `FEAT-MAP-03` Quick card khi bấm pin | Không tô priority | API trả summary card. |
| `FEAT-SRCH-01` Tìm kiếm địa điểm | Quan trọng | FE có thể gọi map/geocoding provider; BE query theo tọa độ/text normalized. |
| `FEAT-SRCH-02` Bộ lọc nâng cao | Không tô priority | Cần cho rental search MVP. |
| `FEAT-SRCH-03` Cập nhật bản đồ động | Quan trọng | API search/filter phải đủ nhanh và có pagination/bounding box. |
| `FEAT-AI-01` AI input panel | Không tô priority | UI/FE-heavy. |
| `FEAT-AI-02` NLP parsing | Quan trọng + AI làm | Quan trọng về product, nhưng nên phase sau khi search/filter ổn định. Không cho AI sinh SQL. |
| `FEAT-AI-03` AI highlight | Quan trọng + AI làm | Cần data/ranking trước. Phase sau MVP search. |
| `FEAT-AI-04` AI summary/chatbot | Không tô priority | Delay. Chi phí và scope cao. |
| `FEAT-POST-01` Tạo bài đăng/chọn vai trò | Quan trọng | MVP core. |
| `FEAT-POST-02` Định vị tòa nhà trên bản đồ | Quan trọng | MVP core, cần lat/long. |
| `FEAT-POST-03` Ràng buộc dữ liệu & upload media | Không tô priority | MVP core dù không tô màu, vì post không có ảnh thật thì app mất giá trị. |
| `FEAT-POST-04` Kiểm duyệt tự động | Đang cân nhắc + technical | Nên làm rule-based tối thiểu: bad words + phone regex. |
| `FEAT-POST-05` Admin duyệt bài | Đang cân nhắc + admin làm | Cần nếu muốn chống tin rác; MVP nên có approve/reject đơn giản. |
| `FEAT-POST-06` Đóng/archived tin | Không tô priority | Cần để tránh pin rác sau khi đã thuê/tìm được bạn. |
| `FEAT-DET-01` Detail page | Quan trọng | API detail bắt buộc. |
| `FEAT-DET-02` Review cộng đồng | Quan trọng | Quan trọng vì dữ liệu review còn dùng cho AI/search trust. |
| `FEAT-MARKET-01` Đăng bán đồ pass | Quan trọng | Feature khác biệt, nhưng nên làm sau rental post ổn định. |
| `FEAT-MARKET-02` Gợi ý đồ theo vị trí | Đang cân nhắc | Delay; cần marketplace data trước. |
| `FEAT-UTIL-01` Lưu bài yêu thích | Không tô priority | MVP core vì unlock roommate dựa trên favorite/save. |
| `FEAT-UTIL-02` Lịch sử hoạt động | Đang cân nhắc | Delay hoặc logging tối thiểu. |
| `FEAT-UTIL-03` Đánh giá & chấm điểm | Quan trọng | Trùng với review; cần normalize để không tạo 2 flow review khác nhau. |
| `FEAT-MOD-01` Report system | Quan trọng | Cần cho trust/safety. |
| `FEAT-MOD-02` Thống kê bài đăng | Đang cân nhắc | Có thể phase 2; MVP chỉ view/save count cơ bản. |
| `FEAT-NOTI-01` Realtime notification | Quan trọng + technical | Product important, nhưng MVP có thể lưu notification REST trước, SignalR sau. |
| `FEAT-NOTI-02` Notification center | Đang cân nhắc | Nên làm REST center nếu đã có events. |

## Kết luận phạm vi

Homeji không chỉ là app đăng tin trọ. Sản phẩm có 4 domain cốt lõi:

1. Tìm trọ theo bản đồ, filter, review và dữ liệu thực tế.
2. Đăng tin phòng/pass phòng có kiểm duyệt.
3. Tìm bạn ở ghép bằng hồ sơ lối sống và cơ chế double opt-in.
4. Marketplace pass đồ gắn với khu vực/phòng.

Các nhóm tính năng như payment, AI chatbot, realtime SignalR, gói chủ trọ, đẩy bài VIP là phase sau vì phụ thuộc nhiều vào core data model. Riêng AI parsing/highlight và notification realtime được tô “quan trọng” trong tài liệu, nhưng vẫn nên triển khai sau khi search/filter và event model đã ổn định.

## Actor chính

| Actor | Mục tiêu |
|---|---|
| Người thuê / sinh viên | Tìm phòng hợp ngân sách, vị trí, lối sống; lưu bài; chat; đặt lịch; đánh giá; tìm bạn ở ghép; mua đồ pass. |
| Người cho thuê | Đăng và quản lý phòng; nhận liên hệ/lịch hẹn; xem thống kê; xác minh; mua gói/đẩy bài; đăng đồ pass. |
| Admin | Duyệt bài, xử lý report, duyệt xác minh chủ trọ, quản lý nội dung vi phạm. |

Phân quyền backend tối thiểu:

| Permission group | Được làm |
|---|---|
| Authenticated user | Cập nhật profile, lifestyle, save post, gửi report, gửi roommate invite, tạo marketplace post. |
| Renter | Tìm/lưu phòng, gửi lịch hẹn, review/report, tìm bạn ở ghép. |
| Landlord/post owner | Tạo/sửa/archived rental post của mình, xem thống kê bài của mình, phản hồi lịch hẹn. |
| Admin/system owner | Approve/reject post, xử lý report, duyệt xác minh chủ trọ, can thiệp nội dung vi phạm. |

## Flow backend bắt buộc

```text
View/API contract -> Controller -> IService -> Service -> IRepository -> Repository/DAL -> DbContext -> Supabase PostgreSQL
```

Controller không gọi repository, DbContext hoặc Infrastructure trực tiếp.

## Domain module đề xuất

### 1. Identity & Profile

Hiện trạng repo đã có Supabase JWT authentication và `user_profiles`.

Mở rộng cần có:

- user role: renter, landlord, admin;
- avatar, phone, school, preferred area;
- lifestyle profile: sleep habit, pet, smoking, budget, interested areas;
- landlord verification status.

### 2. Posts / Rental Listings

Core entity: `RentalPost`.

Trường chính:

- owner user id;
- post type: vacant room, roommate/pass room;
- title, description;
- price, deposit, area;
- address, latitude, longitude;
- amenities;
- lifestyle tags;
- status: draft, pending, active, rejected, archived, rented;
- view count, save count;
- moderation reason.

Rule quan trọng:

- tối thiểu 3 ảnh;
- price/deposit/area phải dương;
- mô tả bị quét bad words và regex phone number;
- post mới qua trạng thái `Pending`, admin duyệt mới `Active`;
- chỉ `Active` mới xuất hiện public/map.

### 3. Media

Core entity: `PostMedia`.

Hỗ trợ:

- image;
- video;
- 3D/VR URL nếu có;
- thumbnail;
- sort order.

Storage nên dùng Supabase Storage, DB chỉ lưu metadata/url/path.

### 4. Map & Search

Backend cần API:

- search/filter posts theo bounding box hoặc radius;
- filter theo price, area, amenities, lifestyle tags;
- get map pins;
- get post quick card;
- get post detail.

MVP không cần tự gọi Google/Goong geocoding ở backend nếu FE đã chọn lat/long. Backend chỉ validate và lưu lat/long.

### 5. Favorites / Unlock Roommate

Core entity: `SavedPost`.

Rule:

- user save/favorite một post;
- khi đã favorite, API cho phép xem danh sách người khác cũng favorite cùng post;
- tính match score dựa trên lifestyle profile.

### 6. Roommate Matching

Core entities:

- `RoommateInvitation`;
- optional `RoommateMatch`.

Status:

- pending;
- accepted;
- rejected;
- cancelled.

Rule:

- double opt-in: chỉ khi người nhận accept mới mở chat/match;
- không cho tự invite chính mình;
- không gửi duplicate pending invitation cho cùng post + receiver.

### 7. Chat

Phase 1 có thể chỉ lưu conversation/message REST API.

Phase sau thêm SignalR:

- realtime new message;
- typing/read status nếu cần;
- notification counter.

Core entities:

- `Conversation`;
- `ConversationParticipant`;
- `Message`.

### 8. Appointments

Core entity: `ViewingAppointment`.

Status:

- pending;
- confirmed;
- cancelled;
- completed;
- rescheduled.

Rule:

- renter tạo yêu cầu xem phòng;
- landlord confirm/cancel/propose another time.

### 9. Reviews

Core entity: `Review`.

Target:

- rental post;
- landlord;
- marketplace seller nếu cần phase sau.

Rule:

- rating 1-5;
- comment optional nhưng phải kiểm duyệt bad words;
- backend tính lại average rating.

### 10. Marketplace / Pass đồ

Core entity: `MarketplacePost`.

Trường chính:

- seller id;
- title, description;
- price;
- category;
- status: active, sold, archived;
- address/lat/long hoặc link với rental post;
- contact visibility.

API cần:

- list marketplace posts;
- search by radius/near rental post;
- create/update/archive;
- mark sold.

### 11. Reports & Moderation

Core entities:

- `Report`;
- `BadWord`;
- optional `ModerationLog`.

Report targets:

- rental post;
- marketplace post;
- review;
- user;
- message.

Status:

- new;
- reviewing;
- resolved;
- rejected.

### 12. Notifications

Core entity: `Notification`.

Events:

- post approved/rejected;
- new message;
- roommate invitation;
- appointment updated;
- report resolved;
- price/status changed for saved post.

Phase 1: REST notification center.
Phase 2: SignalR realtime push.

### 13. Payments / Monetization

Nên delay khỏi MVP backend đầu tiên.

Các object cần sau:

- subscription plan;
- landlord subscription;
- payment transaction;
- promoted post;
- invoice/refund.

Không nên code payment trước khi chọn provider và policy hoàn tiền/đặt cọc.

### 14. AI Search

Nên delay khỏi MVP đầu tiên.

Phase an toàn:

1. Backend nhận natural language query.
2. AI parse thành JSON filter.
3. Backend validate JSON bằng allowlist.
4. Query rental posts như search thường.

Không cho AI tự sinh SQL.

## MVP backend đề xuất

### MVP-1: Có thể demo end-to-end

1. Profile + lifestyle onboarding.
2. Rental posts CRUD.
3. Media metadata.
4. Post moderation rule-based + admin approve/reject.
5. Public map/search/filter active posts.
6. Save/favorite post.
7. Unlock roommate list sau khi save.
8. Roommate invitation double opt-in.
9. Basic notification center.
10. Reports.

Ghi chú theo màu: `FEAT-AUTH-02`, `FEAT-POST-04`, `FEAT-POST-05` bị tô “đang cân nhắc”, nhưng nếu bỏ hoàn toàn thì roommate matching và trust/safety sẽ yếu. MVP nên làm bản tối thiểu, không làm UI/logic quá nặng.

### MVP-2: Tăng độ thật của sản phẩm

1. Chat REST + SignalR.
2. Viewing appointments.
3. Reviews + average rating.
4. Marketplace pass đồ.
5. Owner post statistics.

### MVP-3: Monetization/AI

1. Landlord verification workflow.
2. Subscription/promoted posts.
3. Payment provider.
4. AI natural language search.
5. AI highlight/ranking.
6. AI summary/recommendations.

Ghi chú theo màu: `FEAT-AI-02` và `FEAT-AI-03` được tô “quan trọng”, nhưng đây là product priority, không có nghĩa là nên code trước DB/search. Nên build search/filter truyền thống trước rồi mới gắn AI parser lên trên.

## Rủi ro sản phẩm

Risk verdict: medium-high.

Lý do: phạm vi hiện tại rộng hơn MVP sinh viên thông thường: rental marketplace, roommate matching, marketplace đồ cũ, realtime chat, admin moderation, payment, AI search.

Assumption rủi ro nhất: sinh viên và chủ trọ có sẵn sàng chuyển hành vi từ Facebook/Zalo/Chợ Tốt sang Homeji hay không.

Validation nên làm trước khi build quá rộng:

- demo MVP quanh Làng Đại học/Thủ Đức;
- onboard 20-50 phòng thật;
- test 20-30 sinh viên có thật sự dùng save/favorite + tìm người ở ghép không;
- chỉ mở payment/AI sau khi core search/post/match có usage.

## Quyết định kỹ thuật

- Supabase Auth là source of identity.
- BE không lưu password.
- Supabase PostgreSQL là database chính.
- Không dùng file cấu hình local riêng, `.env`, .NET User Secrets hoặc environment variables cho application config; toàn bộ runtime config nằm trong `src/Homeji.Api/appsettings.json`.
- API giữ flow `Views -> Services -> DAL`.
- Mọi table app nằm trong schema `homeji`.
- Public data chỉ query qua backend API; không expose trực tiếp bảng app qua Supabase Data API ở giai đoạn này.
