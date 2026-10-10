# Unsave unavailable rental posts

Tracking: [BE #9](https://github.com/thanhduykx/Homeji_BE/issues/9); related [FE #10](https://github.com/fogit-hd/EXE101_Homeji/issues/10). Independent releases; no API/schema change.

Unsave previously required an Active rental post before removing the authenticated renter's own saved preference. Archiving, renting or deleting a post therefore prevented removal. Unsave now loads the optional post without media, removes only the current user's preference and decrements its save count only when a removal actually happened and the post exists. New saves still require an Active post.

Tests reproduced three failing unavailable-post cases before the fix. Afterward 332 application unit tests passed, with no skips. API integration: 92 passed, 13 PostgreSQL-dependent tests skipped. Tests include repeated removal, other-user isolation, missing posts, Archived/Rented posts, count updates and preserving the new-save guard.

TRX evidence remains local in `output/quality/unsave-unavailable/`. No migration or real database write was performed. Unit tests establish application behavior, not database-level concurrent save-count guarantees.
