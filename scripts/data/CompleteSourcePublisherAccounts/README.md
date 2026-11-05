# Complete imported publisher accounts and listings

Run from the backend root. Configuration stays in the existing local settings; never copy keys into reports.

```powershell
dotnet run --project scripts/data/CompleteSourcePublisherAccounts -- --self-test
dotnet run --project scripts/data/CompleteSourcePublisherAccounts -- --publish
dotnet run --project scripts/data/CompleteSourcePublisherAccounts -- --apply --publish
dotnet run --project scripts/data/CompleteSourcePublisherAccounts -- --verify
```

Without `--apply`, no remote records change. Both account tools share a database advisory lock.

The user authorized name-based placeholder emails. A normalized Vietnamese name plus the account UUID forms a unique address under `publishers.homeji.invalid`. Only previously imported, unclaimed accounts are eligible; existing ordinary accounts and different pre-existing email addresses are never overwritten. Admin updates retain unconfirmed contacts and the account's pending claim state. This does not send invitations, confirmation messages, passwords, or notifications.

Active source listings map to their actual imported contact profile by phone, never by display name alone. The source UUID is retained as the native post UUID. A collision with another owner's native post aborts the operation. Every new link and full source snapshot is recorded in `homeji.source_publisher_listing_links`, alongside a local backup in ignored `output/source-publisher-accounts/`.

Complete listings use the domain's validation, image minimum, submission and approval transitions. Incomplete listings remain drafts: unset draft coordinates are not published, and missing images are never duplicated to meet the minimum. No source record is deleted or falsely expired. Ordinary accounts, subscriptions, existing posts, verification and conversations are not modified.

Unknown source price units, capacity and fees remain unknown to the UI and AI. `rental-source-import` media provenance identifies these posts without a database schema change. The shared native detail, saving, shortlist and contact features use real native IDs. These imported profiles have not been claimed by the people named in the source; an account record does not establish their identity or message delivery.

After a successful apply, copy the public-ID-only manifest into the frontend and run its build and imported-native browser checks:

```powershell
Copy-Item -LiteralPath output/source-publisher-accounts/public-listing-links.json -Destination C:/EXE101_Homeji/src/lib/sourcePublisherLinks.generated.json
```

The manifest contains only public listing/profile UUIDs. Reports, full snapshots, contact conflicts and local settings must remain outside version control. Rerunning previews verifies idempotency; it does not republish or overwrite existing native posts. `--verify` checks database invariants and two public detail endpoints, whose normal view counters can increment.
