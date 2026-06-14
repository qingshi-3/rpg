# Strategic Management System Architecture

## Metadata

| Field | Value |
|---|---|
| Requirement id | REQ-STRAT-ARCH-001 |
| Status | Accepted and merged into authority documents |
| Parent proposal | `design-proposals/archived/2026-06-13-city-corps-muster-economy` |
| Supersedes | Strategic-management authority in `system-design/strategic-world-runtime-architecture.md` and `system-design/world-site-management-architecture.md` |
| Superseded by | None |
| Amends | None |
| Amended by | None |
| Related implementation proposal | None yet |
| Affected authority documents | `system-design/README.md`; `system-design/strategic-management-system-architecture.md`; `system-design/strategic-world-runtime-architecture.md`; `system-design/world-site-management-architecture.md` |

## Current Architecture

The current accepted system documents split strategic responsibilities across Strategic World Runtime and World Site Management. Those documents were sufficient for the first slice but now mix world tick, site actions, facilities, garrison, army movement, and battle-ready flows into legacy strategic-world concepts.

## Accepted Direction

The new strategic management system is a clean strategic-layer rebuild. It owns strategic management content, persistent state, rule evaluation, command execution, and presentation view models. It must not extend the legacy WorldAction, WorldSite, Garrison, or first-slice definition factory as long-term strategic-management authority.

Battle bridge contracts are deliberately deferred to a separate proposal because they may require battle-side contract changes. This proposal records only the strategic-management-side boundary: strategic management interacts with battle only through future bridge snapshots/results and never through Runtime state.

## Merge Notes

The expected copies were merged into `system-design/`. Implementation must start from a focused implementation proposal under `gameplay-alignment/implementation-proposals/`.
