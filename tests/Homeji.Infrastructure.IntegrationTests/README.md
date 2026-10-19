# Local PostgreSQL persistence checks

Run this project explicitly; it is separate from the fast application-unit-test suite.
It uses the real `ApplicationDbContext`, Npgsql repositories, rental service, validators
and domain rules. `RentalPostingHttpTests` also exercises the actual API controllers,
JSON transport, authorization boundaries, validation and PostgreSQL persistence through
an in-process TestServer. Its test-only identity scheme does **not** prove Supabase JWT
verification. No storage file is uploaded and no real browser is driven by this project.

## Safety

- Only `localhost` / `127.0.0.1` and database `homeji_ui_qa` are accepted.
- Persistence tests do not load application configuration. The HTTP factory uses the
  API's default configuration but explicitly replaces its connection with the guarded
  loopback database, disables startup migrations/background jobs, and uses a dummy
  Supabase project. It never uses production credentials or calls payment/auth services.
- Use a dedicated non-superuser role with ownership of this disposable QA schema, not
  a production/application role. The role needs schema creation for `EnsureCreated`.
- Each test rolls back its inserted rows. No `DROP DATABASE`, cleanup SQL or production
  migrations are run. `EnsureCreated` verifies the current EF model, not migration history.
- The fixture installs the immutable search normalization function and pg_trgm prerequisite
  from AddNormalizedRentalSearch before EnsureCreated; generated search columns require them.
  The guarded QA database role needs permission to install trusted pg_trgm in this disposable DB.
- Missing configuration fails explicitly; passing tests are never silently skipped.

## Optional persistence verification without Docker

Docker is not required for the application, deployment, or independent test suites.
If a dedicated local PostgreSQL database is already available, set `HOMEJI_TEST_DB`
to its connection string. The loopback host and `homeji_ui_qa` database restrictions
above still apply. Do not point these tests at production to bypass the safety guard.
The current workflow does not install or start a database server.

The independently runnable checks are:

```powershell
dotnet test tests/Homeji.Application.UnitTests --no-restore
dotnet test tests/Homeji.Api.IntegrationTests --no-restore
```

Database-dependent API cases are reported as skipped when their configuration is
absent; this is not proof of persistence. On 2026-10-04 these commands returned
133 passed, and 71 passed / 8 skipped respectively.

## Optional Docker reproduction (PowerShell)

The password below is public, test-only and must never be used outside this loopback container.
Check that the container name and host port are unused before creating it.

```powershell
docker run --name homeji-ui-qa-20261004 -d -p 127.0.0.1:55439:5432 -e POSTGRES_PASSWORD=local-ui-qa-only postgres:17-alpine
docker exec homeji-ui-qa-20261004 pg_isready -U postgres
docker exec homeji-ui-qa-20261004 psql -U postgres -c "CREATE ROLE homeji_qa LOGIN PASSWORD 'local-ui-qa-only' NOSUPERUSER NOCREATEDB NOCREATEROLE;"
docker exec homeji-ui-qa-20261004 psql -U postgres -c "CREATE DATABASE homeji_ui_qa OWNER homeji_qa;"
$env:HOMEJI_TEST_DB='Host=127.0.0.1;Port=55439;Database=homeji_ui_qa;Username=homeji_qa;Password=local-ui-qa-only'
dotnet test tests/Homeji.Infrastructure.IntegrationTests
docker stop homeji-ui-qa-20261004
```

For an existing stopped container, use `docker start homeji-ui-qa-20261004` and omit
the role/database creation commands. Stopping preserves the QA schema; no automatic
container/volume deletion is performed.

## Covered

- Draft persistence, actual service validation, ownership rejection.
- Rejection of missing required images and an unowned storage path.
- Media metadata persistence, updating details, submitting, reloading from PostgreSQL
  after clearing EF tracking: pending status, latest title, three images and coordinates.
- The media step uses the frontend's Cloudinary HTTPS URL/bucket payload as well as
  a legacy `homeji-media` relative path. It reproduced the old bucket-validator failure
  before the fix, so the legacy-only case cannot mask a broken production upload flow.
- Seller inventory query returns active/sold/archived listings with media, in creation
  order, and excludes another seller's listing.
- HTTP cases for vacant rooms, roommate shares and room transfers: anonymous draft
  creation returns 401; another owner's edit returns 403; invalid coordinates/unowned
  media return 400; submission without images returns 422. Successful create returns
  201 with a detail URL. Updated details and three owned Cloudinary metadata payloads
  submit as Pending and are checked using a fresh PostgreSQL query.

Actual file upload, Supabase JWT authentication and migration compatibility still require
separate end-to-end verification. New HTTP cases currently compile but have **not passed**:
the 2026-10-04 run stopped at the loopback database guard because the dedicated local
PostgreSQL database was unavailable. The user requested continuing without Docker;
these optional persistence checks remain unverified, not a prerequisite to independent
UI/build/unit/API checks. This is environment failure, not evidence that the HTTP workflow passes.
