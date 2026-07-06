# Homeji Backend Feature Map

Nguồn: Google Doc “User Manual” do team cung cấp.

## Kết luận phạm vi

Homeji không chỉ là app đăng tin trọ. Sản phẩm có 4 domain cốt lõi:

1. Tìm trọ theo bản đồ, filter, review và dữ liệu thực tế.
2. Đăng tin phòng/pass phòng có kiểm duyệt.
3. Tìm bạn ở ghép bằng hồ sơ lối sống và cơ chế double opt-in.
4. Marketplace pass đồ gắn với khu vực/phòng.

Các nhóm tính năng như payment, AI, realtime chat, notification, gói chủ trọ, đẩy bài VIP là phase sau vì phụ thuộc nhiều vào core data model.

## Actor chính

| Actor | Mục tiêu |
|---|---|
| Người thuê / sinh viên | Tìm phòng hợp ngân sách, vị trí, lối sống; lưu bài; chat; đặt lịch; đánh giá; tìm bạn ở ghép; mua đồ pass. |
| Người cho thuê | Đăng và quản lý phòng; nhận liên hệ/lịch hẹn; xem thống kê; xác minh; mua gói/đẩy bài; đăng đồ pass. |
| Admin | Duyệt bài, xử lý report, duyệt xác minh chủ trọ, quản lý nội dung vi phạm. |

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
5. AI summary/recommendations.

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
- Không dùng env theo yêu cầu project; dùng `appsettings.Local.json`.
- API giữ flow `Views -> Services -> DAL`.
- Mọi table app nằm trong schema `homeji`.
- Public data chỉ query qua backend API; không expose trực tiếp bảng app qua Supabase Data API ở giai đoạn này.
