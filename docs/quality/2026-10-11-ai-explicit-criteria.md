# Explicit AI constraint validation

Tracking: [BE #10](https://github.com/thanhduykx/Homeji_BE/issues/10); related [FE #11](https://github.com/fogit-hd/EXE101_Homeji/issues/11). Independent releases; no schema migration.

Client-provided `Criteria` now passes the same strict validation as `PreviousCriteria` before normalization or retrieval. Previously a malformed budget, occupant count or amenity could normalize away and widen the search silently. Validation now retains the appropriate error field (`criteria` versus `previousCriteria`). Partial criteria with nullable optional fields remain supported; total-budget/unknown-fee candidates remain unconfirmed as before.

Regression cases failed before the fix and pass afterward. Tests also exercise the exact frontend location-chip phrase through deterministic intent and the AI search service, preserving budget, kitchen, occupancy and area constraints while changing location.

Verification on 2026-10-11: 344 application unit tests passed, no skips; 92 API integration tests passed, 13 PostgreSQL-dependent tests skipped. The existing global exception handler maps these validation exceptions to HTTP400; this patch's new invalid-criteria tests exercise the application service and error field, not a new authenticated HTTP round trip. Local TRX evidence: `output/quality/ai-explicit-criteria/`.

No database writes, migration, provider key or feature flag changes were performed. Advanced Routes and long-term chatbot history remain disabled.
