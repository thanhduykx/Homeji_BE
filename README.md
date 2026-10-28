# Homeji Backend

ASP.NET Core 9 Web API cho Homeji, dùng Clean Architecture, PostgreSQL/Supabase và Supabase JWT authentication.

## Kiến trúc thư mục

```text
src/
├── Homeji.Api/
│   ├── Controllers/        Controller layer
│   ├── Views/              Request/response contracts cho client
│   ├── Mappers/            Map View <-> Application DTO
│   ├── Authentication/     Supabase JWT authentication
│   └── ErrorHandling/      ProblemDetails và exception handling
├── Homeji.Application/
│   ├── Services/           Business/use-case services
│   ├── IServices/          Service contracts
│   ├── DTOs/               Application DTOs
│   ├── Mappers/            Map Domain Entity <-> DTO
│   └── IRepositories/      Repository contracts
├── Homeji.Domain/
│   ├── Entities/           Domain entities và invariants
│   └── Enums/              Domain enums
└── Homeji.Infrastructure/
    ├── Context/            EF Core DbContext và Fluent API
    ├── Repositories/       Repository implementations
    ├── External/           Supabase Auth, MoMo, PayOS clients
    └── Migrations/         EF Core migrations
```

Runtime flow:

```text
View -> Controller -> IService -> Service -> IRepository -> Repository/DAL -> DbContext -> Supabase PostgreSQL
```

Controller không gọi DAL trực tiếp. Service không phụ thuộc HTTP. Repository interface nằm ở Application, implementation nằm ở Infrastructure.

## Cấu hình

Cấu hình hệ thống tuân thủ nguyên tắc bảo mật và quản lý bí mật (Secret Management):

- `src/Homeji.Api/appsettings.json`: Cấu hình ứng dụng chung và các giá trị mặc định an toàn, không chứa thông tin bí mật.
- Môi trường phát triển cục bộ (Development) nạp thêm `appsettings.Local.json` (được Git, Docker và quy trình đóng gói bỏ qua); lưu trữ các thiết lập kết nối thử nghiệm riêng.
- Biến môi trường (Environment Variables) ghi đè cấu hình ở các môi trường triển khai tương ứng (Database, Payment Gateways, Email Service, AI, Supabase Auth). Các khóa truy cập bí mật được thiết lập trực tiếp qua bảng điều khiển máy chủ (Render Environment Secrets), tuyệt đối không lưu trong kho mã nguồn.
- EF Core tooling đọc cấu hình từ môi trường phát triển; kiểm tra kỹ đích cơ sở dữ liệu trước khi thực hiện migration.
- Môi trường Production không sử dụng file cấu hình cục bộ; áp dụng chính sách xoay vòng định kỳ (credential rotation) cho tất cả khóa API từ các nhà cung cấp dịch vụ.
- Cấu hình Reverse Proxy tin cậy (`KnownProxies` / `KnownNetworks`) bảo đảm nhận diện IP chính xác để phục vụ giới hạn tần suất truy cập (rate limiting) và duy trì kết nối HTTPS an toàn.
- Bật HSTS trên môi trường Production; giới hạn dung lượng tải trọng (payload limit) an toàn cho các tác vụ API và tệp tải lên.

Docker chỉ bind port bằng command argument:

```dockerfile
ENTRYPOINT ["dotnet", "Homeji.Api.dll", "--urls", "http://0.0.0.0:8080"]
```

## Yêu cầu

- .NET SDK 9
- Supabase project
- PostgreSQL connection string từ Supabase
- MoMo merchant config nếu dùng MoMo payment
- PayOS client id/api key/checksum key nếu dùng PayOS payment

## Supabase

Backend xác thực access token bằng Supabase JWKS:

```text
https://<project-ref>.supabase.co/auth/v1/.well-known/jwks.json
```

Khuyến nghị Supabase Auth dùng asymmetric JWT signing key. Frontend dùng Supabase publishable/anon key để login, sau đó gửi:

```http
Authorization: Bearer <supabase-access-token>
```

Frontend có thể kiểm tra email khi người dùng nhập form đăng ký bằng
`GET /api/account/email-availability?email=user@example.com`. API trả
`{ "email": "user@example.com", "exists": true, "available": false }`, để FE
báo sớm email đã được sử dụng. Khi frontend gọi `POST /api/account/register`,
backend vẫn tự kiểm tra lại email trong `auth.users` trước khi gọi Supabase
signup. Nếu email đã tồn tại, API trả `409 Conflict` với `detail` và
`errors.email`.

Supabase Auth tạo token/link xác thực chính thức; backend gửi link đó qua dịch vụ gửi email giao dịch bảo mật.
Cấu hình callback URL mặc định được quản lý qua biến môi trường của nền tảng triển khai. Đồng thời trong
Supabase Dashboard:

1. Vào **Authentication → URL Configuration**.
2. Đặt **Site URL** thành URL frontend production, không để `localhost:3000`.
3. Thêm chính xác callback production, ví dụ
   `https://homeji.onrender.com/auth/callback`, vào **Redirect URLs**.
4. Giữ `http://localhost:3000/**` trong Redirect URLs chỉ để phát triển local.

Frontend cũng có thể truyền `redirectTo` trong request đăng ký; URL đó phải nằm
trong allow-list của Supabase. Nếu không truyền, backend dùng cấu hình callback mặc định.

## Dịch vụ gửi email xác thực & giao dịch

