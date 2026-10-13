# Room transfer expiry — 2026-10-13

Issue: https://github.com/thanhduykx/Homeji_BE/issues/20

Confirmed policy: a transfer remains available through the end of its original lease-end date in Vietnam (UTC+7). It expires when the next Vietnam calendar date starts. Missing legacy lease dates remain unknown; ordinary rentals and roommate shares are unaffected.

A shared availability policy guards public detail, search, AI results, comparisons, new saves, viewing requests, listing conversations and roommate invitations. The repository applies the date predicate before pagination. Submission and admin approval reject expired transfers before changing status or writing notifications. Owners and admins retain management visibility; existing saved preferences and conversation/appointment history are preserved.

Validation: 457 application unit tests and 127 API/infrastructure tests passed; 13 opt-in database tests were skipped. Coverage includes yesterday/today/tomorrow, null dates, non-transfer types, the 17:00 UTC Vietnam midnight boundary, translated SQL before LIMIT, read/action guards, and submission/approval without mutation on failure. Publication tests were rerun after simplifying a write-count assertion (6 passed).

No migration or database mutation is required. Before deployment, the real public endpoint returned three transfers and no expired example on 2026-10-13; therefore this dataset cannot itself prove exclusion of an expired record. Synthetic expiry cases and SQL translation cover that boundary without inserting production data. Production health and query execution will be checked after deployment.
