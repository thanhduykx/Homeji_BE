# Premium AI posting agent

The initial action is secondhand marketplace posting. A user asks to post, attaches up to three images, receives an editable preview containing those images, and explicitly presses Confirm to publish. No model-generated text can invoke publication. Normal marketplace validation, moderation, wallet reserve and seller-location rules remain authoritative.

## Boundaries

- Check active Premium in the database before generation and again before confirmation. Basic users receive an upgrade action plus the normal self-posting route; ordinary questions remain available.
- The chat retains at most six recent messages, 1,000 characters each. Posting uses the current request, at most three recent user messages and the current owned draft. Images are not included in general conversation history.
- A private `homeji.agent_post_drafts` row stores the processed original images for a one-hour approval period. RLS is enabled and anon/authenticated direct table grants are revoked. Images upload to listing storage only after confirmation. Publication/cancellation clears temporary image bytes; a five-minute worker clears expired payloads and deletes old unpublished metadata after 30 days.
- Draft UUID plus an advisory transaction lock serialize approval/cancellation/revision across instances. Publication and the durable approval receipt commit together. Same-body retries return the original post; changed-body retries require opening the posted item. An uncertain cloud upload can leave an orphan image, but cannot create a second marketplace post.
- Revisions supersede the preceding draft. A session can leave posting mode explicitly. Confirmation never trusts a client-supplied owner or image URL.
- Generation has a rolling 24-hour draft-count guard of 20 per Premium user, plus existing costly-operation rate limits and bounded media input.

## Launch offer

`Subscriptions:Premium:LaunchOfferStartsAt/EndsAt` defines one shared seven-day window. Only the configured `PREMIUM_MONTHLY` plan priced at 99,000 VND is discounted to 49,000 VND. Its 30-day entitlement is unchanged. Other packages remain unchanged.

Catalog, wallet, MoMo and PayOS use the same server-side effective-price method. Gateway creation accepts an optional expected price; the new frontend always sends the reviewed price. Expiration returns the catalog to regular price and rejects a stale checkout amount. Already-created payment amounts are not rewritten during callback settlement. Existing payment idempotency and renewal behavior are preserved.

## Validation

- Release API build and focused chatbot/provider/subscription regression tests.
- Isolated PostgreSQL full migration plus real marketplace service: preview creates no post, Basic cannot generate/publish, concurrent confirmation publishes once, changed retries fail, wrong owner/cancel/expiry/zero price cannot publish.
- Launch window boundary tests, other-plan isolation, bounded context and removal of private image bytes.
- Frontend browser fixture: actual chat/editable preview, formatted price edits, duplicate clicks, failure/retry, bounded request context, Basic upgrade/self-post actions, responsive banner and dismissal. Payment confirmation regression covers wallet/MoMo/PayOS.
- No paid production purchase or public production test listing is created by verification. Production release/API checks are distinct from authenticated real-user posting/provider quality.
