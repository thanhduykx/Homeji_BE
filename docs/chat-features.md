# Messaging: voice, GIFs, public profiles and Premium

Voice calls are audio-only and available to both free and Premium accounts. The authenticated `/hubs/voice` hub resolves the recipient from conversation membership. It never accepts an arbitrary target user ID. One call can reserve an account at a time, only the recipient may answer, one device claims the answer, and SDP/ICE is routed only between the two selected connections. Ringing expires after 45 seconds; calls last at most 60 minutes. Initiation and signaling are bounded. Disconnecting releases the reservation. State is in memory for the current single API instance; move this registry to an atomic shared store and configure SignalR scale-out before running multiple replicas.

The client requests the microphone only after calling or accepting. It stops tracks on rejection, timeout, disconnect, cancellation, logout and unmount, including a microphone permission request that resolves after cancellation. Connections use relay-only ICE so peers do not receive each other's IP addresses. No recording is performed. Both people must have Homeji open; there is no background push or native phone call integration.

## Activate external services

### Metered free plan

Set `VoiceCalls:Provider=Metered`, `VoiceCalls:Metered:AppName` to the application subdomain (without `.metered.live`) and `VoiceCalls:Metered:ApiKey` to the **credential-scoped API key** from a dedicated Homeji TURN credential. Do not use the account Secret Key. The backend requests the current ICE array from the documented Get Credential API, validates provider hosts and returns only relay/STUN endpoints plus the actual client credentials. It does not expose the credential API key itself. Metered accepts a single URL string or an array; Homeji normalizes both.

Run `pwsh ./scripts/Configure-MeteredTurn.ps1 -AppName <your-app>` with the credential key in `METERED_TURN_CREDENTIAL_API_KEY`. The script checks real credential retrieval before saving local settings and ignored hosting variables/relay verification credentials. Free-plan credentials may be shared and long-lived; Homeji does not claim they expire per call. Configure expiry/rotation when supported by the account and rotate or revoke this dedicated credential if exposed. New Metered credentials may require up to two minutes to propagate before relay allocations succeed.

The selected free plan currently advertises 500 MB/month without a card. Do not enable a paid plan, automatic top-up or overage billing while setting up the free option. Verify the actual plan on the dashboard and test a real relay allocation and traffic before calling setup complete.

### Managed Cloudflare TURN

The backend also supports `VoiceCalls:Provider=Cloudflare`. Use the Cloudflare Realtime TURN dashboard to create a dedicated Homeji TURN key. Only the backend stores `VoiceCalls:Cloudflare:KeyId` and `VoiceCalls:Cloudflare:ApiToken`; these are administrative credentials, never browser credentials. Set the token in `CLOUDFLARE_TURN_API_TOKEN` and run `pwsh ./scripts/Configure-ManagedTurn.ps1 -KeyId <your-turn-key-id>`. The script verifies credential issuance before changing local configuration, preserves unrelated settings and writes ignored production variables plus temporary verification credentials under `output/voice-deployment/`. It does not activate billing or deploy the backend.

Cloudflare credentials are requested per authenticated user via the fixed Cloudflare TURN API with a 7,200-second TTL. The browser receives the returned temporary ICE credentials, including TCP/TLS alternatives, and keeps relay-only WebRTC. Provider/network failures return a retryable 503 without leaking API tokens. The same authenticated/rate-limited API route serves either Cloudflare or coturn.

Cloudflare currently lists a shared Realtime allowance of 1,000 GB/month and charges for additional egress. A free allowance is not a spending cap: review the activation screen before enabling any metered subscription. Actual relay allocation and data/audio traffic must be tested separately from successful API credential issuance.

### Self-hosted coturn

The code deliberately reports unavailable when services are not configured. No borrowed public API keys or unverified shared TURN credentials are bundled.

1. Sign in at <https://developers.giphy.com/dashboard/> and create an API key for Homeji. Account terms must be accepted by the owner. Production limits/approval are controlled by GIPHY.
2. Use a public Linux VPS with a domain pointing to its IP. Open TCP/UDP 3478 and UDP 49160–49260. The SPA's Render HTTP service cannot substitute for the UDP relay.
3. Supply your key through the `GIPHY_API_KEY` environment variable and run `pwsh ./scripts/Configure-ChatServices.ps1 -TurnHost <your-domain> -PublicIp <your-public-ip>`. This generates a cryptographic secret, merges development settings without removing other settings, and writes ignored deployment files under `output/voice-deployment/`. It does not deploy or open firewall ports. Keep the generated files private.
4. Copy `compose.yaml` and `turnserver.conf` to the VPS and run `docker compose up -d`. Pin `COTURN_IMAGE` to your verified image digest when deploying. The provided base configuration supports UDP and TCP TURN on 3478. Add a trusted certificate and TURN/TLS listener on 5349 or 443 for networks that block these ports; do not expose a TLS URL without a matching listener and certificate.
5. Apply the variables in generated `backend-environment.private.json` to the API hosting environment. These are `Giphy__ApiKey`, `VoiceCalls__TurnUrls__0`, `VoiceCalls__TurnUrls__1`, and `VoiceCalls__TurnSecret`. Never expose the TURN shared secret in frontend variables. `Giphy__ApiKey` must be a browser API key: it is deliberately supplied to signed-in clients by `/api/chat/gif-config`, because GIPHY requires direct browser requests. Keep any GIPHY account/admin credentials separate. Redeploy backend and frontend together.
6. Verify two real accounts on different networks, including Wi-Fi to mobile data: ring, accept, audio in both directions, mute, reject, timeout, disconnect, another tab, and microphone-denied states. A successful build or fixture is not proof of real relay audio.

