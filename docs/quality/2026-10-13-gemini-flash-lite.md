# Gemini Flash-Lite switch — 2026-10-13

The user requested Gemini 3.5 Flash-Lite. The default endpoint in GeminiOptions and appsettings.json now uses gemini-3.5-flash-lite:generateContent. The existing key, retry policy, Routes setting and long-term history setting remain unchanged.

Official model reference: https://ai.google.dev/gemini-api/docs/models/gemini-3.5-flash-lite

Before this change, an authenticated production question about HTTP/HTTPS produced the fallback response. Render logs at 20:38:22–20:38:26 Vietnam time showed GeminiChatbotClient returning 503 on all three attempts. This verifies the current failure; it does not establish that the key is invalid. A separate earlier query hit the frontend timeout. Frontend 20-second timeout versus provider 30-second timeout/retries remains a follow-up concern.

Validation: all 12 Gemini chatbot/parser infrastructure tests passed. These use HTTP handlers and verify the application contract, not provider availability. The Render environment override must also be updated and a real chatbot answer verified before claiming recovery.

Production verification: backend commit 16d0b32 reached Live. At 20:55:06 Vietnam time, Render logs showed a request to gemini-3.5-flash-lite:generateContent; the original Homeji frontend displayed the generated HTTP/HTTPS explanation instead of the timeout/fallback. Existing credentials were retained. All 88 focused chatbot/AI-search application tests also passed after the deadline change. Evidence: C:/EXE101_Homeji/output/quality/full-audit/gemini-flash-lite-production.png. This is one successful live check, not a provider-uptime guarantee.
