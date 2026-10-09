# Local AI integration — 2026-10-09

Integrated origin/main 20bbdc4 into integrate/local-ai-20261009.
Local main remains at 7239c9a. No push was performed.
Original uncommitted work is preserved by commit ddabd5d.

Conflict decisions:
- Keep main AI search and intent DTOs, grounding, telemetry, public listing visibility, chatbot consent/support logic and current model snapshot.
- Keep the main SearchCriteriaJson migration and delete-history endpoint. Remove the duplicate local SearchIntentJson migration and history controller/store from the merged tree; originals remain available in ddabd5d. No database migration was executed. If the local search_intent_json migration was previously applied to a database, reconcile that database's migration history separately before deployment.
- Keep local rental decision/cost/draft/admin endpoints and Google commute implementation; register their services.
- Adapt local Gemini parsing to BudgetBasis, ExcludeRoommateShare and PRIVATE_BATHROOM contracts.
- Apply main RentalPostVisibility rules to the new comparison response and route origins.
- Keep local calculator tests; search/intent assertions use the broader current-main suites rather than the superseded local Fits/EvaluateFit contract (including asking for clarification on “rẻ hơn”).

Validation:
- dotnet test Homeji.sln: 317 unit tests and 91 API tests passed; 12 PostgreSQL tests skipped because their database prerequisites are unavailable.
- git diff --check: passed.
- No database schema was changed.