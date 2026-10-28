# Gemini configuration and verification

Checked on 11 October 2026 (Asia/Saigon).

## Provider scope

- Dependency injection registers `GeminiSearchTextParser` for `IAiSearchTextParser` and `GeminiChatbotClient` for `IChatbotAiClient`. No other language-model provider is registered.
- Chatbot greetings, general questions and approved Homeji support questions call Gemini. Approved support material grounds the prompt rather than replacing the model response.
- Natural-language search and refinements call Gemini. Its interpreted price, area, location and optional preferences are applied, with explicit user constraints overriding conflicting interpretations. Retrieval, authorization, prices and listing evidence remain application/database responsibilities.
- Rental draft generation calls the same Gemini transport through `RentalDraftService`. Frontend previews the generated title and description and requires explicit application, followed by the normal save/publish action.
- Cost arithmetic, comparison tables, administrative counters and ranking from listing evidence are deterministic calculations, not additional generative-model providers.

## Configuration

- Render service: `Homeji_BE`; endpoint configured to Gemini `gemini-3.5-flash:generateContent`.
- Server-only configuration: API key and endpoint are managed via server secrets. No credentials belong in frontend code or this document.
- Local configuration is in ignored `src/Homeji.Api/appsettings.Local.json`, also excluded from Docker context and publish output.
- Advanced Routes and long-term chatbot history remain disabled. Production UI displayed “Lưu lịch sử hiện đang tắt.” during verification.

## Deployment and validation

- Frontend implementation commit: `efe557d`.
- Backend commits: `f8ffb7f`, `68c1144`, `3452ed1`.
- Render deployment `dep-db56jtou01pc73ec91k0` for `3452ed1` reports `Deploy succeeded | Live`; deployment logs show no migrations were needed.
- Production `/health/live` and `/health/ready` returned HTTP 200 after deployment.
- Application unit tests: 323 passed, zero skipped.
- API integration tests: 92 passed, 13 skipped. Skipped tests are not claimed as verified database coverage.
- Frontend `npm run build` passed; the existing large-chunk warning remains.

## Actual Gemini calls

- Production chatbot explained why rain occurs and answered PayOS instructions using the approved Homeji knowledge.
- Production follow-up “Không cần bếp” retained the 4,000,000 VND ceiling and two occupants, removed the kitchen requirement and returned five listing cards from the database.
- Production rental drafting returned a preview for 3,000,000 VND/month, 25 square metres, Linh Trung and a kitchen. The test did not apply, save or publish the draft.
- The production request after `3452ed1` called Gemini, received a transient 503, retried and received HTTP 200, as recorded in Render at 23:41 on 10 October.
- On 11 October, a smoke harness using the project's actual Gemini clients and ignored local configuration succeeded for all three calls: Vietnamese written-number parsing (3,500,000 VND and minimum 20 square metres), general chatbot response and structured rental draft generation. This harness calls Google directly and is not itself a production API test.

Local evidence: frontend `output/verification/gemini-live-client-smoke.json` and `output/verification/gemini-draft-production.png`. These contain no API key. The smoke harness reads existing server configuration without copying credentials into its source.

## Reliability and remaining verification

Gemini can return transient 503 responses. Clients use bounded retries and report genuine provider failure rather than presenting fabricated listings or fixed templates as model output. Successful checks do not guarantee permanent upstream availability.

The final written-number search UI check after reconnect is pending a production login; the earlier search tab lost its authenticated session. Do not treat the HTTP-200 provider log alone as proof of the rendered criteria.
