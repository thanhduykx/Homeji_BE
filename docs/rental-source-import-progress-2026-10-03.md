# Rental source data work — 2026-10-03

## Verified completion of the requested database work

After the user's confirmation, imported the full batch into Supabase Homeji
`pzxxzcrgpfsbdkyrdvng`, `homeji.rental_source_listings`, in one transaction.
The insert-only import added 80 rows; all 20 existing rows remained byte-for-byte
unchanged in the before/after snapshots. The database now contains 100 distinct
source advertisements and 677 original image URLs:

- Phongtro123 / Quận 9: 40 advertisements, 284 image URLs.
- Phongtro123 / Thủ Đức: 19 advertisements, 134 image URLs.
- Muaban / Thủ Đức: 41 advertisements, 259 image URLs.

Database facts (title, address, source URL, district, numeric price/area, gallery)
match all 100 prepared records; zero missing identities or fact mismatches.
Zero outside-district values, invalid numeric facts or missing galleries.
The 60 explicitly expired Thuephongtro advertisements were not imported.

Also removed the four remaining standalone `demo` labels in rental descriptions
and technical seed markers in 19 rental-wanted records. Optimistic predicates
matched ID, old title/description and old update timestamp. Wanted records retain
the visible sentence `Nhu cầu minh họa; không phải yêu cầu thuê thực tế.` because
their model has no synthetic flag. All non-text wanted fields stayed unchanged.
Rental/marketplace synthetic flags remain 44/23. Marker scans are zero for rental,
marketplace, wanted titles/descriptions and profile display names. Meaningful text
such as `hợp đồng mẫu` was preserved.

Before/after backups and executed SQL are under `output/data-import/` (ignored by
Git). The batch retains source expiry and unverified rights/availability metadata
in its JSON snapshot; the existing source table persists its current mapped facts.
Images were checked at collection time and stored as source URLs, not copied into
Homeji Storage. External advertisements are not verified landlord-owned rooms.
No schema/access-policy changes, account deletion, price edits or AI deployment
were performed in this completion step. The 20 collector/import tests pass.

Supabase security advisors have no ERROR. Existing leaked-password protection
WARN and deny-direct-access RLS INFO findings remain unchanged; no permissions
were opened to silence them.

The database objective is fulfilled. Publishing/connecting the source API to the
frontend and implementing the separately researched AI features are follow-up
product work, not claims made by the database import verification.

## Earlier collection and coordination record

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

The earlier writer-coordination question was answered by the user before the
completion writes above. Backend work was preserved; the current worktree also
contains a separate integration commit. No reset or blanket staging was used by
this chat to resolve that state.

## Follow-up product work

1. Extend the existing source model with freshness/expiry and rights metadata
   before exposing additional public recommendations or copying photographs.
2. Investigate the 13 existing empty rental records separately; do not manufacture
   facts or delete records with unknown dependencies.
3. Verify the website consumes the source data appropriately. Synthetic material
   must not appear as a verified real rental simply because its title was cleaned.

The requested import and marker cleanup are verified above. Physical availability,
photo-republication rights and public frontend integration are not certified.
