# Homeji email production verification

Canonical frontend: https://homeji.onrender.com. Backend: https://homeji-be.onrender.com.

## Implemented

- Signup uses a code entered on `/verify-email`, with resend cooldown and an attempt limit. Reset password retains its email link to the new frontend domain.
- Deposit/payment success and withdrawal requested/completed/rejected create immutable receipts. A durable outbox sends email with a UTF-8 HTML attachment; failed delivery does not reverse financial transactions.
- Authenticated users can view their own history at `/receipts`, preview HTML and download it. Historical receipts are backfilled without scheduling old emails.
- Pending signup challenges reference Supabase auth users with cascade deletion. Migration removes orphan challenges before adding the constraint.

## Verified evidence

Feature releases: backend `3616810`, frontend `9b69a7d`. Lifecycle cleanup: backend `16876f2`.

The final local PostgreSQL quality run passed **462 unit + 168 integration tests = 630**, with zero skipped or failed tests. Evidence directory: `output/quality/local-676469fc60e14c6bbf1f3ee9e4e09511`. Tests cover signed repeated payment callbacks, wallet credit/refund, transaction rollback, receipt owner isolation, concurrent outbox claims, uncertain delivery, OTP cooldown/attempt limits, resend preserving password, account-deletion cascade, HTML escaping and attachment encoding.

The production email verification report at ignored `output/production-email-verification.json` records registration 200, immediate resend rejection, later resend 200, old code rejected, new code read from the actual email preview and accepted, and password login 200. A new user saw an empty receipt history while existing receipts remained in the database. Test auth accounts were cleaned up.

Brevo events showed **delivered** for signup, password recovery, and a clearly marked test email with one HTML attachment. Recovery used the new domain. Provider acceptance alone is not treated as inbox delivery; the application's `EmailSentAt` field only records provider acceptance.

Production had 18 historical receipts and no backfill email jobs. No real-money payment or withdrawal was performed for this verification. The financial event-to-outbox path was verified against disposable local PostgreSQL; the production attachment delivery was a separate test email.

Frontend build and fixture browser checks covered invalid/valid OTP, resend cooldown, pagination, sandboxed preview and focus handling. Automatic verification of the browser's Blob download did not complete; HTML generation, preview and email attachment delivery were verified.

## Operations

See [transactional-email-and-receipts.md](transactional-email-and-receipts.md) for environment variables, rate limits, outbox reconciliation and rollback flags. Secrets belong in server configuration, never the frontend or committed files.
