# Normal Chat stays advisory

The reported image/selling request entered the posting tool even with `PostingAgent=false`. Four regression cases reproduced this for Basic/Premium and text/photo requests: expected zero posting-tool calls, actual one.

Normal Chat now calls the normal text/media provider, preserving the existing bounded conversation and attachments. Posting-related guidance includes one manual-form action and a short entitlement-aware suggestion: Basic can upgrade to Premium, while Premium can switch to Agent. The normal reply never returns an Agent draft or marks an Agent action as executed. Unsafe generated publication claims are replaced with manual steps. Agent preparation requires the explicit `PostingAgent=true` flag; its server-side Premium and confirmation rules remain in place.

The provider prompt explicitly asks for practical advice, optional copyable listing text and relevant photo analysis rather than a Premium refusal. The server supplies the entitlement suggestion from the subscription repository, avoiding model guesses about membership or price. Manual-roommate links use the actual supported form type.

Validation: 698 Application unit tests passed. Isolated database/provider-transport replay passed 22 tests, with three real-provider tests explicitly skipped. Normal-Chat browser coverage exercises Basic/Premium, photo input, 390 dark/1366 light, a retained Chat selection, no draft workspace and one manual action. The Agent browser flow now explicitly selects Agent before sending and still verifies revision/approval/cancellation/error behavior. Browser APIs/provider responses are mocked; no new real-provider success is claimed.

Compatibility: clients omitting `PostingAgent` now receive advice; clients intending automation must send `PostingAgent=true`. The current frontend already sends that flag only when Agent is selected. Old QA notes describing automatic Chat-to-Agent routing are superseded by this requirement.
