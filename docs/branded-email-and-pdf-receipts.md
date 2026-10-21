# Branded account email and receipt attachments

OTP signup and recovery emails share a responsive HTML layout with the website's Homeji logo, a prominent six-digit numeric code, ten-minute validity text, and a security notice. Existing confirmation/reset links use the same branded layout. No codes appear in subjects or logs.

Receipt email displays the complete receipt in the message body. It attaches the transaction PDF and the embedded Homeji-logo.png image. It no longer attaches an HTML file. Logo bytes come from the bundled asset, with no remote download at send time. The website renders receipt fields natively in the selected theme and offers no HTML download control.

PDFs use the existing SkiaSharp dependency. The website logo and Noto Sans Regular/Bold fonts are embedded resources; PDF rendering needs no browser, network call or system-installed font. PDF text remains selectable, Vietnamese renders correctly, long references wrap, and long descriptions continue on another page. A typical PDF is about 700 KB because fonts are embedded. Its note distinguishes Homeji receipts from VAT invoices and does not claim delivery or bank settlement before the matching business state.

If document generation fails, the outbox records `receipt_files_failed` and retries without calling Brevo. A receipt email is not sent with missing files. Financial transactions remain independent from delivery. Provider timeouts retain the existing uncertain-outcome handling and stable idempotency key.

The logo URL in HTML is `https://homeji.onrender.com/brand/homeji-logo2.png`; email clients may apply their normal remote-image policy. The visible Homeji wordmark remains text, and the PDF embeds the logo for offline access. The receipt HTML endpoint CSP permits images only from that site and continues to block scripts/forms.

## Validation

- Full local PostgreSQL quality run: 462 unit + 179 integration tests, zero failures/skips (`output/quality/local-140732a2f71048c3a1357936875f9ac4`).
- Document tests cover all seven receipt states, HTML escaping, both attachment payloads, correct OTP purposes and guard text, and long/multiline input.
- Parsed the generated PDF: selectable Vietnamese, correct `125.000 VND`, timestamp in UTC+7, embedded logo, one normal page. The long-content sample wraps to two pages, preserving all 45 input lines and a 120-character reference. Rendered previews were checked visually.
- Docker build also runs the document tests with native Linux Skia before publication, alongside the existing image-processor tests.

No real payment or withdrawal is required for these checks. Generated preview receipts are explicitly marked as test documents.

## Font provenance

Fonts are the unmodified Noto Sans static TTF files from the Noto Fonts project:

- https://github.com/notofonts/noto-fonts/blob/main/hinted/ttf/NotoSans/NotoSans-Regular.ttf
- https://github.com/notofonts/noto-fonts/blob/main/hinted/ttf/NotoSans/NotoSans-Bold.ttf

The SIL Open Font License and copyright notice are retained in `External/EmailAssets/NotoSans-LICENSE.txt` and embedded in the assembly. The logo is copied from the existing frontend brand asset.

## Six-digit OTP contract

Supabase Email OTP length is configured to 6. Signup and recovery API validation and web inputs require exactly six ASCII digits, including leading zeroes. Expiration, cooldown and attempt limits remain unchanged. Previously issued eight-digit codes require requesting a new code.

Production verification on 2026-10-18: recovery email delivered, actual six-digit code accepted by reset API (HTTP 200). A separate explicitly labelled sample receipt was delivered with two attachments (PDF 701977 bytes and HTML 5125 bytes). Brevo rewrites the image URL to its image proxy, so checking for the original logo filename in retrieved body is not a valid delivery assertion. No real financial transaction was made. Six-digit change quality run: 462 unit + 183 integration tests, zero failures/skips, in `output/quality/local-eff1c43a1f72452b862d150efaa86ee5`.

## Receipt design based on the supplied email reference

The receipt uses a full-width teal brand banner, parallel accent rules, a total/date summary and two columns: transaction metadata and a white paper receipt with dashed separators. On narrow email layouts the columns stack. The email body uses the HTML renderer; PDF follows the same hierarchy and branding. Unknown fees, discounts and quantities are not fabricated from free-form descriptions. Long PDFs retain flowing pagination.

Reference redesign checks: 14 document design cases plus sender/dispatcher tests; PDF text extraction confirms Vietnamese, amounts, all 45 long description lines and complete long reference. Normal PDF remains one page; long sample remains two.

## Attachment change, 2026-10-21

Receipt emails now attach the existing PDF plus Homeji-logo.png, replacing the HTML attachment. The HTML email body, Vietnamese PDF, amounts, statuses, recipients, outbox retries and idempotency behavior are preserved. Historical delivery evidence above describes the older attachment policy, not the new one. Automated checks validate all seven receipt events, PNG signatures, PDF signatures, and absence of HTML filenames; no customer email or financial transaction is sent by these checks.

## History action, 2026-10-21

Receipt mail now links directly to the protected wallet-history entry (`/marketplace?market=wallet&wallet=history`) rather than labelling the receipt list as transaction history. It opens a browser tab and includes the visible fallback URL. The footer accurately describes the PDF and PNG attachments. Existing delivered emails are immutable. Renderer tests lock the escaped query, new-tab target and updated copy; browser checks follow the generated email link with APIs intercepted and verify history both with an existing session and after mocked login.
