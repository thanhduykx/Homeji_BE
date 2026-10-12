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
