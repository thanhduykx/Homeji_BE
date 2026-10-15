# Làm sạch dữ liệu phòng trọ — 15/10/2026

## Kết quả database đã áp dụng

Đã cập nhật database Homeji đang được backend production sử dụng. Đây là kết quả đọc trực tiếp database và API, không suy ra từ tài liệu seed.

| Hạng mục | Trước | Sau |
|---|---:|---:|
| Rental Post Active | 53 | 2 |
| Active có cờ giả lập | 40 | 0 |
| Tổng Rental Post có cờ giả lập | 44 | 55 |
| Tin nguồn | 101 | 41 |
| Tin nguồn Quận 9 | 41 | 21 |
| Tin nguồn quá hạn theo dữ liệu lưu | 80 | 0 |

- Đánh dấu thêm 11 Rental Post: 8 tin dùng ảnh Unsplash và 3 bản ghi fixture có ảnh đường dẫn mẫu. Đưa tổng cộng 51 tin Active sang Archived; giữ nguyên bản ghi, ảnh, giá, địa chỉ và các liên kết nghiệp vụ.
- Hai Rental Post còn Active dùng ảnh upload: Saigon Gateway và Centum Wealth. Không coi ảnh upload/cờ không giả lập là chứng nhận chủ sở hữu hoặc phòng còn trống. Không sửa giá hay đổi tên căn hộ thành phòng trọ.
- Đối chiếu lại ngày hết hạn từ nguồn, cập nhật dữ kiện và gallery của 20 tin nguồn đang có, thêm 8 tin Quận 9 chưa quá hạn. 68 quảng cáo hết hạn được đưa khỏi bảng đang phục vụ danh mục. Các tin này vẫn có đầy đủ trong bản sao trước cập nhật.
- Toàn bộ 41 tin nguồn còn lại có `source_checked_at` ngày 15/10/2026. Giữ riêng 21 tin Quận 9 và 20 tin Thủ Đức; không đổi địa bàn của tin để tăng số lượng Quận 9.
- Không tạo chủ trọ, tài khoản liên hệ, tiền cọc, tiện ích, tọa độ hoặc chứng nhận mới từ tin nguồn. Quảng cáo KTX/ở ghép/theo tuần giữ nguyên tiêu đề và giá gốc; giá nguồn không được hiểu mặc định là giá thuê nguyên phòng mỗi tháng.

API khách chưa đăng nhập giới hạn 3 kết quả. Kết quả xem trước trước đây không phải tổng số phòng trong database. Sau cập nhật, `GET /api/rental-posts` trả hai tin không giả lập; `GET /api/rental-source-listings?district=quan-9&pageSize=50` trả 21 quảng cáo nguồn, không có ngày hết hạn đã qua tại lúc kiểm tra.

## Thay đổi code chống tái diễn

- Public rental search luôn loại `IsSynthetic` trước phân trang, không phụ thuộc cờ tùy chọn.
- Chính sách `IsPublicActive` loại dữ liệu giả lập, áp dụng cho tìm kiếm, so sánh, saved rooms và các luồng dùng chung chính sách.
- Người không phải chủ tin không mở được trang chi tiết của tin giả lập, kể cả khi tin vô tình được đặt Active.
- Repository tin nguồn loại quảng cáo quá hạn trước phân trang và khi đọc chi tiết; dùng `TimeProvider` để kiểm thử mốc thời gian.

Các thay đổi code đang ở worktree, chưa commit/push/deploy trong công việc này. Thay đổi dữ liệu đã có hiệu lực trên production; việc tự loại tin khi tới ngày hết hạn trong tương lai cần triển khai bản backend chứa bộ lọc mới.

## Công cụ và bản sao

- `scripts/data/quarantine-synthetic-rentals-20261015.sql`: cập nhật có thể chạy lặp lại, giữ nguyên nội dung và liên kết.
- `scripts/data/refresh_rental_catalog.py`: kiểm tra nguồn hiện có và thu thập bổ sung giới hạn trong Quận 9, tuân theo robots, không ghi DB.
- `scripts/data/render_rental_refresh_sql.py`: kiểm tra dữ liệu, khóa bảng trong transaction, từ chối khi dữ liệu khác bản sao đã đọc, giữ ID khi cập nhật và chỉ xóa tin nguồn đã quá hạn trong bản sao.
- `scripts/data/RentalCatalogRepair`: xác nhận đúng database đích, sao lưu bốn bảng trước khi thực thi SQL trong transaction. Không đưa secret vào tham số hoặc log.
- `scripts/data/check_rental_catalog_images.py`: kiểm tra loại và chữ ký file ảnh gốc, dừng thu thập khi bị rate limit, không sao chép ảnh vào Homeji Storage.

Bằng chứng nằm trong `output/data-import/` (Git ignored):

- `rental-repair-20261015-040336/`: dữ liệu ban đầu.
- `rental-repair-20261015-040713/`: bản sao trước khi ẩn dữ liệu giả lập, SQL đã áp dụng và kết quả.
- `rental-refresh-20261015.json` và `.sql`: dữ kiện nguồn, lỗi và SQL cập nhật.
- `rental-repair-20261015-041248/`: bản sao trước cập nhật tin nguồn.
- `rental-recheck-20261015.json` và `.sql`: kiểm tra lại đầy đủ tin 609391 sau lỗi tải ảnh tạm thời.
- `rental-repair-20261015-041842/`: snapshot sau các cập nhật; 2 Active, 0 Active giả lập, 41 tin nguồn, 0 quá hạn.
- `rental-gallery-check-20261015.json`: 269/269 đường dẫn ảnh gốc trả file có chữ ký ảnh hợp lệ; không có lỗi hoặc ảnh thay thế.

Trong quá trình thực hiện có thêm một draft do hoạt động ngoài phiên này (74 → 75 Rental Post). Không sửa hoặc xóa draft đó.

## Kiểm chứng

- 462 application unit tests pass.
- 131 API tests pass, 13 kiểm thử database opt-in được skip. Trong đó 4 kiểm thử truy vấn PostgreSQL xác nhận lọc giả lập mặc định và lọc hạn tin nguồn trước phân trang mà không cần mở kết nối; không dùng production để chạy test.
- 27 kiểm thử collector/batch/SQL Python pass.
- Đã mở kiểm tra trực quan mẫu ảnh phòng gốc; việc kiểm tra ảnh và quảng cáo không xác minh tình trạng phòng thực tế.
