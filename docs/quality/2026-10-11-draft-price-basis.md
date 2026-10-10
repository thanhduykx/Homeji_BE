# Explicit rental draft price basis

Tracking: [FE #13](https://github.com/fogit-hd/EXE101_Homeji/issues/13), [BE #11](https://github.com/thanhduykx/Homeji_BE/issues/11).

The draft request now carries optional `priceBasis`: `room`, `person`, or `unknown`. The editor uses the same type/author-role rules as the listing price UI: renter roommate amounts are per person; landlord roommate and whole-room listings are per room. Missing author metadata remains unknown. No amount is converted.

The deterministic draft builder labels known units and asks for confirmation when the unit is missing or unknown. Existing payloads remain valid. The preview snapshot includes all facts, so changing the unit invalidates an old preview. Applying a preview still only changes the form; publishing requires the existing save/submit action.

## Verification on 2026-10-11

- FE: 164/164 Node tests pass; production build and ESLint on changed source/tests pass.
- BE: 353 application unit tests pass; 100 API integration tests pass, 13 opt-in database tests skipped.
- Eight new HTTP cases use real DTO binding/application service with a fake Gemini client. Valid units reach provider facts unchanged; invalid units return HTTP400 before provider invocation.

Backend deployment must precede relying on the field in production. These checks do not prove live Gemini wording compliance or production UI behavior. No database schema changes or writes, credentials, Routes flags, or long-term history flags were involved.
