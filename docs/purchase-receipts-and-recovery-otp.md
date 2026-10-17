# Purchase receipts and password recovery OTP

Purchases paid from Homeji balance now record a buyer receipt in the same database save as the wallet debit, reserved inventory and order. This covers individual goods orders and every line in a food checkout. A cancellation, rejection or expiration records a separate refund receipt with the actual refund timestamp. Money is held for the order; a purchase receipt does not claim delivery or payout to the seller.

New receipt events are `6` (purchase paid) and `7` (purchase refunded). HTML includes item title, quantity, unit price and total. Existing receipt IDs/events and APIs remain compatible. The migration backfills only orders with a matching wallet purchase debit, and records historical refunds without enqueueing emails.

Password recovery uses `POST /api/account/request-password-otp` with `{ "email": "..." }`, then `POST /api/account/reset-password-otp` with `{ "email": "...", "code": "...", "newPassword": "..." }`. The existing link endpoints remain available for already-sent links and older clients. Signup OTP is unchanged.

Recovery requires the existing Brevo/registration-OTP configuration. Only confirmed accounts receive recovery codes. Unknown/unconfirmed accounts and requests within cooldown return the same public message. A PostgreSQL upsert enforces a 60-second cooldown across instances; an atomic counter permits five verification attempts and a ten-minute challenge lifetime. Codes are verified with Supabase type `recovery`; signup codes do not satisfy that purpose. Neither codes nor passwords are stored in the challenge table, and the recovery session stays on the backend. RLS blocks direct public access; deleting an auth user cascades to challenges.

Local verification: 462 unit and 169 integration tests passed with no skips. The recovery test covers unknown-account responses, cooldown, wrong codes, five-attempt limit, expiry, resend without password/data, password update only after verification, one-time use and deletion cascade. Database purchase tests assert buyer receipts/outbox and one refund per order alongside actual wallet changes.

No real-money production transactions are used for verification. Transactional email delivery and the financial database path are checked separately.
