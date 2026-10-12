# Comparable room prices in administrator analytics

Date: 2026-10-12. Tracking: Homeji_BE#18 and EXE101_Homeji#22.

Admin analytics previously combined asking prices with different units, while area averages included zero/unknown rent. KPI and area price statistics now accept only positive whole-room monthly rents. Price/m2 additionally requires positive floor area. Supply and demand counts still include all active listings.

The rental snapshot LEFT JOINs owner profiles, preserving listings whose owner profile is missing. It reuses RentalCostCalculator.ResolvePriceBasis rather than introducing another interpretation. RoommateShare with Renter owner is person/month, Landlord owner is room/month, missing/Admin owner is unknown; ordinary room/transfer types retain the established room/month basis. The query selects only required columns and does not add per-listing database reads.

Additive JSON metadata on both kpis and each areas item: priceBasis=room_month, priceSampleCount, excludedPriceCount, priceAreaSampleCount. Existing decimal aggregate fields remain for compatibility; zero samples mean unavailable, not free rent. The coordinated frontend requires valid metadata before displaying prices/advice. Price recommendations require at least four eligible price/area samples locally and globally plus existing interaction thresholds. These are operational gates, not statistical confidence. No price mutation occurs.

Tradeoff: units follow the existing resolver and CURRENT author role; changing role may alter eligibility of existing roommate listings. These are not immutable historical units. Persisting an explicit unit needs a separate domain/data migration decision. There is no unit conversion, database migration or production data rewrite in this change.

Verification: six focused regressions failed before the implementation change. Final application suite: 430 passed. API suite: 123 passed, 13 opt-in database cases skipped. Tests cover mixed units, null/unknown/zero/negative prices, missing area, preserved demand counts, real endpoint camelCase metadata and Admin-only access with test authentication/repositories. Npgsql query translation verifies LEFT JOIN and period predicates without a database connection; this is not execution against a real PostgreSQL database. git diff --check passed.
