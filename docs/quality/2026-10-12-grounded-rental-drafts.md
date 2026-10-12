# Ground rental drafts in source facts

Tracking: [BE #15](https://github.com/thanhduykx/Homeji_BE/issues/15). Requirement: AI product opportunities item 7.

Previously, any valid title/description JSON from Gemini was accepted, including unsupported statements. The provider now selects title/section variant IDs and section order from a bounded catalog. The application validates the complete selection and renders all text from normalized owner inputs. It rejects unknown, missing or repeated IDs, duplicate JSON properties, extra prose/properties, invalid shapes and oversized output. Every description includes source attribution and a final reminder to confirm room status, fees, deposit and contract. An unknown price unit remains explicit; the amount is never divided, multiplied or rounded to another unit.

The prompt includes IDs, availability flags and the declared price basis, without raw address/type/amenity strings. Those owner strings remain quoted data in the rendered response. Control, whitespace and Unicode format characters are normalized before missing-data checks; quote delimiters cannot close the attribution. This prevents generated claims being added, but does not independently verify owner claims. React renders these strings as text.

The public request and Title/Description/Missing response are compatible. Titles remain at most 200 characters and descriptions at most 4000. Optional title summary fields may be omitted for long inputs; descriptions retain all facts. Provider failure still produces an error, with no substitute draft or automatic save/publication. The tradeoff is constrained phrasing rather than arbitrary literary prose; Gemini still selects actual presentation variants and order.

Verification: 411 application unit tests and 109 API tests pass; 13 opt-in database tests skipped. Behavioral coverage includes alternate plans, unsupported prose, missing/duplicate/invalid references, injected source strings, invisible Unicode, maximum-size inputs, fractional amounts, each price basis, malformed output and provider failure. Real-service API tests preserve external DTO compatibility and reject malicious valid provider JSON with 503. These tests use provider doubles, not live Gemini. No migration or database write is required.

Advanced Routes and long-term history remain disabled. Production target: https://homeji-be.onrender.com/.
