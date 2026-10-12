# Price basis in rental decision costs

Tracking: [FE #15](https://github.com/fogit-hd/EXE101_Homeji/issues/15), [BE #12](https://github.com/thanhduykx/Homeji_BE/issues/12).

The newly merged decision tool previously summed per-person roommate rent with group-level fees and compared price differences across incompatible units. The backend now batch-loads owner profiles once, enriches returned post roles, and resolves room/person/unknown using the established listing rules. It only computes a numeric price difference for positive source prices sharing a known unit.

Per-person or unknown prices retain the source amount; aggregate monthly/initial values remain unconfirmed because this group-fee scenario cannot be combined safely without additional unit confirmation. No automatic multiplication or division is introduced. Nonpositive source rent prevents total and initial-payment confirmation, while known positive fee components can still be shown. This favors an explicit missing-data state over an amount combining incompatible scopes.

Frontend results were extracted into a presentation component to test actual table and budget wording. It labels source units and guards subtotal/total/budget claims even against older backend responses. Missing rent/area display unknown; known fee subtotals are labeled as excluding unknown rent. Mixed or unknown units suppress a legacy numeric difference summary. Routes output remains gated off.

## Compatibility and release order

The new cost.priceBasis field is additive. knownMonthlySubtotal becomes nullable, which requires consumer handling. Publish the frontend null/basis guards before deploying the backend nullable response; older frontend formatting could display null as zero. Other repository/client consumers must also preserve unknown values. Existing request shape is unchanged. No migration or database writes are required.

## Verification on 2026-10-12

- FE: 177/177 Node tests pass; six new real-component SSR cases cover old/new room responses, person/unknown/conflicting units, missing rent, mixed summaries and malformed aggregates. Build and focused ESLint pass.
- BE: 372 unit tests pass; 107 API integration tests pass, 13 opt-in database tests skipped. New application tests cover source-unit arithmetic and owner-role hydration with one batch query, cancellation propagation, absent profiles and empty selections.
- Live Gemini, database and Render deployment are not established by these checks. Local evidence is under FE output/quality/full-audit/cost-basis-* and backend test results.

Advanced Routes and long-term history remain disabled.

## Release observation

After FE commit 22623ec was pushed, the public production entry loaded `/assets/index-BJh4K7Y7.js` and `SavedPostsPage-D4IapWEd.js`. Read-only HTTP inspection confirmed both the unit/scope guard text and the missing-rent fee-only subtotal label in that loaded chunk before the backend push. Build hashes differ with deployment environment; this proves these guards are served, not an exact deployment commit or successful browser interaction. Backend release confirmation remains separate.
