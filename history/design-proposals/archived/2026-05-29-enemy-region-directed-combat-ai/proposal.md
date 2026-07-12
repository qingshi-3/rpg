# Enemy Region-Directed Combat AI Design Proposal

Status: Archived
Created: 2026-05-29
Accepted: 2026-05-29
Merged: 2026-05-29
Archived: 2026-05-29

Requirement Id: battle-ai-enemy-region-directed-combat-v1
Parent Proposal: none
Supersedes: none
Superseded By:
Amends: design-proposals/archived/2026-05-27-local-combat-situation-ai; design-proposals/archived/2026-05-23-battle-plan-state-machine
Amended By:
Affected Authority Documents:
- gameplay-design/content-systems-long-term-design.md
- gameplay-design/details/combat-command/README.md
- system-design/README.md
- system-design/hero-led-light-rts-system-architecture.md
- system-design/battle-ai-boundary-architecture.md
- system-design/battle-runtime-architecture.md
- system-design/world-battle-entry-architecture.md
- system-design/battle-group-tactical-region-architecture.md
Related Implementation Proposals: gameplay-alignment/implementation-proposals/2026-05-29-enemy-region-directed-combat-ai.md

## Reason

Current accepted AI authority describes local combat response, but it does not clearly separate battle-group-owned region intent from local combat target selection. Existing enemy behavior can read as unintelligent because an enemy group may keep following an initial objective plan or a selected target instead of using a stable region-directed movement model while out of combat and a bounded local-optimal combat model while engaged.

This proposal corrects the authority chain before implementation. It establishes that target regions, temporary regions, local combat regions, and engagement state are owned by individual battle groups. Global structures may cache region snapshots by battle-group id, but they must not become a global tactical director or mutate group intent.

## Affected Authority Documents

- `gameplay-design/content-systems-long-term-design.md`: player-facing enemy region-directed combat expectation and player-command boundary.
- `gameplay-design/details/combat-command/README.md`: command and engagement rules for region-directed movement versus local combat.
- `system-design/README.md`: accepted document index for the new tactical-region authority.
- `system-design/hero-led-light-rts-system-architecture.md`: routing entry for tactical regions and group engagement state.
- `system-design/battle-ai-boundary-architecture.md`: AI ownership, player/enemy policy separation, and local-optimal combat scope.
- `system-design/battle-runtime-architecture.md`: Runtime-owned group tactical state and region snapshot rules.
- `system-design/world-battle-entry-architecture.md`: battle entry obligations for initial enemy tactical mode and region facts.
- `system-design/battle-group-tactical-region-architecture.md`: new focused authority for battle-group-owned regions and engagement state.

## Current Design Or Architecture

- Battle preparation records objective zones and engagement rules, but enemy tactical mode and dynamic region-selection rules are not explicit authority.
- `LocalCombatSituation` authority exists, but current documents do not make it unambiguous that local combat regions are battle-group-owned observations rather than global combat directors.
- The accepted AI boundary permits local combat response, but it does not define how non-engaged enemy groups select or reselect fixed/temporary regions.
- Player and enemy autonomy use the same Runtime facts, but the documents do not clearly separate reusable tactical solvers from enemy-only strategy policies.
- Temporary regions derived from dispersed player units are not yet an accepted design concept.

## Expected Design Or Architecture

- Non-engaged movement is region-directed: a battle group moves toward a fixed or temporary region, not toward a moving unit target.
- Engaged combat is local-optimal: a battle group builds a bounded local combat region from its members' perception coverage, then chooses targets, attack slots, support slots, and degradation within that region.
- Enemy groups own enemy tactical policies: offense, active defense, and hold defense. Hold defenders switch to active assault when any member is damaged or when the group perceives a player unit. V1 does not require return-to-defense after activation.
- Player groups may reuse perception, local combat regions, target/slot solvers, and Runtime validation, but player target regions and engagement posture remain controlled by player commands or accepted player battle plans.
- Target regions, temporary regions, local combat regions, and engagement state are battle-group-owned. Runtime/global caches may index snapshots by battle-group id for query, diagnostics, and performance, but the cache does not own intent or mutate group state.
- Temporary player-cluster regions are generated only when fixed target regions contain no relevant opposing units. They are cached and refreshed at a configurable interval, defaulting to 5 Runtime ticks.

## Follow-Up Implementation Scope

Implementation must be split after this design proposal is accepted, merged, and archived. Likely implementation slices:

1. Add battle-group tactical state and diagnostics without behavior changes.
2. Add enemy initial tactical mode and fixed-region target selection.
3. Add group-level engagement enter/exit state from decoupled perception facts.
4. Add bounded local combat region building and local-optimal target/slot solving.
5. Add temporary player-cluster regions with configurable refresh interval.
6. Add tests, diagnostics, and manual QA for enemy offense, active defense, hold-defense activation, and player-command non-overwrite.

## Acceptance

This design proposal is acceptable when:

- all expected authority copies state that tactical regions are owned by battle groups, not global battle state;
- enemy-only policy is separated from reusable perception, region, local-combat, and Runtime validation systems;
- player-command authority is preserved and cannot be overwritten by enemy region policy;
- non-engaged movement is region-directed and engaged behavior is local-optimal within a bounded combat region;
- temporary region generation, owner, cache behavior, and default refresh interval are documented;
- the merge plan maps every expected copy to its final authority path.

## Merge Plan

- `expected/gameplay-design/content-systems-long-term-design.md` -> `gameplay-design/content-systems-long-term-design.md`
- `expected/gameplay-design/details/combat-command/README.md` -> `gameplay-design/details/combat-command/README.md`
- `expected/system-design/README.md` -> `system-design/README.md`
- `expected/system-design/hero-led-light-rts-system-architecture.md` -> `system-design/hero-led-light-rts-system-architecture.md`
- `expected/system-design/battle-ai-boundary-architecture.md` -> `system-design/battle-ai-boundary-architecture.md`
- `expected/system-design/battle-runtime-architecture.md` -> `system-design/battle-runtime-architecture.md`
- `expected/system-design/world-battle-entry-architecture.md` -> `system-design/world-battle-entry-architecture.md`
- `expected/system-design/battle-group-tactical-region-architecture.md` -> `system-design/battle-group-tactical-region-architecture.md`
