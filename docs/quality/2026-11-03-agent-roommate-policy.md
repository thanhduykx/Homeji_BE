# Agent roommate authoring and explicit posting language

The additional live-provider business-flow check exposed an unrecognized “đăng tìm bạn ở ghép” request. Unit reproduction confirmed three posting variants returned null. A separate database test confirmed a renter was rejected by Agent with “Cần vai trò chủ trọ”, even though ordinary RoommateShare authoring explicitly allows renters.

Changes:

- Recognize accented “đăng tìm” and “đăng tuyển” as explicit posting requests. Preserve “đang tìm” as discovery: diacritic removal cannot decide this distinction safely.
- Extract the existing normal-form authoring policy into `RentalPostAuthorPolicy` and reuse it for both draft preparation and confirmation. Renters retain roommate/pass permissions, landlords retain rental/roommate permissions, and all other pre-existing role restrictions remain intact. Premium is still checked separately before generation and confirmation.
- Add a nine-case permission matrix and an actual database regression that prepares and confirms roommate sharing as a renter through the same normal rental service. Ownership, approval and idempotency tests remain enabled.
- The maintenance runner now also backs up marketplace posts/media before applying SQL. A guarded transaction removed exactly five directly verified 404 image references; restore SQL and original media rows are saved locally. It never removed posts, users, orders or image files.

Validation: 694 Application unit tests passed; isolated PostgreSQL Agent integration replay passed 11 tests with three explicit live-provider skips. The four-case additional real-provider test initially exposed the routing issue; after the code fixes, its provider call returned `ExternalServiceUnavailableException`. The deterministic roommate publication regression passed in that same run. The extra four live cases are not signed off. Earlier sofa/rental live receipts are separate evidence and do not establish those extra cases.

No production listing was posted by these tests, and no payment was initiated. The fixture database and generated password files were stopped/cleaned by the test runner.
