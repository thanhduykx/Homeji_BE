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

Cấu hình không chứa secret trong Git:

- `src/Homeji.Api/appsettings.json`: cấu hình chung và giá trị mặc định không bí mật.
- Development đọc thêm `appsettings.Local.json` (đã được Git/Docker/publish bỏ qua); bản local trên máy hiện tại đã giữ lại các giá trị cấu hình cũ.
- Environment variables ghi đè cấu hình ở mọi môi trường; ví dụ `ConnectionStrings__DefaultConnection`, `Email__Smtp__Password`, `Payments__MoMo__AccessKey`, `Payments__MoMo__SecretKey`, `Payments__PayOS__ApiKey`, `Payments__PayOS__ChecksumKey`, `Supabase__ServiceRoleKey`.
- EF tooling đọc cấu hình chung, local và environment variables. Không chạy `database update` nếu chưa kiểm tra database đích.
- Production không nạp file local. Các khóa từng được commit cần được rotate qua nhà cung cấp; thay đổi working tree không xóa lịch sử Git.
- Reverse proxy phải cấu hình IP/CIDR tin cậy bằng `ReverseProxy:KnownProxies` hoặc `ReverseProxy:KnownNetworks`. Mặc định chỉ tin loopback; không dùng mạng `/0`. Cần cấu hình đúng proxy của Render trước khi deploy, để giữ HTTPS và rate limiting theo IP chính xác.
- Production bật HSTS, không trả exception nội bộ; JSON body mặc định tối đa 128 KiB, upload ảnh có giới hạn riêng.

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

Supabase Auth tạo token/link xác thực chính thức; backend gửi link đó qua Gmail SMTP.
Cấu hình callback mặc định bằng `Supabase:RegistrationRedirectUrl` (production nên
đặt qua biến môi trường `Supabase__RegistrationRedirectUrl`). Đồng thời trong
Supabase Dashboard:

1. Vào **Authentication → URL Configuration**.
2. Đặt **Site URL** thành URL frontend production, không để `localhost:3000`.
3. Thêm chính xác callback production, ví dụ
   `https://exe101-homeji.onrender.com/auth/callback`, vào **Redirect URLs**.
4. Giữ `http://localhost:3000/**` trong Redirect URLs chỉ để phát triển local.

Frontend cũng có thể truyền `redirectTo` trong request đăng ký; URL đó phải nằm
trong allow-list của Supabase. Nếu không truyền, backend dùng
`Supabase:RegistrationRedirectUrl`.

## SMTP xác thực đăng ký

`POST /api/account/register` dùng Supabase Admin `generate_link` để tạo tài khoản chưa
xác thực và URL xác nhận, sau đó `SmtpAccountEmailSender` gửi URL qua SMTP. Người dùng
click link để Supabase xác nhận email rồi mới đăng nhập. Supabase không gửi email trong
luồng này nên không dùng hạn mức email mặc định của Supabase.

Cấu hình nằm tại `Email:Smtp` trong `appsettings.json`. Trên Render có thể ghi đè bằng:

- `Email__Smtp__Enabled`
- `Email__Smtp__Host`, `Email__Smtp__Port`, `Email__Smtp__EnableSsl`
- `Email__Smtp__Username`, `Email__Smtp__Password`
- `Email__Smtp__FromEmail`, `Email__Smtp__FromName`
- `Supabase__ServiceRoleKey` — bắt buộc để backend gọi Admin `generate_link`

`ServiceRoleKey` là khóa quản trị và tuyệt đối không được đưa xuống frontend. Trong
Supabase Dashboard vẫn giữ **Confirm email** bật. Nếu SMTP gửi thất bại, backend cố gắng
xóa tài khoản chưa xác thực vừa tạo và trả `503` để người dùng có thể đăng ký lại.

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

Trước khi deploy, đặt secret Gemini trong phần **Environment** của service:

```text
Ai__Gemini__ApiKey = <Gemini API key>
```

API key Gemini không được lưu trong `appsettings.json`. Trong môi trường
Production, ASP.NET Core tự động ánh xạ `Ai__Gemini__ApiKey` thành
`Ai:Gemini:ApiKey` và sử dụng secret do Render cung cấp.

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
