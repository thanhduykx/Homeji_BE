# Production verification — 19 October 2026 (Asia/Ho_Chi_Minh)

Backend feature `646252c` and HTTP boundary fix `24f1f24` were deployed. Frontend
`19acec9` adds discovery controls; `24ded16` preserves management deep-link intent.

The final disposable PostgreSQL run passed 463 application unit tests and 198 API/
integration tests (661 total, zero skipped), including four actual HTTP record-binding
cases. A production-only record annotation failure found during verification was fixed
before declaring the workflow complete; missing-profile requests now return forbidden.

An isolated confirmed test account exercised the deployed API successfully:

- All personalization defaults off; enabling requires explicit settings.
- Three observations within one session did not trigger a proposal. Three distinct
  sessions did; explicit maximum rent stayed at 4,000,000 VND after live Gemini proposal.
- Confirmed following worked; partial radius input returned 400. Clearing history kept
  the follow; disabling cleared follows/feed. Anonymous API access returned 401.
- Only this test account's follow timestamp was moved back seven days to test existing
  public rooms. No fake posts were published and no customer account was modified.
- One eligible public listing was returned. Live Gemini ranking succeeded and returned
  only eligible IDs. The worker created the account's in-app room notification.
- Email was explicitly enabled on the disposable account. Brevo reported both `sent`
  and `delivered` for its room digest. Provider mail-content inspection returned
  `Mail content not available`, so actual delivered HTML/logo appearance was not verified.
- The disposable auth account was deleted successfully; dependent preferences and test
  notifications follow the existing account cleanup relationships.

The deployed personalization table has RLS enabled and no SELECT privilege for either
`anon` or `authenticated`. Browser source inspection and desktop/mobile synthetic UI
checks were completed before browser access was disabled in Work settings. They prove
component interaction separately from provider and database verification.

Local evidence: `output/interests-production-check.json`,
`output/quality/local-950a70dbdee6498ea7707eef1dd90482/`, and frontend
`output/interest-ui-desktop.png`, `output/interest-ui-mobile.png`.
