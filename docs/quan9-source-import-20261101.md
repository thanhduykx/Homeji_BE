# Quận 9 source catalog — 2026-11-01

Imported 116 new external advertisements and refreshed 10 existing source records. Public API readback matched all 126 records, prices, areas, source links and 589 gallery URLs. Distribution: Mogi 111, Phongtro123 10, Phongtro.vn 5. Fifteen source addresses mention Lê Văn Việt; eighteen titles or addresses mention the street.

## Evidence and limits

- Batch: `data/quan9-rental-sources-2026-11-01.json`; committed database receipt: `quan9-data-import-receipt-20261101.json`; public API verification: `quan9-public-api-verification-20261101.json`.
- Read public listing HTML and listing-specific structured data. Retained original source identity, address, price, area, dates and gallery links. Excluded explicit expired/unavailable ads, unrelated recommendations and exact duplicate facts.
- Downloaded gallery candidates to memory and checked HTTP response, MIME type, image decoding, dimensions and size. Stored original image URLs, not copied image binaries. These checks do not authenticate the room or establish image reuse rights.
- Source ward labels include the thirteen former Quận 9 ward names and newer administrative labels. New Long Phước addresses can refer to the merged ward; these labels do not establish verified coverage of every former ward boundary. No coordinates were invented.
- Advertisements are source references, not Homeji landlord posts. No owner verification, physical room identity, current vacancy or image authenticity is asserted. Some Mogi publication dates are old; collection/check time is separate from publication time. A source without an explicit expiry does not prove current availability.
- No native rental posts, accounts or unrelated records were modified. Existing source coordinates were preserved.

## Reproduction

Requires Python with requests, beautifulsoup4, Pillow and psycopg. Run from the backend root. Collector limits and robots checks bound public requests; it does not access private APIs or bypass blocked sources.

```powershell
python scripts/data/collect_quan9_sources.py --help
python scripts/data/import_quan9_sources.py --help
python -m unittest discover -s scripts/data -p 'test_quan9_collectors.py'
```

The importer defaults to dry run, requires the explicit expected Supabase project, validates source/image hosts and values, uses parameterized SQL in a transaction, and requires at least 100 new records. Replaying this already-imported batch intentionally fails that new-record requirement. Do not apply it again to production.

## Public sources

- https://mogi.vn/ho-chi-minh/quan-9/thue-phong-tro-nha-tro
- https://mogi.vn/ho-chi-minh/quan-9/thue-phong-tro-nha-tro/duong-le-van-viet-quan-9-sid8486
- https://phongtro123.com/tinh-thanh/ho-chi-minh/quan-9?orderby=moi-nhat
- https://phongtro123.com/tags/phong-tro-duong-le-van-viet-quan-9.html
- https://phongtro.vn/cho-thue-phong-tro-phuong-long-phuoc-tp-ho-chi-minh

## Validation

Frontend: 455 tests passed, production build passed, changed-file ESLint passed. Backend: rental-source filtered suite passed (9 unit tests, 4 integration tests; 1 opt-in integration test skipped). Existing Python source tests: 23 passed; new source-specific collector tests: 2 passed. Public API readback: 126/126 records, no missing IDs or field mismatches.
