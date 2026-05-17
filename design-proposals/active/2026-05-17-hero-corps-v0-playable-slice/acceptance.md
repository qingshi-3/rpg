# Acceptance

## Product Acceptance

Status: Accepted by user on 2026-05-17.

Required confirmation:

- V0.1 expedition only selects a hero.
- The selected hero brings a default corps.
- Existing animated unit resources under `assets/battle/units/` are mandatory for visible combatants.
- The first playable path is world map -> `出征` -> hero selection -> right-click enemy strategic location -> world travel -> arrived assault choice -> deployment -> `开战` -> real-time battle.
- Light RTS command UI is deferred until after this playable slice.

## Technical Acceptance

Status: Implemented; awaiting user playtest acceptance.

Acceptance targets after implementation:

- [x] The playable path can be launched from the existing world map.
- [x] The deployment/start-battle flow records enough facts for deterministic regression tests.
- [x] Existing regression projects still pass.
- [x] New tests cover the v0.1 product path without relying on placeholder unit shapes.

## Merge Acceptance

Status: Not started.

After implementation and user acceptance, merge the expected gameplay copies into accepted authority and archive this proposal.
