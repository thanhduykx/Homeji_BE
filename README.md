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

Project không dùng file cấu hình local riêng, `.env`, .NET User Secrets hoặc environment variables cho application config.

- File cấu hình duy nhất: `src/Homeji.Api/appsettings.json`
- Runtime, EF tooling và Docker image đều đọc file này.
- Trade-off: clone repo chạy đơn giản hơn, nhưng nếu repo public hoặc chia sẻ rộng thì cần rotate/giới hạn quyền các key trong `appsettings.json`.

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

## SMTP đăng ký account

API gửi email xác nhận đăng ký sau khi Supabase tạo account thành công. Cấu hình tại:

```json
"Email": {
  "Smtp": {
    "Enabled": true,
    "Host": "smtp.gmail.com",
    "Port": 587,
    "EnableSsl": true,
    "Username": "your-smtp-user",
    "Password": "your-smtp-password-or-app-password",
    "FromEmail": "no-reply@homeji.vn",
    "FromName": "Homeji",
    "LoginUrl": "http://localhost:3000/login"
  }
}
```

Email SMTP này là email thông báo/xác nhận đăng ký từ Homeji. Nếu cần email verification token chính thức của Supabase Auth, cấu hình Supabase Auth custom SMTP trong Supabase Dashboard.

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
https://homeji-api-thanhduy.onrender.com/swagger
```

Local Swagger sẽ nằm tại:

```text
http://localhost:<port>/swagger
```

## API chính

Account/Auth:

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
homeji-api-thanhduy
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

## Quy ước phát triển

- Không expose EF entities trực tiếp qua API.
- Mọi I/O async nhận `CancellationToken`.
- Business invariants nằm trong Domain.
- Orchestration nằm trong Application Services.
- DAL chỉ đi qua repository.
- Không tự động chạy migration khi API start.
- Build bật nullable reference types, analyzers và warnings-as-errors.
