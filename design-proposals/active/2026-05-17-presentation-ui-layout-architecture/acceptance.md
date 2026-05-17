# Presentation UI Layout Architecture Acceptance

Status: First Phase Implemented

## User Acceptance

- [x] User accepts the expected UI architecture.
- [x] User accepts phased implementation instead of a single large rewrite.
- [x] User accepts that the first implementation batch moves panels left while preserving behavior.

## Architecture Acceptance

- [x] UI remains Presentation-only and does not own long-term state, runtime truth, settlement, rewards, or battle outcome.
- [x] UI mode is presentation state only.
- [x] Layout hosts have clear responsibilities.
- [x] Main persistent panels use left primary workspace.
- [x] Right side is reserved for compact notifications and minimap/navigation aid.
- [x] Battle-preparation UI consumes the same authoritative request/snapshot data as runtime handoff.
- [ ] Future battle command UI submits `CommandRequest`.

## Implementation Acceptance

- [x] Batch 1 complete and verified.
- [x] Batch 2 complete and verified.
- [x] Batch 3 complete and verified.
- [x] Batch 4 complete and verified if needed before business development continues.
- [x] No oversized source files are introduced.
- [x] Existing architecture regression tests pass.

## Merge Acceptance

- [x] `expected/system-design/presentation-ui-layout-architecture.md` has been merged into `system-design/presentation-ui-layout-architecture.md`.
- [x] `system-design/README.md` routes to the new UI architecture document if needed.
- [ ] Proposal is archived after final acceptance.
