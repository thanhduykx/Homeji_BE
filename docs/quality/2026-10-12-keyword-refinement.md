# Visible keyword refinement

Tracking: [FE #16](https://github.com/fogit-hd/EXE101_Homeji/issues/16), [BE #13](https://github.com/thanhduykx/Homeji_BE/issues/13).

Search previously retained the provider keyword across turns without displaying it among editable criteria. The frontend now shows a trimmed keyword chip. Activating it prepares the dedicated `Bỏ từ khóa` edit and focuses the existing textarea. The explicit new-search action also restores input focus.

The backend recognizes the exact normalized dedicated command and clears only Keyword from the previous criteria. This avoids reintroducing a stale keyword or unrelated changed constraints from the provider. Broader and negative mentions are not treated as this dedicated command. Gemini is still called and provider failures still propagate; this does not introduce a fallback parser.

## Verification

- FE: 178/178 Node tests pass; build and focused ESLint pass. Chip tests cover trimming, empty values, preservation of other chips and input immutability.
- BE: 382 unit tests pass; 107 API tests pass and 13 opt-in database tests are skipped. Regressions cover exact accented/unaccented edits, negative/broader mentions, conflicting provider criteria, normal new keyword parsing and provider failure.
- Edge against the local opt-in fake API: keyword chip visible; Enter prepares the clear command and focuses `natural-rental-query`; submitting removes only the keyword chip while retaining location, budget, occupancy, kitchen and roommate exclusion; new-search clears the input and focuses it. The request audit confirms previous criteria were submitted. Screenshot: local ignored `output/quality/full-audit/keyword-clear-browser.png`.
- Browser fixture evidence verifies frontend interaction only. It does not establish live Gemini parsing, database behavior or production end-to-end success.

## Compatibility and release

The API request shape is unchanged. Deploy backend command handling before publishing the frontend clear control: an older backend may retain the keyword. Existing keyword parsing remains supported. No database migration or database writes are needed. Both local branches were synchronized with origin/main before publishing.

Advanced Routes and long-term history remain disabled.

## Exact public URL verified later on 2026-10-12

The required frontend origin https://exe101-homeji.onrender.com/ subsequently served index-CVpcSBgs.js containing the keyword chip and clear command. Authenticated browser verification on that exact origin produced a title-keyword chip; Enter populated the clear command and focused the textarea; submitting removed only the keyword while retaining the street, budget ceiling, occupancy, kitchen and roommate exclusion. Local screenshot: output/quality/full-audit/keyword-clear-production.png.

The earlier deployment report was premature while that URL still served the prior bundle. The alternate -qlcu service is not the user's public release target. No URL or OAuth-origin migration was performed. This one live Gemini refinement does not establish the full accuracy or latency benchmark. The owning Render service for the original frontend URL remains unidentified in the currently visible workspace; future releases must be verified directly at the required public origin.
