# Frontend domain migration — 17/10/2026

Production frontend: https://homeji.onrender.com. Backend remains https://homeji-be.onrender.com; Supabase project remains pzxxzcrgpfsbdkyrdvng. Historical quality reports retain their original observed URLs; they are not current deployment instructions.

## Configuration

- FE: Render service name `homeji`; same-origin `/api` and `/hubs` continue to proxy to the existing backend. Signup and recovery already derive callback URLs from `window.location.origin`; internal routes, API paths, SignalR and storage URLs need no hostname rewrite. Maps diagnosis now points at the new domain.
- BE: signup/recovery/Google defaults, payment frontend return URL, and CORS production origin moved to the new hostname. Render runtime overrides and Blueprint use the same values. MoMo/PayOS backend callbacks and webhook destinations remain on the backend domain.
- Supabase Auth: Site URL is the new frontend. Exact allowed redirects are `/auth/callback` and `/reset-password` on that origin. Old frontend callback entries were removed; verified again after reload. Default Supabase email templates are in use; application account emails use the existing Brevo integration and generated callback links. No schema/data migration is needed. No Edge Functions exist. The existing active cron liveness job targets the unchanged backend.
- Google Cloud: existing OAuth client now authorizes the new frontend plus localhost. Supabase provider callback is unchanged. App home/privacy/terms links were updated; old frontend authorized-domain entries removed. Maps key has no website restriction, so the domain move requires no key change; existing API restrictions were preserved.

## Evidence

- FE production build passed. 14 focused proxy/Google configuration/SignalR session tests passed.
- BE build and 55 focused API-boundary/account-email tests passed, including actual middleware CORS preflight and all provider-return redirect assertions.
- Live HTTP probes: root, login, register, auth callback, reset-password, payments, privacy, terms return HTML 200. Packages and rental listing APIs return JSON 200 through the new frontend; private profile returns 401 without a token.
- Live BE liveness and database readiness return 200. Google URL endpoint returns `redirect_to=https://homeji.onrender.com/auth/callback`. Three payment-return routes respond 302 to the new frontend. CORS allows the new origin and omits allow-origin for an untrusted origin.
- Live FE entry `/assets/index-C5pp3TBP.js` and map-diagnosis chunk `MapErrorPanel-DMVeSwux.js` contain no old frontend origin; the latter includes the new domain.
- Authenticated production check: the existing `/api/account/google/redirect` flow successfully returned through Supabase to `https://homeji.onrender.com/` with a signed-in greeting. Reload preserved the session. Observed same-origin responses: profile, notifications, conversations, activities, viewing appointments, marketplace posts, saved posts, rental posts, roommate invitations and SignalR negotiation all 200. Google Maps JavaScript/modules loaded 200. No token, private response body or account identifier was saved in the verification JSON.
- Code releases: FE `3bc21f3`, BE `e146ab7`. BE deploy `dep-db9ovu8jgiss738vj040` reached Live after runtime configuration was saved.

Local read-only probe results and screenshots: `C:/EXE101_Homeji/output/verification/domain-migration/`.

## Verification limit

No real payment/order, signup, or password reset was executed. The existing backend/Supabase Google redirect flow and authenticated API calls are verified end to end. The separate Google Identity Services popup button remains unverified: account selection is visible with the new legal URLs, but automated interaction with the popup does not finish. An earlier attempt still showed origin_mismatch after account selection; current saved origin is correct and the frontend SDK has been reloaded. No successful production `/api/account/google/id-token` exchange has been captured. Keep the overall migration goal active until the primary popup login is confirmed.
