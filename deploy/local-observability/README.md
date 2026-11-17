# Thử Grafana và Kafka với Homeji

Chạy lệnh từ thư mục gốc backend. Grafana và cổng client Kafka chỉ nhận kết nối từ máy local.

## Grafana

```powershell
docker compose -f deploy/local-observability/compose.yaml up -d

$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:Telemetry__Enabled = 'true'
$env:OTEL_EXPORTER_OTLP_PROTOCOL = 'http/protobuf'
$env:OTEL_EXPORTER_OTLP_ENDPOINT = 'http://127.0.0.1:14318'
$env:OTEL_METRIC_EXPORT_INTERVAL = '5000'
```

Dùng cấu hình phát triển của Homeji với PostgreSQL cục bộ. Để thử riêng dashboard với các endpoint health, đặt các giá trị thử nghiệm dưới đây trong cùng cửa sổ PowerShell trước khi chạy API. Cấu hình này dùng kết nối DB giả và tắt worker.

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Staging'
$env:BackgroundJobs__Enabled = 'false'
$env:Database__ApplyMigrationsOnStartup = 'false'
$env:ConnectionStrings__DefaultConnection = 'Host=127.0.0.1;Port=15491;Database=homeji_telemetry_demo;Username=homeji;Timeout=2'
$env:Supabase__ProjectUrl = 'https://local-demo.invalid'
$env:Supabase__Audience = 'authenticated'
$env:Email__Provider = 'Smtp'
```

```powershell
dotnet run --project src/Homeji.Api --no-launch-profile -- --urls http://127.0.0.1:15087
```

Mở [Homeji Backend](http://127.0.0.1:13001/d/homeji-backend-local) và gọi `http://127.0.0.1:15087/health/live` vài lần, cách nhau một vài giây. Các biểu đồ dùng số đo trong cửa sổ hai phút. Với cấu hình thử nghiệm ở trên, `/health/ready` dự kiến trả HTTP 503 vì thiếu PostgreSQL. Dùng tín hiệu này để kiểm tra biểu đồ 5xx. Panel AI Search cần lưu lượng AI Search đã xác thực.

## Kafka

```powershell
docker compose -f deploy/local-observability/compose.yaml --profile kafka up -d
& ./deploy/local-observability/Test-Kafka.ps1
```

Broker cần vài giây sau khi container khởi động. Script xác minh hai nhóm đọc cùng sự kiện mẫu. Bài kiểm thử tích hợp bên dưới gọi endpoint duyệt tin, thử broker treo, rồi đọc sự kiện do API tạo qua outbox.

```powershell
# PostgreSQL dùng riêng cho kiểm thử, có thể dừng ngay sau khi chạy xong.
docker run -d --name homeji-integration-postgres -e POSTGRES_USER=homeji -e POSTGRES_HOST_AUTH_METHOD=trust -e POSTGRES_DB=homeji_quality -p 127.0.0.1:15491:5432 postgres:17-alpine
docker exec homeji-integration-postgres pg_isready -U homeji -d homeji_quality
docker exec homeji-integration-postgres psql -U homeji -d postgres -v ON_ERROR_STOP=1 -c 'CREATE ROLE anon; CREATE ROLE authenticated; CREATE ROLE service_role;'

$env:HOMEJI_TEST_DATABASE = 'Host=127.0.0.1;Port=15491;Database=homeji_quality;Username=homeji'
$env:HOMEJI_TEST_KAFKA = '127.0.0.1:19092'
dotnet test tests/Homeji.Api.IntegrationTests --filter FullyQualifiedName~RentalPublicationKafkaTests
```

Bài kiểm thử tự tạo database và topic cho mỗi lần chạy. Để xuất sự kiện khi chạy API trực tiếp, áp dụng migration, tạo topic `homeji.rental-publications.v1`, bật `BackgroundJobs__Enabled` và đặt các biến sau.

```powershell
$env:Kafka__RentalPublications__Enabled = 'true'
$env:Kafka__RentalPublications__AnalyticsEnabled = 'true'
$env:Kafka__RentalPublications__BootstrapServers = '127.0.0.1:19092'
$env:Kafka__RentalPublications__Topic = 'homeji.rental-publications.v1'
```

Mỗi lần duyệt hợp lệ ghi một sự kiện có `eventId` ổn định. Worker đánh dấu outbox sau khi broker xác nhận đã lưu. Consumer thống kê ghi theo `eventId` duy nhất rồi mới commit offset, nên bản phát lại vẫn chỉ tính một lượt duyệt. Sự kiện sai định dạng chỉ ghi log partition và offset, rồi consumer tiếp tục. Có thể xem bản tin gốc trong Kafka trong thời gian lưu của topic. Dùng consumer group riêng khi phát lại để kiểm tra.

`Enabled` điều khiển việc ghi outbox và gửi Kafka. `AnalyticsEnabled` điều khiển nhóm consumer `homeji.rental-publications.analytics.v1`. Có thể dừng consumer trong lúc publisher tiếp tục gửi. Bật `BackgroundJobs__Enabled` trên tiến trình chạy worker. Với broker quản lý, đặt SecurityProtocol và thông tin xác thực từ secret của môi trường.

Để kiểm kê các sự kiện chờ gửi, chạy với role vận hành được phép đọc outbox. Kết quả gồm tối đa 20 bản ghi, ưu tiên sự kiện có thể được lấy sớm nhất theo chỉ mục của worker.

```sql
SELECT id, rental_post_id, occurred_at, available_at, attempts,
    CASE
        WHEN lease_id IS NOT NULL AND available_at > now() THEN 'processing'
        WHEN lease_id IS NOT NULL THEN 'lease_expired'
        WHEN available_at > now() THEN 'retry_scheduled'
        ELSE 'ready'
    END AS dispatch_status
FROM homeji.rental_publications
WHERE published_at IS NULL
ORDER BY available_at, id
LIMIT 20;
```

Đếm lượt duyệt được consumer ghi nhận trong bảy ngày lịch gần nhất theo giờ Việt Nam. Số liệu bắt đầu từ khi bật luồng Kafka và chỉ gồm tin đi qua luồng duyệt có phát sự kiện.

```sql
SELECT (occurred_at AT TIME ZONE 'Asia/Ho_Chi_Minh')::date AS publication_date,
       count(*) AS approvals
FROM homeji.rental_publication_facts
WHERE occurred_at >= (((now() AT TIME ZONE 'Asia/Ho_Chi_Minh')::date - 6)::timestamp AT TIME ZONE 'Asia/Ho_Chi_Minh')
  AND occurred_at <= now()
GROUP BY 1
ORDER BY 1;
```

```powershell
docker compose -f deploy/local-observability/compose.yaml --profile kafka down
```

Xem [cách chạy API trên Kubernetes local](../kubernetes/README.md).
