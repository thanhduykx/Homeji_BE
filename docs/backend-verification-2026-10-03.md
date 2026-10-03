# Backend và vận hành dữ liệu — 03/10/2026

Code hiện có được giữ trên `main`, nền `9ba8ac5`; sau `git fetch`, `origin/main` cùng commit tại lúc tích hợp. Không chạy reset, checkout ghi đè, pull hoặc merge một nhánh chưa được người dùng xác định. Bản sao 57 file trước tích hợp và manifest SHA256 nằm ở `output/backups/before-integration-20261003-121836/`. Người dùng sau đó yêu cầu commit và push toàn bộ code đã kết hợp lên `main`; Render được cấu hình tự deploy khi có commit mới.

Trong lúc thực hiện, workspace có thêm code hết hạn thanh toán 15 phút, `PaymentExpirationWorker` và collector Thuephongtro/Muaban. Đã giữ các thay đổi đó, kể cả đăng ký worker chung với worker marketplace và cờ bật/tắt background jobs. Bản sao kết hợp 99 file nằm ở `output/backups/combined-20261003-125311/`. Collector bổ sung hiện tạo snapshot chứng cứ riêng; chưa nhập các nguồn đó vào danh mục đang công bố. Không tự đổi ngữ nghĩa hết hạn hoặc gán tin hết hạn thành phòng còn trống.

## Hợp đồng API mới

Phạm vi: Quận 9 cũ và Thủ Đức. Homeji quản lý món và đơn; tiệm tự giao hoặc khách tới lấy. Tìm món hỗ trợ `sellerId`, `listingType=2`, `nearRentalPostId`, `radiusKm`; truy vấn PostgreSQL lọc vòng tròn và xếp gần nhất **trước phân trang**. Stock, giá, trạng thái món và số lượng giữ chỗ có kiểm tra cạnh tranh; giao dịch ví và stock vẫn nguyên tử.

Đơn giỏ hàng có một `checkoutId` chung; API tiếp tục trả các dòng đơn để tương thích client hiện có. Mặc định `fulfillment.mode=0` là nhận tại tiệm và vẫn yêu cầu `pickupAddress`. Mode `1` là tiệm giao; dùng địa chỉ tiệm từ món, lưu thông tin người nhận theo đơn. `pickupAt` vẫn là tên field cũ và phải ở tương lai, biểu thị thời điểm nhận/giao mong muốn.

```json
{
  "items": [{"postId": "<mã món>", "quantity": 1}],
  "pickupAt": "<thời điểm tương lai, ISO 8601>",
  "fulfillment": {
    "mode": 1,
    "delivery": {
      "recipientName": "Người nhận",
      "recipientPhone": "0901234567",
      "address": "Địa chỉ tại Thủ Đức",
      "latitude": 10.85,
      "longitude": 106.77
    }
  }
}
```

`POST /api/marketplace-orders/cart` nhận hợp đồng trên. `GET /api/marketplace-orders/{id}` chỉ cho người mua/người bán liên quan đọc, bao gồm thông tin giao hàng. Không thay đổi phí nền tảng, thời gian escrow hoặc thêm phí vận chuyển. Địa chỉ giao vẫn kiểm tra vùng tọa độ đã cấu hình trong repo; bounding box không phải ranh giới hành chính chính xác.

## Tin trọ thật và làm sạch dữ liệu