`GET /api/chat/voice-config` returns temporary coturn HMAC credentials with a two-hour expiry and `Cache-Control: private, no-store`. HMAC-SHA1 is required by coturn's TURN REST credential protocol; the analyzer suppression is local to that operation.

## GIF and profile contracts

GIF search calls GIPHY's search/trending API directly from the browser with `rating=g`, a 24-item limit and a 50-character query limit. The client debounces and cancels searches and displays attribution without reordering results. Search text is sent to GIPHY as entered. The server validates conversation membership and the GIF ID before sending a message. The message body persists only an ID in a versioned `homeji:gif:v1:` JSON payload, never a media URL. Each viewer resolves that ID directly with GIPHY and checks the current rating and HTTPS media host before displaying it. Deleted or non-general content gets an unavailable state. No provider responses, URLs or media files are persistently cached. This avoids a database migration and preserves the existing message send/notification transaction. Old clients display the payload as text; deploy the UI with the API. GIPHY receives GIF search terms, GIF IDs and browser IP addresses; `no-referrer` avoids sending a Homeji page URL.

`GET /api/profile/{userId}` requires authentication and returns only ID, display name, avatar, membership date and current Premium status. It excludes email, phone, address, school, rental needs, budget and lifestyle information. The conversation list batches active Premium subscriptions in one query, using current backend time, rather than performing one entitlement query per row. Premium decoration is membership branding, not identity or landlord verification.

## Verification boundary

### Live setup checked on 2026-10-30 (Asia/Saigon)

A dedicated GIPHY API application named `Homeji Web Chat` was created with the owner's explicit terms approval. Its browser key is stored in ignored local backend configuration and an ignored environment handoff file, not in tracked source. The actual Homeji `giphyClient` returned 24 search results, resolved a selected ID again, and loaded its real media in Edge. The check used a local config middleware; it did not assert a production session or send a production message.

Metered workspace `homeji-chat` was configured with its Free Trial selection (500 MB, no payment method added) and dedicated `Homeji voice calls` credential after explicit approval. The settled TURN dashboard confirmed `$0/mo`, 500 MB, auto-reload off and a hard stop at quota exhaustion. It separately showed 39.9 KB against `Homeji voice calls` after the relay test; rounded headline totals were still zero. The actual backend `VoiceIceService` contacted Metered successfully and returned five ICE server entries. Two browser RTCPeerConnections with `iceTransportPolicy: relay` established a real connection, exchanged an echoed data-channel message, and received nonzero generated audio bytes in both directions. These are two peers in one browser, not two real users on different networks, and no microphone was accessed. Configuration scripts keep provider keys and ICE test credentials in ignored private files. Production hosting variables and production deployment remain separate from this local setup verification.

Sanitized verification records and screenshots are under the frontend workspace's `output/verification/chat-services/`. No GIPHY provider response/media URLs are stored in those records. The saved temporary TURN client credentials stay in the backend's ignored `output/voice-deployment/` directory.

Unit checks cover registry concurrency, answer ownership, expiration/disconnect cleanup, flood limits, public profile field exclusion and malicious GIF payload URLs. Frontend browser fixtures can demonstrate layout and interaction, but cannot prove GIPHY credentials, TURN connectivity, production deployment or real microphone audio.

References: [GIPHY API](https://developers.giphy.com/docs/api/), [WebRTC TURN](https://webrtc.org/getting-started/turn-server), [coturn configuration](https://github.com/coturn/coturn/blob/master/docker/coturn/turnserver.conf).

Managed TURN references: [credential generation](https://developers.cloudflare.com/realtime/turn/generate-credentials/), [pricing and free allowance](https://developers.cloudflare.com/realtime/turn/faq/).

Metered references: [Get Credential](https://www.metered.ca/docs/turn-rest-api/get-credential/), [credential propagation and expiry](https://www.metered.ca/docs/turn-rest-api/post-create-credential/), [free plan](https://www.metered.ca/pricing).


## Recorded voice messages (2026-10-30)

`POST /api/conversations/{id}/messages/voice` accepts a multipart `file`. Authentication, conversation membership and the costly-operation rate limiter apply. The application validates canonical RIFF/WAV PCM bytes (mono, 16 kHz, signed 16-bit, 0.3–120 seconds), exact header/data lengths and a maximum of 3,840,044 bytes. Extra chunks, trailing data and spoofed MIME declarations cannot bypass validation. The format contains only fixed headers and PCM samples, with no ancillary metadata or external media references. A PCM recording can contain any sample values; this validation does not assert that a human spoke.

Voice and image attachments share the existing 30-files/24-hour quota. Audio uses `audio/wav`, `Other` context and zero image dimensions in the existing private attachment table; no schema migration is required. Content download still checks conversation membership, and deletion still checks the uploader and clears stored bytes. No recording is uploaded before the user presses **Gửi âm thoại**.

The frontend captures audio in a same-origin AudioWorklet, resamples to 16 kHz off the UI thread and caps sample storage at two minutes. It stops microphone tracks and closes audio contexts before preview, on cancellation, thread changes, incoming/outgoing call activation and unmount. Permission responses and upload completions from an old conversation cannot alter the new thread. Failed uploads preserve the local preview for retry. WAV avoids a new media decoder dependency; the tradeoff is about 3.8 MB for two minutes, larger than compressed Opus. Calls continue to use TURN; recorded messages use authenticated HTTP uploads and do not consume TURN relay quota.

Local generated-audio browser verification exercised record → preview → upload → private attachment playback (21.16 seconds). This proves the browser capture/playback pipeline and fixture UI, not a real microphone conversation or a production message send. Focused tests cover malformed headers, duration limits, authorization/quota rejection, deletion ownership, resampling, late permissions, stale upload responses, retry and cleanup.