`POST /api/account/register` tích hợp tạo tài khoản và URL xác nhận bảo mật, sau đó chuyển phát link xác thực hoặc mã OTP tới email người dùng thông qua dịch vụ gửi email giao dịch (Transactional Email Provider). Người dùng xác nhận email trước khi đăng nhập chính thức.

Cấu hình dịch vụ email được quản lý an toàn qua biến môi trường máy chủ (Server Secrets):

- Trạng thái kích hoạt và nhà cung cấp dịch vụ gửi thư
- Thông số kết nối mạng và bảo mật truyền tải (TLS/SSL)
- Thông tin định danh người gửi và tên hiển thị thương hiệu Homeji
- Khóa phân quyền quản trị phía máy chủ (lưu trữ độc quyền tại backend, tuyệt đối không gửi xuống frontend)

Trong Supabase Dashboard, tính năng **Confirm email** luôn được kích hoạt. Nếu dịch vụ gửi thư gặp sự cố gián đoạn tạm thời, hệ thống tự động xử lý an toàn để bảo vệ tính nhất quán tài khoản và hỗ trợ người dùng đăng ký lại dễ dàng.

## Database migration

```powershell
dotnet tool restore
dotnet ef database update `
  --project src/Homeji.Infrastructure `
  --startup-project src/Homeji.Api `
  --context ApplicationDbContext
```

Các migration hiện có:

- `InitialCreate`
- `AddCoreHomejiModules`
- `AddAccountAndPayments`

Application tables nằm trong schema `homeji`, không đặt trực tiếp trong `public`.

## Chạy local

```powershell
dotnet restore
dotnet build --no-restore
dotnet run --project src/Homeji.Api
```

Swagger:

```text
https://homeji-be.onrender.com/swagger
```

Local Swagger sẽ nằm tại:

```text
http://localhost:<port>/swagger
```

## API chính

Account/Auth:

- `GET /api/account/email-availability`
- `POST /api/account/register`
- `POST /api/account/login`
- `POST /api/account/forgot-password`
- `POST /api/account/reset-password`
- `GET /api/account/google/url`
- `GET /api/account/google/redirect`

Payment:

- `POST /api/payments/momo/create`
- `POST /api/payments/momo/ipn`
- `POST /api/payments/payos/create`
- `POST /api/payments/payos/webhook`
- `GET /api/payments/{paymentId}`
- `GET /api/payments/orders/{orderCode}`

Core modules:

- `GET /api/profile/me`
- `PUT /api/profile/me`
- `GET /api/rental-posts`
- `POST /api/rental-posts/drafts`
- `POST /api/reports`
- `GET /api/notifications`

## Kiểm thử

```powershell
dotnet test --no-restore
```

Current test suites:

- `Homeji.Application.UnitTests`
- `Homeji.Api.IntegrationTests`

## Deploy Render

Render Blueprint hiện tại:

```text
homeji-be
```

Tạo service lần đầu:

```powershell
1. Vào https://dashboard.render.com/.
2. Chọn **New +** → **Blueprint**, rồi kết nối `thanhduykx/Homeji_BE` ở branch `main`.
3. Render đọc `render.yaml`, tạo Docker service và tự deploy mỗi lần push `main`.
```

Trước khi deploy, thiết lập khóa API Gemini trong bảng điều khiển **Environment Secrets** của Render.

Khóa API Gemini được quản lý dưới dạng secret môi trường máy chủ bảo mật và tuyệt đối không lưu trong mã nguồn hoặc file cấu hình công khai. Khi khởi chạy, dịch vụ ASP.NET Core sẽ tự động nạp cấu hình từ môi trường an toàn.

## Quy ước phát triển

- Không expose EF entities trực tiếp qua API.
- Mọi I/O async nhận `CancellationToken`.
- Business invariants nằm trong Domain.
- Orchestration nằm trong Application Services.
- DAL chỉ đi qua repository.
- Migration startup do `Database:ApplyMigrationsOnStartup` điều khiển; Render hiện bật qua environment variable. Production nên chạy migration như bước deploy riêng bằng tài khoản DDL, API dùng quyền tối thiểu.
- Build bật nullable reference types, analyzers và warnings-as-errors.

## Food quanh trọ và kiểm chứng chất lượng (03/10/2026)

Phạm vi sản phẩm: Quận 9 cũ / Thủ Đức; tiệm tự giao hoặc khách đến lấy. Chi tiết hợp đồng mới và kết quả kiểm chứng nằm trong [backend-verification-2026-10-03.md](docs/backend-verification-2026-10-03.md).

Danh mục tin nguồn và ảnh dùng `GET /api/rental-source-listings`, có link nguồn và thời điểm thu thập; các tin giả lập cũ được phân biệt qua `isSynthetic`. Hướng dẫn thu thập và kiểm tra dữ liệu nằm trong tài liệu trên.

```powershell
# Sonar analyzer cục bộ, build nghiêm ngặt, tests, coverage và dependency audit
./scripts/quality/Test-Quality.ps1

# PostgreSQL riêng, toàn bộ migration/tests và k6; tự dừng API/DB sau chạy
./scripts/quality/Test-LocalBackend.ps1

# Scanner server/Cloud: cần SONAR_TOKEN, SONAR_PROJECT_KEY, SONAR_HOST_URL,
# và SONAR_ORGANIZATION với Cloud đã tạo project
./scripts/quality/Test-Quality.ps1 -ServerAnalysis
```

`Test-LocalBackend.ps1` chỉ dùng database `homeji_quality` trên loopback, không chạy tải vào production. Cần PostgreSQL 18 tại đường dẫn mặc định hoặc truyền `-PostgresBin`, cùng k6 trong PATH.