Đã nhập vào Supabase Homeji `pzxxzcrgpfsbdkyrdvng`, bảng `homeji.rental_source_listings`: **20 tin, 150 URL ảnh**, gồm Quận 9: 10 tin/77 ảnh; Thủ Đức: 10 tin/73 ảnh. Nguồn là hai trang công khai [Quận 9](https://phongtro123.com/tinh-thanh/ho-chi-minh/quan-9) và [Thủ Đức](https://phongtro123.com/tinh-thanh/ho-chi-minh/quan-thu-duc) của Phongtro123. Bộ thu thập đọc robots, giới hạn số tin, giãn request, chỉ cho HTTPS tới host nguồn/CDN, chặn redirect và kiểm tra MIME/chữ ký ảnh. Không đọc API bị robots chặn.

Tin phải có breadcrumb đúng khu vực, địa chỉ không chứa địa phương bị loại, giá/diện tích rõ ràng và ảnh từ gallery của chính tin. Một tin có tiêu đề 30 m² nhưng khối diện tích ghi 20 m²: lưu 20 m² theo field chi tiết, giữ tiêu đề nguyên gốc. Không suy đoán tọa độ, tiền cọc, tình trạng còn phòng hoặc quyền sở hữu. Ảnh lưu bằng URL nguồn trong database, không tải lại vào Storage; link có thể thay đổi khi nguồn sửa/xóa tin. Không tạo tài khoản chủ trọ giả hoặc gắn tin ngoài vào chủ trọ Homeji.

API dành cho frontend:

```text
GET /api/rental-source-listings?district=quan-9&page=1&pageSize=20
GET /api/rental-source-listings?district=thu-duc&maxPrice=3000000
GET /api/rental-source-listings/{id}
```

Kết quả chứa `source`, `sourceId`, `sourceUrl`, `imageUrls`, `sourceUpdatedAt`, `collectedAt`; bộ lọc có validation và rate limit. Chưa triển khai API mới lên Render; frontend cần dùng route này khi bản backend mới được deploy. Luồng đăng tin/pass trọ bản địa tiếp tục dùng `/api/rental-posts`.

Đã làm sạch đúng **68 bản ghi**: 44 tin trọ, 23 tin marketplace và tên profile `Homeji Demo Admin` → `Homeji Admin`. Bỏ `[PRIMARY_ACCOUNTS_FULL_V1]`, `[Mẫu]`, marker `HOMEJI_*`/`DEMO_KEY` và hậu tố `demo`; giữ các nội dung có ý nghĩa như “hợp đồng mẫu”. Các tin giả lập giữ `isSynthetic=true` trong dữ liệu và DTO, nên việc bỏ nhãn không biến chúng thành tin thật. Không xóa tài khoản, giao dịch, tin hoặc thay nội dung địa chỉ/giá. Bản ghi cũ ngoài khu vực vẫn được giữ; đợt nhập mới chỉ gồm hai khu vực đã chốt.

Backup trước/sau và SQL đối chiếu nằm ở `output/data-import/`. Mỗi UPDATE kiểm tra ID, title, description và thời điểm sửa cũ; thay đổi đồng thời sẽ được bỏ qua thay vì ghi đè. Tác vụ sửa địa chỉ/tiêu đề seed lúc khởi động nay mặc định tắt; chỉ bật thủ công `LegacyData:NormalizeLocationsOnStartup=true` cho việc sửa dữ liệu cũ.

Snapshot: `rental-sources-20261003-122943.json`, SHA256 `fbd5a3495bbe18789da608f31d1bdea309eed963b709360d850d2e49af48a81d`. Bộ thu thập tạo JSON và SQL giao dịch; chạy lại dùng unique `(source, source_id)` và không cập nhật đè phiên bản mới hơn. Snapshot/backup/log được Git ignore.

```powershell
py -m venv output/data-import/venv
output/data-import/venv/Scripts/python.exe -m pip install -r scripts/data/requirements.txt
output/data-import/venv/Scripts/python.exe -X utf8 scripts/data/collect_rental_sources.py
# Xem snapshot và SQL trước khi nhập; collector không tự kết nối database.
pwsh -File scripts/quality/Test-LocalBackend.ps1 -SourceImportSql <file-sql-vừa-tạo>
pwsh -File scripts/quality/Test-ImportedListings.ps1 -Snapshot <file-json-vừa-tạo>
```

Lệnh thứ hai chạy API loopback, đọc database đã cấu hình và so sánh với snapshot, không viết dữ liệu. SQL nhập đã được thử hai lần trên PostgreSQL riêng trước khi nhập Supabase. Bảng nguồn bật RLS, revoke quyền của PUBLIC/anon/authenticated; truy cập sản phẩm đi qua backend.

## Kiểm chứng và cấu hình còn cần khi triển khai

- Build nghiêm ngặt: 0 warning/0 error; EF không có model change chưa tạo migration.
- PostgreSQL cục bộ tại snapshot kết hợp: **121 unit + 75 integration = 196 passed, 0 skipped**; thêm 17 kiểm thử Python passed. Có kiểm tra SQL thực, gallery ảnh, escape wildcard, phạm vi DB, API nguồn, stock/wallet cạnh tranh, nhóm đơn của phiên bản backend cũ và xử lý hết hạn thanh toán. Các kiểm thử bổ sung kiểm tra mốc 899/900/901 giây, quyền của người dùng và thành công thanh toán xác thực đến muộn. Manifest toàn bộ source/tests xác nhận không có file sửa/thêm trong lượt kiểm chứng đó. Log: `output/quality/combined-final-verification.log`. Trước push, build mới nhất vẫn 0 warning/0 error; suite thanh toán mở rộng có 10 kiểm thử passed, gồm hai kiểm thử mới về deadline PayOS và concurrency token; 20 kiểm thử Python passed sau khi bổ sung ba kiểm thử SQL import insert-only.
- k6 gần nhất: 5 VU/30 giây, hơn 2.000 món, 931 request, 0 lỗi, 3.724/3.724 checks; p95 **11,31 ms**, max 466,42 ms. Benchmark dùng PostgreSQL 18 trên máy cục bộ và quota tìm kiếm riêng; không phải latency production Supabase 17.
- API cục bộ kết nối Supabase thật đã đối chiếu đủ 20 tin/150 ảnh qua route mới; chứng cứ `output/quality/imported-live-api-verification.log`.
- Sonar C# analyzer cục bộ đã chạy lại: 52 diagnostics còn lại, gồm maintainability và 2 hotspot S5693 về upload 50/42 MiB. Giữ giới hạn upload tương thích các kiểm tra số ảnh/kích thước ảnh hiện có; chưa có quality gate Cloud. Không còn S6444/S5332 trong báo cáo; NuGet audit không tìm thấy package có lỗ hổng đã biết tại lúc chạy, kể cả transitive. Log: `output/quality/quality-combined-latest.log`.
- Hai migration `AddMarketplaceFulfillmentAndCheckout` và `AddRentalSourceListings` đã áp dụng Supabase; không có checkout ID bằng zero. Trigger tương thích gán ID nhóm cho backend cũ còn chạy, còn code mới tự cung cấp ID. `search_path` function được cố định; không dùng security definer.
- Supabase advisor hiện không có ERROR; còn cảnh báo [leaked-password protection](https://supabase.com/docs/guides/auth/password-security#password-strength-and-leaked-password-protection) đang tắt. INFO RLS không có policy là trạng thái deny trực tiếp cho các bảng backend sở hữu; không mở policy public để xóa cảnh báo.

Security thay đổi: chỉ tin forwarded headers từ proxy đã khai báo, 429 có Retry-After, quota cho AI/upload, regex có timeout, SMTP yêu cầu TLS, Production không trả stack trace, Kestrel không trả Server header. Cấu hình production cần khai báo proxy/IP thực của host để quota phân biệt client.

Secret đã bỏ khỏi appsettings theo dõi Git, giữ trong `appsettings.Local.json` được ignore và không publish. Production dùng environment variables; Blueprint khai báo `ConnectionStrings__DefaultConnection` là secret `sync: false`. Service Render đã tồn tại cần có giá trị này trong Environment; Blueprint không tự điền secret cho service cũ. **Cần xoay vòng các secret từng nằm trong lịch sử Git và cập nhật môi trường triển khai**; việc bỏ giá trị trong commit hiện tại không thu hồi secret cũ. Không tự thay credentials đang được service khác sử dụng.

Để chạy SonarCloud, cần đăng nhập Browser, chọn project/organization và đặt `SONAR_TOKEN`, `SONAR_PROJECT_KEY`, `SONAR_HOST_URL`, `SONAR_ORGANIZATION` trong môi trường máy; không gửi token qua chat. Chạy `pwsh -File scripts/quality/Test-Quality.ps1 -ServerAnalysis`. Chưa upload code hoặc xác nhận quality gate của Cloud vì các thông tin này chưa có.
