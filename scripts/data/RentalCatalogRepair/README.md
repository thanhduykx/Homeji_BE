# Rental catalog repair runner

Run from the backend repository root with .NET 9. The runner reads the ignored
`src/Homeji.Api/appsettings.Local.json`, verifies the existing Homeji database
target, and never prints its connection string. Do not commit that configuration
or the database snapshots.

Inspect and back up without modifying application tables:

```powershell
dotnet run --project scripts/data/RentalCatalogRepair/RentalCatalogRepair.csproj
```

Apply a reviewed SQL file after automatically backing up rental posts, media,
amenities and source advertisements, in a repeatable-read transaction:

```powershell
dotnet run --project scripts/data/RentalCatalogRepair/RentalCatalogRepair.csproj -- --apply scripts/data/quarantine-synthetic-rentals-20261015.sql
```

Source refresh SQL is produced separately by `render_rental_refresh_sql.py` from
collector evidence and the full `rental_source_listings.json` backup. Its guard
rejects changed or missing existing rows rather than overwriting concurrent work.
The runner commits only after the SQL and summary complete. Failure rolls back
the transaction; snapshots remain under ignored `output/data-import/`.

The JSON table files contain the state **before** applying SQL. `summary.json`
contains counts after applying SQL, and `applied.sql` records the exact statements.
Run inspection again for an independent post-update snapshot. Source expiry checks
describe advertisements, not physical room availability or ownership.
