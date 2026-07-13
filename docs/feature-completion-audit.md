# Homeji backend feature completion audit

Audit date: 2026-07-14. Source of truth: the original Homeji feature document and the current API implementation.

## Backend-complete flows

| Area | Backend capability | Primary API |
|---|---|---|
| Authentication | Register, email confirmation, login support through Supabase, forgot/reset password, Google OAuth callback support | `/api/account/*` |
| Profile | Personal profile, renter/landlord lifestyle onboarding | `/api/profile/*` |
| Rental map/search | Active-post search, text/range/amenity/bounding-box filters, detail and map coordinates | `/api/rental-posts` |
| Rental management | Create, update, archive, moderation, media metadata | `/api/rental-posts/*`, `/api/admin/moderation/*` |
| Reviews | One review per user/post, rating summary, community comments used by AI ranking | `/api/rental-posts/{id}/reviews` |
| Saved posts | Save/remove/list and roommate candidate unlock | `/api/saved-posts/*` |
| Roommates | Invitation lifecycle; accepting creates a private two-person conversation | `/api/roommate-invitations/*`, `/api/roommate-chats/*` |
| Viewing appointments | Renter request/cancel; post owner confirm/reject; persistent notifications | `/api/viewing-appointments/*` |
| Marketplace | Create/update/sold/archive; location, radius and rental-post-context discovery | `/api/marketplace-posts/*` |
| Trust and safety | Reports, rule-based moderation, admin approve/reject | `/api/reports`, `/api/admin/moderation/*` |
| Landlord verification | Landlord submission; admin queue and approve/reject; verified profile tag state | `/api/landlord-verifications/*`, `/api/admin/landlord-verifications/*` |
| Notifications | REST inbox/read state plus authenticated SignalR push | `/api/notifications`, `/hubs/notifications` |
| Activity history | Privacy-safe history for successful authenticated mutations; request bodies are not stored | `/api/activities` |
| Premium and promotion | Premium subscription/tag, promotion eligibility and ranking | `/api/subscriptions/*` |
| Payments | MoMo and PayOS initiation/callback/webhook flows | `/api/payments/*` |
| AI search | Gemini NLP parsing into allow-listed filters, validated DB query, review-aware scoring and highlight reasons | `/api/ai/*` |
| AI chatbot | Persistent popup conversations backed by Gemini | `/api/chatbot/*` |
| Owner statistics | Per-owner post/view/save totals | `/api/rental-posts/mine/stats` |

## Frontend or provider responsibilities

These items do not require another backend business module:

- GPS permission, map movement, pin rendering and the `AI bảo thế đó` visual effect are frontend/map-SDK behavior. The API already returns coordinates and AI-ranked results.
- Voice capture and speech playback are frontend/device or speech-provider integrations. Text produced by speech-to-text can be sent to the existing AI search/chatbot endpoints.
- Google sign-in UI starts with Supabase OAuth; the API validates the resulting Supabase JWT.
- Chatbot popup placement, loading/error states and filter-panel synchronization are frontend behavior.
- Upload bytes go directly to the configured media provider; the API stores validated HTTPS media metadata.

## Operational verification

- EF Core model has no pending changes after migration `CompleteFeatureAudit`.
- Swagger exposes the REST surface without URL version prefixes.
- `/health/ready` checks the Supabase PostgreSQL connection.
- SignalR clients connect to `/hubs/notifications` with the Supabase access token through `accessTokenFactory` and listen for `notificationReceived`.
