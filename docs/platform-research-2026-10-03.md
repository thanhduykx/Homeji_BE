# Homeji — tham khảo nền tảng thuê trọ và đồ ăn quanh trọ

Ngày kiểm tra nguồn: **03/10/2026**. Phạm vi: nguồn chính thức Phongtro123, Nhà Tốt, GrabMerchant, Shopee Partner, SonarSource và Grafana; đối chiếu mã backend hiện có. Báo cáo này không tự chốt mô hình giao hàng, địa bàn, hoa hồng hoặc chính sách thanh toán.

## Kết luận để triển khai

Homeji đã có nền tảng thuê trọ, ở ghép, pass phòng, tìm mua đồ/đồ ăn theo vị trí và checkout. Công việc có giá trị trực tiếp với yêu cầu của người dùng là làm đúng tìm kiếm quanh trọ, xử lý đồng thời tồn kho/thanh toán, giữ quyền sở hữu tài nguyên và kiểm chứng backend bằng dữ liệu thật. Không cần sao chép toàn bộ siêu ứng dụng để phục vụ sinh viên và tiệm nhỏ.

Mô hình giao hàng và địa bàn còn chờ người dùng quyết định. Dữ liệu hiện có mô tả thời gian/địa chỉ nhận hàng; chưa đủ để khẳng định backend đã hỗ trợ đội tài xế, phí giao hàng và định tuyến. Các phần đó phải được thiết kế sau khi chốt ai chịu trách nhiệm giao.

## Quan sát từ nguồn sơ cấp

