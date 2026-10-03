# Rental source data work — 2026-10-03

## Objective and boundaries

Remove technical seed markers from displayed data and import approximately 100 real
advertisements with their own photographs from multiple sites. Geography remains
the existing Quận 9 / old Thủ Đức scope; nearby Dĩ An/Bình Dương and old Quận 2
are not substitutes. External advertisements do not create Homeji landlords,
verified ownership, guessed deposits, or invented map coordinates.

## Work completed by this chat

- Ran the existing primary collector without modifying it: 38 Phongtro123 records,
  274 validated original image URLs. Snapshot:
  `output/data-import/rental-sources-20261003-124527.json`.
- Added a Thuephongtro collector and preserved explicit expiry. Its 60 records / 302
  image URLs were all marked expired and are excluded from the candidate import.
- Added a Muaban collector reading only the advertisement's own public page data.
  It excludes expired ads, checks its own gallery and identity, and never saves
  contact information or descriptions. Result: 41 records / 259 image URLs.
- Added a batch validator with source/image host allowlists, finite numeric limits,
  field lengths, geography, duplicate source identity, and expiration checks.
  Prepared batch has 100 candidates / 677 original image URLs, not yet imported by
  this chat. The 60 expired records remain separate evidence, not available rooms.
- Completed an additional primary-source collection: 21 records / 144 image URLs,
  using observed next-page links and excluding existing URLs. Snapshot:
  `output/data-import/additional-rental-sources-20261003-125549.json`.
- The final candidate batch is `output/data-import/homeji-source-batch-20261003.json`:
  59 Phongtro123 advertisements and 41 Muaban advertisements. The 60 expired
  Thuephongtro advertisements are excluded, not silently relabeled as available.
- All 17 parser/batch tests pass. These tests do not prove physical availability
  or permission to republish another advertiser's photographs.

Images are stored as source URLs in the snapshots, not downloaded copies in Homeji
Storage. `rights_status=not_confirmed` and `availability_verified=false` remain
explicit. No public publication or database mutation was performed by this chat
in this data continuation.

## Concurrent external updates observed

Read-only Supabase checks showed the live state changing during this work without
a database write from this chat: technical markers disappeared, `is_synthetic`
columns appeared, and `homeji.rental_source_listings` was created with 20 records /
150 image URLs from Phongtro123. The 44 rental / 23 marketplace seed records still
have synthetic flags. Do not repeat the migration or overwrite these changes.

There are also 13 rental records with empty title and description (11 status 1,
2 status 5). This was observed, not attributed to a particular writer. Do not
invent replacement contents. Investigate their source/backup before repairing.

An asynchronous user question is pending: which chat/person owns database writes?
Do not write while the same rows/schema are being changed by another writer.
Backend also contains extensive uncommitted work owned by the user; no blanket
stage, reset, migration deployment, or backend publication is authorized by this
data collection alone.

## Next actions after coordination

1. Revalidate the final combined batch against the latest source/table state.
   Preserve source identity and use an idempotent import, not duplicate rows.
2. Compare against the live table and migration history, then agree one writer.
   Preserve source expiry and provenance in the persisted import design.
3. Import the validated batch transactionally, retaining original source links,
   image URLs, and unverified availability. Do not publish expired ads.
4. Verify database counts by source/district, image references, uniqueness, and
   remaining technical markers. Investigate empty records without changing other
   users' data or manufacturing facts.
5. Verify the website consumes the source data appropriately. Synthetic material
   must not appear as a verified real rental simply because its title was cleaned.

The full goal is not complete: database import and end-to-end verification remain.
