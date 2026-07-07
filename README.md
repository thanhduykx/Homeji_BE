# Homeji Backend

ASP.NET Core 9 Web API theo Clean Architecture, sá»­ dá»¥ng PostgreSQL cá»§a Supabase vÃ  xÃ¡c thá»±c access token do Supabase Auth phÃ¡t hÃ nh.

## Kiáº¿n trÃºc thÆ° má»¥c

```text
src/
â”œâ”€â”€ Homeji.Api/
â”‚   â”œâ”€â”€ Controllers/        Controller layer
â”‚   â”œâ”€â”€ Views/              View/API contracts nháº­n vÃ  tráº£ vá» cho client
â”‚   â”œâ”€â”€ Mappers/            Map View <-> Application DTO
â”‚   â”œâ”€â”€ Authentication/     JWT/Supabase authentication
â”‚   â””â”€â”€ ErrorHandling/      ProblemDetails vÃ  exception handling
â”œâ”€â”€ Homeji.Application/
â”‚   â”œâ”€â”€ Services/           Business/use-case services
â”‚   â”œâ”€â”€ IServices/          Service contracts
â”‚   â”œâ”€â”€ DTOs/               Internal application DTOs
â”‚   â”œâ”€â”€ Mappers/            Map Domain Entity <-> DTO
â”‚   â”œâ”€â”€ IRepositories/      Repository contracts
â”‚   â””â”€â”€ Common/             Shared exceptions/common application types
â”œâ”€â”€ Homeji.Domain/
â”‚   â”œâ”€â”€ Entities/           Domain entities vÃ  business invariants
â”‚   â””â”€â”€ Exceptions/         Domain exceptions
â””â”€â”€ Homeji.Infrastructure/
    â”œâ”€â”€ Context/            DAL: EF Core DbContext vÃ  Fluent API configuration
    â”œâ”€â”€ Repositories/       DAL: repository implementations
    â”œâ”€â”€ Migrations/         EF Core migrations
    â””â”€â”€ Health/             Infrastructure health checks
tests/
â”œâ”€â”€ Homeji.Application.UnitTests/
â””â”€â”€ Homeji.Api.IntegrationTests/
```

Dependency chá»‰ Ä‘i vÃ o phÃ­a domain:

```text
Api â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€> Application â”€â”€> Domain
 â””â”€â”€> Infrastructure â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€> Domain
              â””â”€â”€â”€â”€â”€â”€> Application
```

`IRepository` náº±m trong Application Ä‘á»ƒ service khÃ´ng phá»¥ thuá»™c Infrastructure. Infrastructure chá»‰ implement repository vÃ  context. ÄÃ¢y lÃ  Ä‘iá»ƒm quan trá»ng Ä‘á»ƒ giá»¯ Clean Architecture vÃ  testability.

Runtime flow:

```text
View/API contract -> Controller -> IService -> Service -> IRepository -> Repository/DAL -> DbContext -> Supabase PostgreSQL
```

Controller chá»‰ nháº­n/tráº£ `Views`, sau Ä‘Ã³ mapper chuyá»ƒn sang DTO cho service. Service khÃ´ng biáº¿t HTTP, khÃ´ng biáº¿t controller. DAL khÃ´ng Ä‘Æ°á»£c gá»i trá»±c tiáº¿p tá»« controller.

## Quy Æ°á»›c cáº¥u hÃ¬nh

Project khÃ´ng dÃ¹ng `.env`, environment variables hoáº·c .NET User Secrets cho application config.

- `src/Homeji.Api/appsettings.json`: cáº¥u hÃ¬nh máº·c Ä‘á»‹nh an toÃ n, cÃ³ thá»ƒ commit.
- `src/Homeji.Api/appsettings.Local.json`: cáº¥u hÃ¬nh local/server tháº­t, khÃ´ng commit.
- `src/Homeji.Api/appsettings.Local.example.json`: file máº«u Ä‘á»ƒ copy.

Trade-off: cÃ¡ch nÃ y Ä‘Æ¡n giáº£n cho repo hiá»‡n táº¡i, nhÆ°ng file `appsettings.Local.json` sáº½ chá»©a connection string/password tháº­t. Khi deploy, pháº£i báº£o vá»‡ file nÃ y báº±ng quyá»n truy cáº­p cá»§a server/CI vÃ  khÃ´ng Ä‘Æ°a vÃ o Git.

## YÃªu cáº§u

- .NET SDK 9.0.315 hoáº·c patch má»›i hÆ¡n cá»§a dÃ²ng 9.0
- Má»™t Supabase project
- Supabase Auth dÃ¹ng asymmetric JWT signing key (`ES256` Ä‘Æ°á»£c khuyáº¿n nghá»‹)

API xÃ¡c thá»±c chá»¯ kÃ½ báº±ng JWKS táº¡i `https://<project-ref>.supabase.co/auth/v1/.well-known/jwks.json`. Legacy shared JWT secret khÃ´ng Ä‘Æ°á»£c há»— trá»£ cÃ³ chá»§ Ä‘Ã­ch.

## Cáº¥u hÃ¬nh Supabase

### 1. JWT signing key

