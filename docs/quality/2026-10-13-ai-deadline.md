# Gemini total request deadline — 2026-10-13

Issue: https://github.com/thanhduykx/Homeji_BE/issues/21 (frontend fogit-hd/EXE101_Homeji#26).

Both Gemini clients now share a linked cancellation deadline across attempts, response body reads and retry delays. TimeoutSeconds is capped at 30 seconds for the complete provider sequence (minimum 5 seconds); ordinary caller cancellation is propagated. Existing service fallback behavior remains in place. The frontend allows 40 seconds for AI requests, retaining the 20-second default for other requests.

Before the fix, both deadline tests ran through three slow transient responses (about nine seconds despite a five-second configured budget) and failed. After the fix, each sequence cancels in about five seconds during attempt two. All 14 Gemini infrastructure tests passed. These tests use local handlers and do not claim provider uptime.

No credentials, schema, Routes rollout or history storage settings were changed. A provider outage or a cold Render instance may still cause an error or fallback response.
