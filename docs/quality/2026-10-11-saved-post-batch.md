# Saved post batch loading

Tracking: [BE #8](https://github.com/thanhduykx/Homeji_BE/issues/8), related [FE #9](https://github.com/fogit-hd/EXE101_Homeji/issues/9). Independent releases; no API or migration dependency.

`SavedPostService.GetMineAsync` now uses the existing batch repository instead of fetching details individually. The saved list determines order; missing and inactive posts remain excluded. Empty saves avoid the rental-post query. The repository retains its no-tracking split query behavior.

This replaces a number of detail fetches proportional to saved posts with a fixed collection-loading query pattern. A single saved post can require extra split-query round trips, while larger lists avoid N+1 calls and collection cross-products. Dictionary lookup adds O(n) references; no speculative caching or pagination contract changes were introduced.

Verification on 2026-10-11: 326 application unit tests passed; 92 API integration tests passed, 13 PostgreSQL-dependent tests skipped. New tests check one batch call, chronology, media mapping, missing/inactive exclusion, empty results and cancellation forwarding. TRX files remain local under `output/quality/saved-post-batch/`. No migration or production database write was performed. Real PostgreSQL performance is not established by these tests.
