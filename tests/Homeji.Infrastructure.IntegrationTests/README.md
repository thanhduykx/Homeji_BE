# Local PostgreSQL persistence checks

Run this project explicitly; it is separate from the fast application-unit-test suite.
It uses the real `ApplicationDbContext`, Npgsql repositories, rental service, validators
and domain rules. It does **not** exercise HTTP authentication, storage upload or the browser.

## Safety

- Only `localhost` / `127.0.0.1` and database `homeji_ui_qa` are accepted.
- No application configuration or production credentials are loaded.
- Use a dedicated non-superuser role with ownership of this disposable QA schema, not
  a production/application role. The role needs schema creation for `EnsureCreated`.
- Each test rolls back its inserted rows. No `DROP DATABASE`, cleanup SQL or production
  migrations are run. `EnsureCreated` verifies the current EF model, not migration history.
- Missing configuration fails explicitly; passing tests are never silently skipped.

## Reproduce with Docker (PowerShell)

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

Actual upload, API transport/authentication and migration compatibility still require
separate end-to-end verification.
