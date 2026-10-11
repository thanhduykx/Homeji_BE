# Log dữ liệu Quận 9 — 11/10/2026 (Asia/Saigon)

## Trạng thái

**Đã cập nhật và kiểm tra database Supabase thành công lúc 19:48–19:49 ngày 11/10/2026 (UTC+7). CHƯA deploy code website/backend mới lên Render.**

### Kết quả thực thi

- Database đích: `aws-1-ap-south-1.pooler.supabase.com:5432`, database `postgres`, project `pzxxzcrgpfsbdkyrdvng`. Không ghi mật khẩu vào log.
- Người dùng đã cung cấp kết nối; lưu trong `src/Homeji.Api/appsettings.Local.json`, xác nhận được Git ignore.
- Kiểm tra trước khi ghi: migration mới nhất là `20261009072205_AddIndependentRoommateDiscovery`, chưa có bảng quán; bảng nguồn trọ và cột thời hạn đã có.
- Áp dụng riêng migration `20261011091052_AddFoodSourcePlaces`; đã xác nhận lịch sử migration và bảng mới tồn tại. RLS bật.
- **Thêm mới 5 quán ăn**, ID lần lượt `5099783a-32a0-4b43-9c4b-2c5d4f6f1011` đến `5099783a-32a0-4b43-9c4b-2c5d4f6f1015`, đúng thứ tự bảng quán bên dưới.
- **Thêm mới tin trọ 711972**, ID `7d7ca99d-0101-4c11-8111-000000711972`, 2.300.000 đồng/tháng, 20 m², 1 URL ảnh, thời hạn đúng nguồn.
- **Giữ nguyên tin trọ 711906 đã tồn tại**, ID `844521fd-8438-57b9-bcd9-6c9858dcb723`, 2.700.000 đồng/tháng, 16 m² và 6 URL ảnh có sẵn. Không tạo bản sao hoặc rút bớt ảnh.
- Kiểm tra sau commit: có đủ 5 quán và 2 mã tin mục tiêu. Repository `FoodSourcePlaceRepository` đọc bằng EF Core từ database thật trả được cả 5 quán, nguồn, địa chỉ và ghi chú.
- Receipt đầy đủ, không có connection string/mật khẩu: [quan9-data-import-receipt-20261011.json](quan9-data-import-receipt-20261011.json).
- Chưa deploy code mới và chưa kiểm tra giao diện website production. Dữ liệu đã ở database; endpoint quán/UI mới cần phiên bản code mới trên Render.

- Dataset: `scripts/data/quan9-source-data-20261011.json`.
- SQL nhập: `scripts/data/quan9-import-20261011.sql` — 5 quán ăn, 2 tin trọ, không ghi đè mục đã tồn tại.
- Migration: `20261011091052_AddFoodSourcePlaces` tạo bảng `homeji.food_source_places`.
- Quán ăn là danh bạ có nguồn: không tạo tài khoản người bán, giá món, tồn kho, đặt hàng hay tọa độ giả.
- Tin trọ vào bảng có sẵn `homeji.rental_source_listings`: giữ mã tin, link nguồn, giá, diện tích, ảnh và thời hạn nguồn; không biến thành tin chủ nhà đã xác minh.
- Ảnh là URL từ chính gallery của tin, không tải lại lên storage. Việc tải ảnh qua công cụ nghiên cứu thất bại; chưa xác minh ảnh với phòng thực tế hoặc quyền tái sử dụng.
- Link Google Maps là tìm kiếm tên/địa chỉ, không phải place ID/ghim đã kiểm chứng. Không đọc được Maps bằng công cụ nghiên cứu.
- Nguồn web được đọc qua kết quả/cached page của công cụ internet. Mốc kiểm tra lưu trong JSON là thời điểm rà soát nguồn trong phiên này, không phải thời điểm xác nhận trực tiếp với chủ nhà.

## Quán ăn chuẩn bị nhập

