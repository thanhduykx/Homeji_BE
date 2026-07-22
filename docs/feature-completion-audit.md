# Homeji backend feature completion audit

Audit date: 2026-07-14. Source of truth: all tabs in the Homeji requirements document, including the renter and landlord tables, not only the copied summary tab.

## Implemented backend flows

| Area | Capability | Primary API |
|---|---|---|
| Account and access | Email registration/confirmation, duplicate-email check, login, forgot/reset password, Google OAuth hand-off, Supabase JWT and role policies | `/api/account/*` |
| Profile | Renter and landlord profile, contact address, rental need, lifestyle and roommate preferences, verified and Premium badges | `/api/profile/*` |
| Rental discovery | Keyword, price, deposit, vacancy, move-in date, amenity and map-bound filters; enriched detail; 2–4 room comparison | `/api/rental-posts`, `/api/rental-posts/compare` |
| Rental management | Draft, media, edit with re-moderation, submit, archive, mark rented, owner statistics and admin approval/rejection | `/api/rental-posts/*`, `/api/admin/moderation/*` |
| Wanted rooms | Renters publish, search, update and close room-wanted posts; landlords can start a direct conversation | `/api/rental-wanted-posts/*` |
| Saved rooms and roommates | Save/remove/list, compatible roommate candidates, invitation lifecycle and private roommate chat | `/api/saved-posts/*`, `/api/roommate-invitations/*`, `/api/roommate-chats/*` |
| Direct messaging | Renter-landlord, buyer-seller and wanted-post conversations with persistent messages and notifications | `/api/conversations/*` |
| Viewing appointments | Request, confirm, reject, cancel, reschedule and complete | `/api/viewing-appointments/*` |
| Reviews | Review after a completed viewing, one review per user/post, seven optional criteria, aggregate scores and report support | `/api/rental-posts/{id}/reviews`, `/api/reports` |
| Marketplace | Create/search/update/sold/archive listings; purchase request, pickup acceptance/rejection/cancellation/completion; completion marks item sold | `/api/marketplace-posts/*`, `/api/marketplace-orders/*` |
| Trust and safety | Report users, rental posts, marketplace posts, wanted posts, reviews and roommate invitations; content moderation and landlord verification | `/api/reports`, `/api/admin/*`, `/api/landlord-verifications/*` |
| Notifications | REST inbox/read state and authenticated SignalR push for messages, appointments, moderation, saved-post changes, new matching rooms and marketplace orders | `/api/notifications`, `/hubs/notifications` |
| Activity history | Categorized views, searches, messages, payments, reviews and successful authenticated mutations without storing request bodies | `/api/activities` |
| Premium | Basic entitlement, Premium badge and ranking boost; monthly, quarterly and yearly plans | `/api/subscriptions/*` |
| Payments | MoMo and PayOS creation/callback/webhook, idempotent Premium activation and user payment history | `/api/payments/*`, `/api/subscriptions/*` |
| AI search | Gemini NLP parsing into allow-listed filters, database search, review-aware ranking and structured highlighted results | `/api/ai/*` |
| AI chatbot | Persistent conversations; multi-turn rental intent combines recent messages and returns a structured `searchUpdate` for map/filter synchronization | `/api/chatbot/*` |

## Frontend and provider responsibilities

- GPS permission, map movement, pins, clustering and the visual “AI bảo thế đó” effect are implemented by the frontend/map SDK. The API returns coordinates, filters and ranked highlights.
- Voice capture and speech playback are device/speech-provider features. Transcribed text can be sent to AI search or chatbot endpoints.
- Google sign-in starts in Supabase OAuth; the backend validates the resulting Supabase access token.
- Chatbot popup layout, optimistic message states and filter-panel synchronization are frontend responsibilities; the backend persists messages and returns `searchUpdate`.
- Media bytes are uploaded to the configured storage/CDN; the backend validates and stores HTTPS media metadata.

## Conditional product decisions

Marketplace holding is now an explicit product decision: checkout debits the buyer, the seller marks the whole order delivered, and Homeji holds the Seller Net Amount for 24 hours. Buyer confirmation can close receipt early but cannot release funds early; a background lifecycle worker auto-completes and releases the held amount after the deadline. Production operation still requires an approved cancellation/dispute policy, PSP merchant approval and webhook reconciliation.

Search-by-travel-time and route distance require choosing and funding a geocoding/directions provider. Current APIs support text, coordinates and bounding-box filtering without locking the product to one provider.

## Operational verification

- Migration `CompleteFullFeatureAudit` creates the new conversation, marketplace-order and wanted-post tables and adds the audited profile, activity, rental-detail and review fields.
- EF Core reports no pending model changes after the migration.
- Swagger uses unversioned `/api/...` routes.
- `/health/ready` verifies the Supabase PostgreSQL connection.
- SignalR clients connect to `/hubs/notifications` with a Supabase access token and listen for `notificationReceived`.
