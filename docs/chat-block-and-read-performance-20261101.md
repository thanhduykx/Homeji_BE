# Message blocking and lighter history reads

`PUT /api/conversations/{id}/block` blocks the other participant across all post conversations; `DELETE` removes only the authenticated caller's own block. Both return the resulting block state. Membership authorization runs before every change. Inbox DTOs add optional-compatible `IsBlockedByMe` and `IsBlockedByOther` fields. Existing history and attachment authorization remain available.

New conversations, text/GIF, images, voice messages and new/accepted voice calls reject blocked pairs. A dedicated table uses a directional composite primary key, reverse lookup index, foreign keys, RLS and revoked browser-role grants. The migration adds a message-insert trigger plus a shared per-pair transaction advisory lock for block changes, so a message already being prepared cannot bypass a completed block. Existing messages are preserved. The trigger rejection maps to the same 403 message as application validation. Blocking does not erase history or recall delivered messages; it does not terminate an already established peer-to-peer call.

History now projects attachment metadata rather than selecting attachment `Content` bytes. Binary content is fetched through the existing authorized content endpoint. Unread counts aggregate and apply each participant's read cutoff in PostgreSQL instead of transferring every incoming message timestamp into application memory. No personalized history cache was introduced. History remains unpaginated; very long text-only conversations can still require a separate pagination change.

Validation: release solution build has zero warnings/errors; 55 conversation/message/voice domain-service tests passed, and 12 selected API/query tests passed with one opt-in database test initially skipped. An isolated PostgreSQL run then applied the complete migration chain and passed nine service tests plus five query/database tests, including block persistence, own-only unblock, cross-thread enforcement, a prepared-send race, RLS and history/unread correctness. No real production conversations were sent or blocked for testing.

The metadata query regression test failed before the query fix because SQL selected `Content`, then passed after projection. A local 2 MiB attachment fixture returned roughly 5 KiB of JSON metadata. These fixture results do not establish production latency.

Run the focused disposable database suite with:

```powershell
./scripts/quality/Test-LocalBackend.ps1 -SkipLoadTest -TestFilter 'FullyQualifiedName~ConversationDatabaseTests|FullyQualifiedName~ConversationReadQueryTests|FullyQualifiedName~PostConversationServiceTests'
```
