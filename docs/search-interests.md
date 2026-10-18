# Search interests and Gemini-assisted room discovery

## Behavior

Authenticated accounts can enable personalization at `/api/search-interests/settings`.
Personalization, Gemini and daily email are separate opt-ins, all disabled by default.
Only explicit room searches are observed. Three distinct browser sessions for the same
normalized area in seven days produce a proposal, never an automatic subscription.
The frontend reuses a tab session until 30 minutes of search inactivity. Reloads and
filter changes within that session count once per area. This is a convenience signal,
not abuse-proof proof of three different visits; request/session IDs are client supplied.

Users confirm the area and hard filters, optionally select a place and 0.5–30 km radius,
and can follow up to ten criteria. The feed contains up to twenty public active posts,
created after the follow and within thirty days, excluding the user's posts and posts
marked viewed. Existing rental availability, capacity, price, deposit and amenity rules
are reused. A radius uses straight-line distance, not commute time. Without a selected
place, an area is matched against listing text; ambiguous names can produce imprecise
geography. Newly approved old drafts are not treated as newly created listings.

Dismissal suppresses an area proposal for thirty days. Clearing history keeps explicit
follows but removes search history and viewed markers (previously seen rooms can return).
Turning personalization off clears follows and behavioral data and disables AI/email.
An empty consent record persists so the legacy profile-based notification path cannot
silently override opt-out. Operational cooldown/delivery timestamps persist to prevent
clear/toggle actions bypassing limits. Legacy users without settings retain legacy behavior.

## AI boundary

`ISearchInterestService` is the application boundary; infrastructure owns persistence,
matching and provider calls. `GeminiInterestRanker` shares configured `Gemini` credentials
and endpoint with existing AI search. No keys enter frontend code.

AI proposal is explicitly requested and sends at most five recent search criteria, with
a 1,000-character prompt cap, to the existing parser. Explicit latest filters take
precedence; results require review and confirmation. AI ranking sends confirmed criteria
and up to twenty already eligible listing IDs, titles, price, area and deterministic
matching reasons. No account name, email, user ID or device GPS is included. A selected
search center is part of the opted-in search criteria. Users can leave Gemini disabled
and use every normal matching/following function.

Proposal and ranking each have a persisted fifteen-minute cooldown. Titles/history are
untrusted data. Ranking accepts only candidate IDs, deduplicates them, and rechecks all
hard constraints and availability after the provider returns. Model prose is never used
as a factual matching explanation. Failure falls back to ordinary matching/newest order.
AI can improve ordering but cannot establish whether a room is actually still vacant.

## Persistence, privacy and delivery

The backend-only `homeji.search_personalization` table is protected with RLS and no
`anon`/`authenticated` grants. API ownership comes from the authenticated user, never
from a request user ID. Migration `20261018191601_AddSearchPersonalization` is additive.

A bounded JSONB aggregate holds one account's preferences and markers. This intentional
denormalization makes consent changes, follows, dedupe and notification creation atomic
under one per-user PostgreSQL transaction/advisory lock. It is not an analytics event
store. Bounds: 100 observations/seven days; 1,000 viewed/notified/emailed markers each
and 100 dismissals/thirty days; ten follows. Expired markers are pruned on access or
worker processing. Inactive enabled accounts are processed by the worker; disabled
accounts contain no behavioral data. A `(Enabled, CheckedAt)` index supports batches.
At materially larger scale, normalize follows/markers and index geographic candidates
with PostGIS; the current implementation performs at most ten bounded searches per user.

The background worker runs every minute and considers thirty oldest enabled accounts
not checked in fifteen minutes. Notifications are deduplicated per account/listing and
at most five are created per pass. Email requires explicit opt-in and confirmed address,
contains at most ten new matches and a management link, and is attempted at most once
per twenty-four hours. Notifications precede email. The existing Brevo transactional
sender provides branded HTML. This digest is not a transaction receipt and has no PDF.

A delivery claim and emailed markers commit before sending. Known failures release
markers for the next daily attempt. Timeout, crash or unknown provider result stays
uncertain and is not automatically replayed; the UI explains this. This favors avoiding
duplicate email over guaranteed delivery. There is a narrow consent-change/in-flight-send
race after the last consent check; email already submitted cannot be recalled. Sleeping
hosting or backlog can delay notifications; the cadence is not a delivery-time guarantee.

## Verification

`scripts/quality/Test-LocalBackend.ps1 -SkipLoadTest` applies all migrations to a disposable
local PostgreSQL database and runs the solution tests. `SearchInterestDatabaseTests`
checks consent/session thresholds/retention/account isolation, RLS/grants, legacy opt-out,
price/amenities/radius/availability/seen filters, notification dedupe and email opt-in/daily
limits, AI consent/cooldown, explicit-budget precedence, failure fallback and invented IDs.
Local fake providers prove contracts; they do not prove production Gemini or inbox delivery.

Primary references: [Gemini structured output](https://ai.google.dev/gemini-api/docs/structured-output),
[Supabase RLS](https://supabase.com/docs/guides/database/postgres/row-level-security).
