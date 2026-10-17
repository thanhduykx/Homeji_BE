# Purchase receipts and password recovery OTP

Purchases paid from Homeji balance now record a buyer receipt in the same database save as the wallet debit, reserved inventory and order. This covers individual goods orders and every line in a food checkout. A cancellation, rejection or expiration records a separate refund receipt with the actual refund timestamp. Money is held for the order; a purchase receipt does not claim delivery or payout to the seller.

New receipt events are `6` (purchase paid) and `7` (purchase refunded). HTML includes item title, quantity, unit price and total. Existing receipt IDs/events and APIs remain compatible. The migration backfills only orders with a matching wallet purchase debit, and records historical refunds without enqueueing emails.

Password recovery uses `POST /api/account/request-password-otp` with `{ "email": "..." }`, then `POST /api/account/reset-password-otp` with `{ "email": "...", "code": "...", "newPassword": "..." }`. The existing link endpoints remain available for already-sent links and older clients. Signup OTP is unchanged.

Recovery requires the existing Brevo/registration-OTP configuration. Only confirmed accounts receive recovery codes. Unknown/unconfirmed accounts and requests within cooldown return the same public message. A PostgreSQL upsert enforces a 60-second cooldown across instances; an atomic counter permits five verification attempts and a ten-minute challenge lifetime. Codes are verified with Supabase type `recovery`; signup codes do not satisfy that purpose. Neither codes nor passwords are stored in the challenge table, and the recovery session stays on the backend. RLS blocks direct public access; deleting an auth user cascades to challenges.

Local verification: 462 unit and 169 integration tests passed with no skips. The recovery test covers unknown-account responses, cooldown, wrong codes, five-attempt limit, expiry, resend without password/data, password update only after verification, one-time use and deletion cascade. Database purchase tests assert buyer receipts/outbox and one refund per order alongside actual wallet changes.

No real-money production transactions are used for verification. Transactional email delivery and the financial database path are checked separately.

## Production verification, 18 October 2026

Backend release `c853a1b` applied the migration. Production contains 36 purchase receipts and 23 refund receipts from evidenced historical wallet transactions; zero historical marketplace email jobs were created. Recovery RLS and the auth-user cascade constraint are enabled.

The ignored report `output/production-recovery-otp-check.json` records request 200, identical unknown-account response, wrong-code rejection, successful OTP password reset, successful login with the new password, rejection of the old password and rejection of code replay. The OTP was read from the actual Brevo email preview; Brevo recorded `sent` and `delivered`. The disposable test account was removed.

Frontend release `6b4b441` is live at https://homeji.onrender.com. The generated WebP returns HTTP 200 with `image/webp` and 380358 bytes. Browser inspection confirms the new illustration loaded, with zero old WebGL canvases/scene controls. Both the deployment worktree and the original working folders build successfully; unrelated local edits were preserved. An additional targeted cart test run passed all five cases, including receipt amounts/titles/order references for both pickup and seller delivery.

Supabase security advisors report the recovery table as RLS enabled with no public policy (intentional backend-only access). The pre-existing leaked-password-protection warning remains outside this change; see https://supabase.com/docs/guides/auth/password-security#password-strength-and-leaked-password-protection.
