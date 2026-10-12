# Preserve budget uncertainty in AI search

Tracking: [BE #14](https://github.com/thanhduykx/Homeji_BE/issues/14). Related independent transport work: [FE #18](https://github.com/fogit-hd/EXE101_Homeji/issues/18).

Legacy source prices at zero or below could pass a max-only budget filter and receive a price-fit explanation. AI search now requires a positive source rent whenever either budget bound exists. Missing rent without a budget remains discoverable, but receives no price-match explanation. The application enforces this even if a repository returns an unfiltered candidate.

Explicit client criteria could set a total budget while omitting feeUnits from Unknown, bypassing the existing frontend's fee-confirmation flow. Normalization now always preserves fee uncertainty for a total budget because source listings have no evidence that fees are complete. Other unknown constraints remain intact. This intentionally makes previously overconfident total-budget responses require confirmation.

Verification: ten new regression cases, seven failing before the fix; 35 focused tests, 392 unit tests and 107 API tests pass. Thirteen opt-in database tests are skipped. No schema migration or database writes are required. Routes and long-term history remain disabled.

This patch does not resolve per-person versus whole-room budget semantics or hydrate source owner roles in AI search. Those are separate remaining work: ask for an explicit unit when ambiguous and compare compatible source/request units without multiplying or dividing by occupancy. Draft semantic grounding also remains prompt-based; the presence of a preview and confirmation does not establish that arbitrary generated claims are supported.
