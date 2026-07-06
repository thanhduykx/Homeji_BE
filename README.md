# Homeji Backend

ASP.NET Core 9 Web API theo Clean Architecture, sử dụng PostgreSQL của Supabase và xác thực access token do Supabase Auth phát hành.

## Kiến trúc

```text
src/
├── Homeji.Api/             HTTP boundary, JWT, middleware, controllers
├── Homeji.Application/     Use cases, validation, contracts
├── Homeji.Domain/          Entities và business invariants
└── Homeji.Infrastructure/  EF Core, PostgreSQL, repository, migrations
tests/
├── Homeji.Application.UnitTests/
└── Homeji.Api.IntegrationTests/
```

Dependency chỉ đi vào phía domain:

```text
Api ───────────────> Application ──> Domain
 └──> Infrastructure ──────────────> Domain
              └──────> Application
```

`Homeji.Domain` không phụ thuộc ASP.NET Core, EF Core hay Supabase. Supabase Auth là nguồn danh tính duy nhất; API không lưu hoặc xử lý mật khẩu.

## Quy ước cấu hình

Project không dùng `.env`, environment variables hoặc .NET User Secrets cho application config.

- `src/Homeji.Api/appsettings.json`: cấu hình mặc định an toàn, có thể commit.
- `src/Homeji.Api/appsettings.Local.json`: cấu hình local/server thật, không commit.
- `src/Homeji.Api/appsettings.Local.example.json`: file mẫu để copy.

Trade-off: cách này đơn giản cho repo hiện tại, nhưng file `appsettings.Local.json` sẽ chứa connection string/password thật. Khi deploy, phải bảo vệ file này bằng quyền truy cập của server/CI và không đưa vào Git.

## Yêu cầu

- .NET SDK 9.0.315 hoặc patch mới hơn của dòng 9.0
- Một Supabase project
- Supabase Auth dùng asymmetric JWT signing key (`ES256` được khuyến nghị)

API xác thực chữ ký bằng JWKS tại `https://<project-ref>.supabase.co/auth/v1/.well-known/jwks.json`. Legacy shared JWT secret không được hỗ trợ có chủ đích.

## Cấu hình Supabase

### 1. JWT signing key

Trong Supabase Dashboard, mở **Project Settings → JWT Keys** rồi migrate/rotate sang asymmetric signing key. Frontend dùng publishable key để đăng nhập; không đưa secret key hoặc database password vào frontend.

### 2. Database connection

Lấy connection string từ **Connect** trong Supabase Dashboard.

- Backend chạy lâu dài và có IPv6: dùng Direct connection.
- Backend chỉ có IPv4: dùng Supavisor Session mode, cổng `5432`.
- Không dùng Transaction mode cho EF Core migrations.

Định dạng ADO.NET/Npgsql:

```text
Host=<host>;Port=5432;Database=postgres;Username=<username>;Password=<password>;SSL Mode=Require;Trust Server Certificate=false
```

Tạo file cấu hình local:

```powershell
Copy-Item src/Homeji.Api/appsettings.Local.example.json src/Homeji.Api/appsettings.Local.json
```

Sau đó sửa `src/Homeji.Api/appsettings.Local.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=<host>;Port=5432;Database=postgres;Username=<username>;Password=<password>;SSL Mode=Require;Trust Server Certificate=false"
  },
  "Supabase": {
    "ProjectUrl": "https://<project-ref>.supabase.co",
    "Audience": "authenticated"
  },
  "Cors": {
    "AllowedOrigins": [
      "http://localhost:3000"
    ]
  },
  "Api": {
    "EnableOpenApi": true,
    "UseHsts": false
  }
}
```

Không commit `appsettings.Local.json`, connection string, database password hoặc Supabase secret key.

### 3. Apply migration

```powershell
dotnet tool restore
dotnet ef database update `
  --project src/Homeji.Infrastructure `
  --startup-project src/Homeji.Api
```

Migration tạo schema riêng `homeji`, không đặt application table trong schema `public`. Bảng `homeji.user_profiles`:

- dùng cùng UUID với `auth.users.id`;
- xóa cascade khi Supabase Auth user bị xóa;
- bật RLS phòng thủ;
- thu hồi quyền truy cập trực tiếp của `PUBLIC`, `anon` và `authenticated`.

Vì vậy dữ liệu profile chỉ đi qua Homeji API. Nếu sau này cần truy cập từ Supabase Data API, phải thiết kế policy RLS theo ownership trước khi cấp quyền.

## Chạy ứng dụng

```powershell
dotnet restore
dotnet build --no-restore
dotnet run --project src/Homeji.Api
```

OpenAPI JSON được bật/tắt bằng `Api:EnableOpenApi`. File mẫu local đang bật OpenAPI tại `/openapi/v1.json`.

## Authentication flow

1. Frontend đăng nhập bằng Supabase Auth.
2. Supabase trả access token.
3. Frontend gửi `Authorization: Bearer <access-token>` tới Homeji API.
4. API kiểm tra signature, issuer, audience, expiry và lấy user id từ claim `sub`.

Các controller được bảo vệ mặc định bằng fallback authorization policy. Chỉ health checks và OpenAPI khi được bật bằng cấu hình là anonymous.

## API mẫu

| Method | Endpoint | Mô tả |
|---|---|---|
| `GET` | `/health/live` | Process liveness |
| `GET` | `/health/ready` | Database readiness |
| `GET` | `/api/v1/profile/me` | Lấy profile hiện tại |
| `PUT` | `/api/v1/profile/me` | Tạo hoặc cập nhật profile hiện tại |

Ví dụ request:

```http
PUT /api/v1/profile/me
Authorization: Bearer <supabase-access-token>
Content-Type: application/json

{
  "displayName": "Homeji User"
}
```

## Kiểm thử

```powershell
dotnet test --no-restore
```

- Unit tests kiểm tra validation và profile use case.
- Integration tests kiểm tra health endpoint và authorization boundary.
- Kiểm thử database thật cần Supabase connection riêng cho test; không dùng production database.

## Quy ước phát triển

- Không expose EF entities trực tiếp qua API.
- Mọi I/O bất đồng bộ nhận `CancellationToken`.
- Business invariants nằm trong Domain; orchestration nằm trong Application.
- Không tự động chạy migration khi API khởi động.
- Package versions được quản lý tại `Directory.Packages.props` và khóa trong `packages.lock.json`.
- Build dùng nullable reference types, analyzers và warnings-as-errors.
