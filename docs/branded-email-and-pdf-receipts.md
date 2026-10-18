# Branded account email and receipt attachments

OTP signup and recovery emails share a responsive HTML layout with the website's Homeji logo, a prominent six-digit numeric code, ten-minute validity text, and a security notice. Existing confirmation/reset links use the same branded layout. No codes appear in subjects or logs.

Receipt email displays the complete HTML receipt in the message body. It also attaches **two files**, with the same receipt reference: `Homeji-HJ-<id>.pdf` and `Homeji-HJ-<id>.html`. Amount, time, status and explanatory notes use the same presentation helpers. The HTML attachment exactly matches the email body. The website HTML preview also uses that renderer.

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