| Nền tảng | Quan sát có nguồn | Hàm ý cho Homeji — khuyến nghị, chưa phải yêu cầu mới |
|---|---|---|
| Phongtro123 | Trang công khai có danh mục phòng trọ/ở ghép, tin đã lưu, bộ lọc giá và diện tích, khu vực và tin mới; tin hiển thị giá/tháng, diện tích, ảnh và thời gian. [Trang chính thức](https://phongtro123.com/) | Ưu tiên ngân sách, vị trí, tình trạng phòng và freshness trong tìm kiếm. Tin đã thuê/đã pass phải rời tập public. |
| Nhà Tốt | Trang tin phòng trọ thể hiện ảnh, giá, địa chỉ, bản đồ, lưu tin và báo cáo. Một tin mẫu thể hiện trường tiền cọc riêng và thông tin nội thất. Đây là quan sát giao diện, không xác thực các lời quảng cáo của người đăng. [Tin mẫu có báo cáo](https://www.nhatot.com/thue-phong-tro-quan-3-tp-ho-chi-minh/133284258.htm), [tin mẫu có cọc](https://www.nhatot.com/thue-bat-dong-san-quan-cau-giay-ha-noi/133991315.htm) | API phải phân biệt dữ liệu niêm yết với trạng thái kiểm chứng. Nên giữ giá thuê, tiền cọc và phí pass riêng; không suy ra chi phí thật từ mô tả tự do. |
| GrabMerchant | Tài liệu phân biệt quy trình giao chuẩn bằng đối tác tài xế với lấy tại quán và đặt trước. Trang đối tác giới thiệu quản lý đơn và thanh toán bằng GrabMerchant. [Các hình thức nhận đơn](https://merchant.grab.com/vn-vn/guides/quan-ly-cua-hang/quan-ly-don-hang-lay-tai-quan-va-don-hang-dat-truoc), [trang đối tác](https://www.grab.com/vn/merchant/) | Mô hình nhận hàng phải rõ trong hợp đồng API. Pickup, người bán tự giao và đội tài xế của nền tảng khác nhau về phí, quyền chuyển trạng thái và dữ liệu địa chỉ. |
| Shopee Partner | Hướng dẫn chính thức có thêm/sửa món, thời gian bán, topping, lịch mở/đóng thường ngày và ngày đặc biệt, trạng thái quán bận, quản lý đơn và tài chính. Hướng dẫn tài khoản phân biệt admin với quản lý/nhân viên. [Nhập môn](https://merchant.shopeefood.vn/edu/article/nhap-mon-cho-chu-quan-moi), [phân quyền](https://merchant.shopeefood.vn/edu/article/quan-ly-tai-khoan) | Tiệm nhỏ cần tạm ngừng nhận đơn và thể hiện món còn bán. Lịch quán, topping, tài khoản nhân viên là khả năng tham khảo cho giai đoạn sau; không nên thêm vào phạm vi hiện tại chỉ vì đối thủ có. |

Không dùng số lượt truy cập, số tin hay lời khẳng định “uy tín” do nền tảng tự công bố để suy ra chất lượng thị trường. Không coi các giá trong một tin mẫu là benchmark thị trường hoặc làm căn cứ tự đặt ngưỡng giá cho Homeji.

## Đối chiếu backend hiện có

Các liên kết dưới đây là mã nguồn/tài liệu trong repository tại thời điểm đọc; cần kiểm tra lại nếu implementation thay đổi trong cùng đợt phát triển.

| Nhu cầu | Có sẵn | Gap/rủi ro quan sát và hướng xử lý |
|---|---|---|
| Sinh viên tìm trọ / ở ghép / pass phòng | Enum có `VacantRoom`, `RoommateShare`, `RoomTransfer`; `RentalPost` có phí pass, lý do, đồng ý chủ nhà và dấu vết người duyệt. [Enum](../src/Homeji.Domain/Enums/RentalPostType.cs), [entity](../src/Homeji.Domain/Entities/RentalPost.cs) | Giữ domain pass phòng độc lập với pass đồ. Kiểm thử tin pass không được public nếu thiếu xác nhận/duyệt cần thiết; bảo vệ liên hệ chủ nhà khỏi lộ không cần thiết. |
| Đồ ăn gần trọ | Search nhận `listingType`, tọa độ, `radiusKm`, `nearRentalPostId`; repository lọc tin active/còn số lượng. [Controller](../src/Homeji.Api/Controllers/MarketplacePostsController.cs), [service](../src/Homeji.Application/Services/Marketplace/MarketplacePostService.cs), [repository](../src/Homeji.Infrastructure/Repositories/MarketplacePostRepository.cs) | Hiện chỉ lọc hộp tọa độ trong SQL; khoảng cách được tính và sort sau `Skip/Take`. Điểm ở góc hộp có thể ngoài vòng tròn; món gần nhất toàn bộ tập có thể rơi ở trang sau. Cần lọc khoảng cách và sort ổn định **trước** phân trang. |
| Tiệm nhỏ bán món và nhận đơn | `MarketplacePost` có loại Food, số lượng, đơn vị và phút chuẩn bị; checkout kiểm tra món đang bán, cùng người bán, ví và số lượng. [Entity](../src/Homeji.Domain/Entities/MarketplacePost.cs), [checkout](../src/Homeji.Application/Services/MarketplaceOrders/MarketplaceOrderService.cs) | Kiểm tra đồng thời hai người mua món cuối, giảm tồn và trừ tiền cùng giao dịch; không dựa vào kiểm tra tồn trước lưu. Cấu hình stock hiện chưa có concurrency token cho `AvailableQuantity`/`ReservedQuantity`. [Mapping](../src/Homeji.Infrastructure/Context/Configurations/MarketplacePostConfiguration.cs) |
| Một lần checkout nhiều món | `CONTEXT.md` định nghĩa một Marketplace Order chứa nhiều line, nhưng `CreateCartAsync` hiện tạo một entity Order cho mỗi món; repository nhận diện nhóm bằng buyer/seller/`CreatedAt`. [Ngôn ngữ domain](../CONTEXT.md), [service](../src/Homeji.Application/Services/MarketplaceOrders/MarketplaceOrderService.cs), [repository](../src/Homeji.Infrastructure/Repositories/MarketplaceOrderRepository.cs) | Đây là lệch hợp đồng nội bộ, không phải yêu cầu vay mượn từ đối thủ. Tối thiểu cần định danh checkout tường minh và chuyển trạng thái/refund nguyên checkout; migration/header-line đầy đủ ảnh hưởng API cũ và dữ liệu nên phải chọn phương án tương thích. |
| Giao món | DTO có `PickupAt`, `PickupAddress`; chưa thấy fulfillment mode/tài xế/phí ship ở contract đã đọc. [DTO](../src/Homeji.Application/DTOs/MarketplaceOrders/MarketplaceOrderDtos.cs) | Chờ lựa chọn pickup/người bán tự giao/nền tảng vận chuyển. Không diễn giải địa chỉ pickup thành bằng chứng đã có giao tận nơi. |
| Trust | Repo có report, duyệt bài, xác minh chủ trọ, review, appointment, quyền owner và rate limit public search. [Feature map](homeji-backend-feature-map.md), [controllers](../src/Homeji.Api/Controllers) | Kiểm thử quyền theo người dùng và ID tài nguyên: không đọc chat riêng, sửa tin, chuyển trạng thái đơn/rút tiền của người khác. Nội dung đã duyệt không đồng nghĩa đảm bảo thông tin chính xác ngoài đời. |

## Trade-off tìm kiếm và tồn kho

- **SQL tính khoảng cách từ tọa độ hiện có:** thay đổi nhỏ, tránh migration spatial; cần lọc bounding box trước để giảm tập ứng viên rồi tính khoảng cách/sort/paginate trong database. Phải xử lý tọa độ biên, giá trị radius không hợp lệ và tie-break ID. Đây là đề xuất kỹ thuật từ gap mã nguồn, không phải công bố của nền tảng tham khảo.
- **PostGIS geography + spatial index:** thuận lợi nếu quy mô bản đồ lớn hoặc nhiều truy vấn khoảng cách; thêm phụ thuộc extension, migration và kiểm chứng môi trường. Chỉ chọn sau khi đo query plan/quy mô thực tế; không thể khẳng định nhanh hơn nếu chưa benchmark.
- **Optimistic concurrency tồn kho:** phù hợp bổ sung tối thiểu cùng mẫu version của ví hiện có; khi xung đột trả lỗi rõ để người mua tải lại. **Khóa dòng/atomic update có điều kiện:** quản lý contention rõ hơn nhưng cần transaction bao quanh toàn bộ checkout; nếu retry phải tránh trừ tiền/notification hai lần. Cần integration test PostgreSQL, vì test in-memory không chứng minh được hành vi khóa/giao dịch.

## Verification bằng Sonar và k6

### SonarQube / SonarQube Cloud

Nguồn chính thức yêu cầu .NET scanner chạy theo thứ tự `begin` → build/test → `end`; Cloud cần project key, organization và token, token truyền cả hai bước. Scanner điều chỉnh ruleset và tắt `WarningsAsErrors`, vì vậy nên có build chất lượng riêng. [Using the scanner](https://docs.sonarsource.com/sonarqube-cloud/analyzing-source-code/scanners/sonarscanner-for-dotnet/using)

Sonar không tự tạo coverage; cần Coverlet/OpenCover hoặc `dotnet-coverage`. OpenCover dùng `sonar.cs.opencover.reportsPaths`; XML của `dotnet-coverage` dùng `sonar.cs.vscoveragexml.reportsPaths`. Phải xác nhận report được import, không coi test pass là đã có coverage trên dashboard. [Coverage .NET](https://docs.sonarsource.com/sonarqube-cloud/analyzing-source-code/test-coverage/dotnet-test-coverage)

Đề xuất cho Homeji: giữ token ngoài repo/log; chạy scanner theo phiên bản phù hợp server; xuất coverage unit/integration; xem quality gate và từng security hotspot trên browser. Ghi đúng trạng thái “đã chạy scanner”, “đã upload”, “quality gate pass” theo bằng chứng thực tế. Chưa có kết quả Sonar trong báo cáo nghiên cứu này.

### k6 — tốc độ khi có tải

Grafana tài liệu hóa threshold theo `http_req_duration` percentile và `http_req_failed`; threshold thất bại làm test kết thúc thất bại. `check()` cần threshold riêng nếu muốn assertion ảnh hưởng gate. [Thresholds](https://grafana.com/docs/k6/latest/using-k6/thresholds/), [đo lường](https://grafana.com/docs/learning-hub/k6-performance-testing/02-your-first-test/10-what-k6-measures/)

Đề xuất benchmark có kiểm soát:

1. Endpoint rental search/map, food nearby và detail: seed dữ liệu có phân bố vị trí, nhiều ảnh và trang đủ lớn; đo cold/warm riêng.
2. Checkout: dùng user/dữ liệu test và ví test; chạy case cạnh tranh món cuối, hủy/refund lặp lại, tài khoản thiếu tiền. Không phát sinh giao dịch tài chính thật để thử tải.
3. Ghi VU hoặc arrival rate, thời gian chạy, số bản ghi, runtime, cấu hình DB, lỗi, p50/p95/p99 và throughput. Chạy trước/sau tối ưu trên cùng môi trường.
4. Ngưỡng p95/p99 và lỗi cần dựa baseline và kỳ vọng UX/SLO đã chốt; số ví dụ trong tài liệu k6 không tự trở thành yêu cầu Homeji.

Sonar, test API và k6 bổ sung cho nhau. Báo cáo static analysis không chứng minh race checkout hoặc latency thật; benchmark nhanh không chứng minh quyền truy cập đúng. Nên có integration test với PostgreSQL cho transaction/concurrency và test 401/403/404/409/429 cho API liên quan.

## Những quyết định còn mở

1. Địa bàn đã được người dùng chốt: Quận 9 cũ / Thủ Đức. Bán kính tìm kiếm mặc định 5 km, tối đa 50 km theo hợp đồng hiện có.
2. Người dùng đã chốt: khách đến lấy hoặc tiệm tự giao; Homeji quản lý món và đơn hàng. Chính sách xử lý giao thất bại vẫn cần quyết định vận hành.
3. Một tiệm tương ứng một tài khoản hay có nhiều nhân viên; đây là mở rộng mô hình chưa được yêu cầu cụ thể.
4. Chưa điều chỉnh hoa hồng/giá tối thiểu/tạm giữ/rút tiền dựa trên nghiên cứu này; chính sách hiện tại cần giữ cho tới khi có yêu cầu khác.

Tài liệu cũ [food-marketplace-financial-research.md](food-marketplace-financial-research.md) chứa đề xuất tài chính từ 15/07/2026; đề xuất đó không tự động trở thành yêu cầu mới ngày 03/10/2026.