| Quán | Địa chỉ theo nguồn | Nguồn đầy đủ | Giới hạn |
|---|---|---|---|
| Bánh Tráng Trộn Má Bé | 95 Đường 475, Phước Long B, Quận 9 cũ | [Nhà Tốt](https://www.nhatot.com/kinh-nghiem/dia-diem-an-vat-quan-9.html), [danh mục Foody](https://www.foody.vn/%28A%28%26%20%20%20%20amp%3Bquot%3B-cpjdosfhwxnr-%26%20%20%20%20amp%3Bquot%3B%29%29/ho-chi-minh/khu-vuc-tp-thu-duc/tren-duong-duong-475) | Chưa xác nhận tình trạng hoạt động |
| Helios – Ăn Vặt & Trà Sữa | 17 Đinh Củng Viên, Phước Long A, Quận 9 cũ | [ShopeeFood](https://shopeefood.vn/ho-chi-minh/helios-an-vat-tra-sua-dinh-cung-vien), [Nhà Tốt](https://www.nhatot.com/kinh-nghiem/dia-diem-an-vat-quan-9.html) | Chưa xác nhận tình trạng hoạt động |
| Ăn Vặt Xứ Nẫu | 77 Đường Làng Tăng Phú, Tăng Nhơn Phú A, Quận 9 cũ | [VinWonders](https://vinwonders.com/vi/wonderpedia/news/an-vat-quan-9-tphcm/), [Nhà Tốt](https://www.nhatot.com/kinh-nghiem/dia-diem-an-vat-quan-9.html) | Chưa xác nhận tình trạng hoạt động |
| Tiệm Chè Đà Lạt Hương Dung | 196 Nguyễn Văn Tăng, Long Thạnh Mỹ, Quận 9 cũ | [Nhà Tốt](https://www.nhatot.com/kinh-nghiem/dia-diem-an-vat-quan-9.html) | Chưa đối chiếu nguồn thứ hai hoặc tình trạng hoạt động |
| Chóp Chép – Bánh Tráng Trộn Tóp Mỡ Lòng Đào | 1 Đường số 1, Quận 9 cũ | [Nhà Tốt](https://www.nhatot.com/kinh-nghiem/dia-diem-an-vat-quan-9.html) | Nguồn chưa ghi phường; chưa đủ địa chỉ để xác định vị trí chính xác. Ghi chú này hiển thị trên website |

Giữ cách gọi phường trong nguồn, không tự chuyển thành địa chỉ hành chính hiện hành. Không tự thêm giá món, giờ mở cửa, đánh giá hoặc ảnh quán.

## Tin trọ chuẩn bị nhập

### 711972 — 976/2 Nguyễn Duy Trinh, Long Trường

- [Tin gốc](https://phongtro123.com/con-duy-nhat-1-phong-cho-thue-976-nguyen-duy-trinh-pr711972.html).
- Giá theo nguồn: 2.300.000 đồng/tháng; diện tích 20 m².
- Cập nhật 22/08/2026 19:25; hết hạn 21/10/2026 19:25, UTC+7.
- [URL ảnh trong gallery tin](https://pt123.cdn.static123.com/images/thumbs/900x600/fit/2026/08/22/1787401427046-120031049874458534-910286588504228269-d681a630d3313aca9fd7f30dbcd05c10_1787401500.jpg).
- Chưa xác nhận còn phòng. Không nhập các nhãn tiện ích chung của trang vì không có đủ đối chiếu với mô tả.

### 711906 — phòng mini cho nữ, 24 Đường số 6, Phước Long

- [Tin gốc](https://phongtro123.com/cho-nu-thue-phong-dang-can-ho-mini-pr711906.html).
- Giá theo nguồn: 2.700.000 đồng/tháng; diện tích 16 m².
- Cập nhật 21/08/2026 15:46; hết hạn 20/10/2026 15:46, UTC+7.
- [URL ảnh trong gallery tin](https://pt123.cdn.static123.com/images/thumbs/900x600/fit/2026/08/21/img-4773_1787301919.jpg).
- Chưa xác nhận còn phòng. Thông tin giới hạn cho nữ được giữ trong tiêu đề. Không tự điền phí, tiền cọc hoặc tiện ích chưa xác minh.

## Mục loại khỏi gói nhập

- **200197 — 86/22T Đình Phong Phú:** nguồn ghi **Đã cho thuê**, hết hạn 18/08/2026. [Tin gốc](https://phongtro123.com/tinh-thanh/ho-chi-minh/tung-bung-khai-truong-khu-tro-cao-cap-q9-uu-tien-nhan-vien-van-phong-lh-0909-55-26-29-ms-ngoc.html), [ảnh nguồn](https://pt123.cdn.static123.com/images/thumbs/900x600/fit/2025/10/19/a62c662b-9ca3-4ba5-bdd5-e047bba30bc8_1760837389.jpg).
- **650756:** địa chỉ 216/34 ở trường địa chỉ và 261/34 Đình Phong Phú trong mô tả, hết hạn 28/06/2026. [Tin gốc](https://phongtro123.com/phong-moi-dep-gan-nga-4-thu-duc-khu-cnc-20m2-co-gac-vi-tri-tien-loi-pr650756.html).
- **126488 — 56 Đường 385:** tin hết hạn từ 19/08/2020. [Tin gốc](https://phongtro123.com/tinh-thanh/ho-chi-minh/cho-thue-phong-tai-134-binh-loi-p-13quan-binh-thanh.html).

Không xóa/sửa các mục cũ nếu chúng đã có trong database; gói nhập này chỉ thêm các mục được nêu ở trên.

## Thay đổi website/backend

- API đọc công khai `GET /api/food-source-places`, tối đa 50 mục; đi qua Controller → Service → Repository, không có API ghi công khai.
- Trang Đồ ăn có nút xem danh sách quán cùng link nguồn và Maps; có trạng thái lỗi, tải lại và danh sách rỗng.
- Mục Tin từ nguồn ngoài hiển thị tối đa 3 ảnh nguồn với thông báo chưa xác minh; ảnh lỗi được ẩn.
- Sửa bộ lọc khu vực từ `quan9`/`thuduc` thành `quan-9`/`thu-duc` để khớp API.
- Bảng mới bật RLS, không cấp quyền cho PUBLIC/anon/authenticated; đọc qua backend như bảng tin nguồn hiện có.

## Kiểm tra cấu hình kết nối

- `src/Homeji.Api/appsettings.json` chỉ có `Host=localhost;Port=5432;Database=homeji;Username=homeji`, không có mật khẩu.
- Không thấy `appsettings.Local.json`, file `.env` chứa kết nối trong hai workspace, biến môi trường kết nối hiện tại, hoặc file User Secrets.
- Cổng PostgreSQL localhost 5432 không có dịch vụ nhận kết nối.
- `render.yaml` khai báo `ConnectionStrings__DefaultConnection` với `sync: false`: giá trị được cấu hình riêng trên Render, không nằm trong repo. Chưa truy cập dashboard Render/Supabase.
- Không đọc hoặc sửa file `dataa.sql` của dự án STELLA CITY; website Homeji dùng schema PostgreSQL khác.

## Cách hoàn tất sau khi có kết nối

Lưu cấu hình thật vào `src/Homeji.Api/appsettings.Local.json` (đã được repo bỏ qua), hoặc đặt biến môi trường `ConnectionStrings__DefaultConnection`. Không commit mật khẩu, không gửi connection string vào log.

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=<host>;Port=<port>;Database=<database>;Username=<user>;Password=<password>;SSL Mode=Require"
  }
}
```

Sau khi kiểm tra đúng database đích và lịch sử migration, áp dụng migration mới; không tự chạy tất cả migration cũ vào database chưa kiểm tra. File `scripts/data/quan9-food-schema-20261011.sql` là script idempotent dành riêng cho migration mới. Có thể chạy trong Supabase SQL Editor bằng tài khoản quản trị, rồi chạy `scripts/data/quan9-import-20261011.sql`.

SQL nhập có transaction, giới hạn thời gian khóa/truy vấn, chặn nhập nếu tin trọ đã quá ngày hết hạn tại thời điểm chạy, `ON CONFLICT DO NOTHING`, và truy vấn kết quả để đối chiếu 5 quán/2 mã tin. Các mục đã tồn tại được giữ nguyên. Kết quả `RETURNING` cho biết mục thực sự thêm mới; truy vấn cuối cho biết mục đã có, không chứng minh nội dung mục cũ giống dataset.

Kết quả chạy đã ghi ở đầu log và receipt; phần hướng dẫn này được giữ để tham khảo. **Deploy và kiểm tra UI production chưa thực hiện.**

## Validation

### Thử cập nhật trực tiếp theo yêu cầu người dùng — 11/10/2026

- Đã kiểm tra lại cấu hình: `appsettings.json` vẫn dùng localhost, không có mật khẩu; chưa có `appsettings.Local.json` hoặc biến môi trường kết nối.
- Inventory computer-use trả về không có trình duyệt/tab được kết nối. Thử mở Supabase Dashboard bằng in-app browser thất bại với `Browser is not available: iab`.
- Không có kết nối database có quyền ghi hoặc phiên Dashboard để thực thi SQL. **Không chạy migration/import và không thay đổi database.**
- Cần người dùng cung cấp cấu hình kết nối local có quyền ghi hoặc kết nối phiên trình duyệt đã đăng nhập Supabase; không cần gửi mật khẩu vào chat.

- Frontend production build và lint các file thay đổi: đã pass.
- Backend build sau migration: đã pass, 0 warning/error.
- Test API danh bạ quán: 1/1 pass, kiểm tra truy cập anonymous và giữ nguyên nguồn/ghi chú địa chỉ chưa chắc chắn mà không tạo tài khoản người bán.
- EF kiểm tra model và migration snapshot: không có thay đổi model chưa được ghi trong migration.
- Script schema được sinh từ migration trước đến đúng `AddFoodSourcePlaces`, không bao gồm các migration cũ khác; chưa chạy trên database thật.
- Generator kiểm tra mã tin, URL nguồn/ảnh, dữ liệu số, trùng quán, mục loại và ngày hết hạn trước khi sinh SQL: đã pass, xuất 5 quán + 2 tin trọ.
- Database thật đã được kiểm tra bằng SQL và EF Core sau khi người dùng cung cấp kết nối. End-to-end trên website production chưa kiểm tra vì chưa deploy code mới.