Trong Supabase Dashboard, má»Ÿ **Project Settings â†’ JWT Keys** rá»“i migrate/rotate sang asymmetric signing key. Frontend dÃ¹ng publishable key Ä‘á»ƒ Ä‘Äƒng nháº­p; khÃ´ng Ä‘Æ°a secret key hoáº·c database password vÃ o frontend.

### 2. Database connection

Láº¥y connection string tá»« **Connect** trong Supabase Dashboard.

- Backend cháº¡y lÃ¢u dÃ i vÃ  cÃ³ IPv6: dÃ¹ng Direct connection.
- Backend chá»‰ cÃ³ IPv4: dÃ¹ng Supavisor Session mode, cá»•ng `5432`.
- KhÃ´ng dÃ¹ng Transaction mode cho EF Core migrations.

Äá»‹nh dáº¡ng ADO.NET/Npgsql:

```text
Host=<host>;Port=5432;Database=postgres;Username=<username>;Password=<password>;SSL Mode=Require;Trust Server Certificate=false
```

Táº¡o file cáº¥u hÃ¬nh local:

```powershell
Copy-Item src/Homeji.Api/appsettings.Local.example.json src/Homeji.Api/appsettings.Local.json
```

Sau Ä‘Ã³ sá»­a `src/Homeji.Api/appsettings.Local.json`:

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

KhÃ´ng commit `appsettings.Local.json`, connection string, database password hoáº·c Supabase secret key.

### 3. Apply migration

```powershell
dotnet tool restore
dotnet ef database update `
  --project src/Homeji.Infrastructure `
  --startup-project src/Homeji.Api
```

Migration táº¡o schema riÃªng `homeji`, khÃ´ng Ä‘áº·t application table trong schema `public`. Báº£ng `homeji.user_profiles`:

- dÃ¹ng cÃ¹ng UUID vá»›i `auth.users.id`;
- xÃ³a cascade khi Supabase Auth user bá»‹ xÃ³a;
- báº­t RLS phÃ²ng thá»§;
- thu há»“i quyá»n truy cáº­p trá»±c tiáº¿p cá»§a `PUBLIC`, `anon` vÃ  `authenticated`.

VÃ¬ váº­y dá»¯ liá»‡u profile chá»‰ Ä‘i qua Homeji API. Náº¿u sau nÃ y cáº§n truy cáº­p tá»« Supabase Data API, pháº£i thiáº¿t káº¿ policy RLS theo ownership trÆ°á»›c khi cáº¥p quyá»n.

## Cháº¡y á»©ng dá»¥ng

```powershell
dotnet restore
dotnet build --no-restore
dotnet run --project src/Homeji.Api
```

OpenAPI JSON Ä‘Æ°á»£c báº­t/táº¯t báº±ng `Api:EnableOpenApi`. File máº«u local Ä‘ang báº­t OpenAPI táº¡i `/swagger`.

## Authentication flow

1. Frontend Ä‘Äƒng nháº­p báº±ng Supabase Auth.
2. Supabase tráº£ access token.
3. Frontend gá»­i `Authorization: Bearer <access-token>` tá»›i Homeji API.
4. API kiá»ƒm tra signature, issuer, audience, expiry vÃ  láº¥y user id tá»« claim `sub`.

CÃ¡c controller Ä‘Æ°á»£c báº£o vá»‡ máº·c Ä‘á»‹nh báº±ng fallback authorization policy. Chá»‰ health checks vÃ  OpenAPI khi Ä‘Æ°á»£c báº­t báº±ng cáº¥u hÃ¬nh lÃ  anonymous.

## API máº«u

| Method | Endpoint | MÃ´ táº£ |
|---|---|---|
| `GET` | `/health/live` | Process liveness |
| `GET` | `/health/ready` | Database readiness |
| `GET` | `/api/profile/me` | Láº¥y profile hiá»‡n táº¡i |
| `PUT` | `/api/profile/me` | Táº¡o hoáº·c cáº­p nháº­t profile hiá»‡n táº¡i |

VÃ­ dá»¥ request:

```http
PUT /api/profile/me
Authorization: Bearer <supabase-access-token>
Content-Type: application/json

{
  "displayName": "Homeji User"
}
```

## Kiá»ƒm thá»­

```powershell
dotnet test --no-restore
```

- Unit tests kiá»ƒm tra validation vÃ  profile use case.
- Integration tests kiá»ƒm tra health endpoint vÃ  authorization boundary.
- Kiá»ƒm thá»­ database tháº­t cáº§n Supabase connection riÃªng cho test; khÃ´ng dÃ¹ng production database.

## Quy Æ°á»›c phÃ¡t triá»ƒn

- KhÃ´ng expose EF entities trá»±c tiáº¿p qua API.
- Má»i I/O báº¥t Ä‘á»“ng bá»™ nháº­n `CancellationToken`.
- Business invariants náº±m trong Domain; orchestration náº±m trong Application.
- KhÃ´ng tá»± Ä‘á»™ng cháº¡y migration khi API khá»Ÿi Ä‘á»™ng.
- Package versions Ä‘Æ°á»£c quáº£n lÃ½ táº¡i `Directory.Packages.props` vÃ  khÃ³a trong `packages.lock.json`.
- Build dÃ¹ng nullable reference types, analyzers vÃ  warnings-as-errors.

