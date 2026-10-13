# AI candidate recall — 2026-10-13

Issue: https://github.com/thanhduykx/Homeji_BE/issues/22

AI search previously read only the first 100 structurally eligible listings, then matched normalized Vietnamese text. An older valid street/title match could disappear from results. A regression with a matching 101st listing failed before this change.

Search now reads pages of 100 through the existing structured repository query and retains only the global best requested results (maximum five). Hard constraints, accent normalization, expiry and commercial-score separation are preserved. Premium enrichment runs once for winning owners. Ordering already has an ID tie-breaker. Caller cancellation stops the scan rather than returning an apparent complete partial result.

Validation: all 461 application unit tests passed. New cases verify the 101st street match, older higher-fit result across pages, unchanged budget/exclusions, exact-full-page exhaustion and cancellation without partial results. No schema or repository-interface change is required; no production data was inserted for the regression.

Tradeoff: candidate memory is bounded by a page plus the shortlist, but page reads grow with eligible inventory and OFFSET becomes more expensive. This restores correctness without claiming a large-scale performance improvement. Indexed normalized text and keyset retrieval should be considered with measured query/latency evidence if inventory grows. Concurrent listing changes can still shift offset pages; snapshot semantics are not promised.
