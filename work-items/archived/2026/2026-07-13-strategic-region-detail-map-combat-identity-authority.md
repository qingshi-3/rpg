# Strategic Region, Detail-Map, And Combat-Identity Authority

Status: Completed
Executor: `executor` (`gpt-5.6-sol`, high)
Verifier: Main Agent (independent parent context, with read-only gameplay and architecture reviewers)
Created: 2026-07-13
Updated: 2026-07-13

## Objective

Synchronize the user-confirmed long-term gameplay and architecture direction for strategic-world regions mapping into detailed maps, terrain-driven corps identity, cavalry momentum, and precise player-controlled skill targeting.

## Confirmed Discussion Result

The user confirmed all of the following on 2026-07-13:

1. A city or comparable battle-capable strategic location may contain multiple strategic-world regions. These regions map through stable semantic identities into the location's detailed authored map; the relationship is not a literal pixel-scale copy.
2. The strategic region or approach used by an army can resolve a different detailed-map entrance, attacker deployment area, route context, or other explicit battle-start condition. Strategic geography and detailed-map combat must therefore remain causally connected.
3. Strategic control and battle results use the same location and region identities. The detailed map consumes resolved strategic context and returns consequences through the accepted battle settlement and Strategic Management command path rather than owning campaign state.
4. Corps classes must be differentiated by readable base mechanics that interact with terrain and command decisions, not only by higher movement, attack, defense, or other scalar values.
5. Cavalry's defining baseline is momentum-dependent impact: it must gain its strongest offensive value by moving through sufficient usable space and entering a valid charge-like contact. Tight passages, broken routes, blocking, and terrain that prevents a clean approach naturally constrain cavalry. Exact distance, turning, reset, damage, and counter values remain unconfirmed tuning or later focused design.
6. Precise player-controlled targeting is a product distinction of the skill system. A skill may use unit, cell, direction, area, or multi-stage spatial targeting, and the player chooses the relevant target or placement. The system must not reduce player-cast skills to automatic target selection and release by default.
7. The current first-slice problem is content assignment and tactical identity, not lack of targeting architecture: three hero companies currently share one thunder skill family despite the accepted one-signature-skill first-slice rule. This task records that as implementation evidence but does not repair content or code.

## Authority Impact

This is a large durable gameplay and cross-system contract change. Execution must update:

- global gameplay authority with concise region-to-detail-map, corps-mechanic, cavalry, and precise-skill-targeting rules;
- a focused gameplay-detail route for strategic-region and detailed-map mapping;
- heroes/corps and combat-command detail authority;
- system architecture routing and a focused cross-system mapping contract;
- existing strategic-world authoring, Strategic Management, site-map layout, and Strategic Battle Bridge documents only as needed to establish ownership and stable-id handoff without duplicating full rules.

The existing standalone strategic-region prototype remains reference evidence and does not become production Runtime authority through this documentation task.

## Execution Scope

- Add a focused gameplay detail defining the player-facing relationship between strategic regions and detailed maps.
- Add or refine focused architecture defining semantic mapping identities, source ownership, battle-entry resolution, snapshot handoff, and settlement/writeback boundaries.
- Update current authority indexes and concise parent summaries.
- Define corps-class identity as rule interaction rather than scalar differentiation, with cavalry momentum as the first confirmed example.
- Preserve precise player-directed skill targeting and distinguish it from current first-slice skill-content problems.
- Remove or revise current statements that incorrectly say strategic-region to detailed-map mapping is wholly unconfirmed, while preserving boundaries for still-unconfirmed vision, discovery, exact control rules, balance, and implementation sequencing.

## Non-Goals

- No code, scene, resource, configuration, shader, map artifact, or external-state changes.
- No production Runtime integration of the standalone region prototype.
- No literal world-polygon-to-detail-cell coordinate projection requirement.
- No full territorial-control, supply, logistics, vision, discovery, or strategic-AI design.
- No exact cavalry momentum formula, charge distance, turn-rate, collision, counter, cooldown, or balance values.
- No complete base mechanics for every corps class.
- No repair of the current three-hero thunder-skill configuration.
- No new skill content, skill tree, loadout UI, or battle command implementation.

## Constraints And Risks

- Gameplay authority must state player-facing outcomes; architecture authority must state ownership and handoff contracts. Do not copy whole sections between them.
- Strategic regions, city construction regions, battle tactical regions, and authored detailed-map markers are different concepts and must not be conflated.
- Mapping is semantic and stable-id based. Geographic presentation may inspire the detailed layout, but a world polygon is not required to scale directly into battle-grid cells.
- Region approach may affect entrance, deployment, and scenario context, but authored map topology and Runtime legality remain authoritative inside battle.
- Precise targeting does not make every skill strategically sound. Skill homogeneity, excessive area damage, and shared first-slice grants remain separate content issues.
- No installed GodotPrompter skill applies to this gameplay and architecture documentation synchronization.

## Acceptance Criteria

- Current gameplay authority explicitly confirms multi-region strategic locations and semantic region-to-detail-map mapping.
- Different strategic approaches can explicitly resolve different detailed-map entrances or attacker deployment contexts.
- The documents name one authoritative path from geographic definitions and Strategic Management state through the Strategic Battle Bridge into detailed-map battle context, then back through settlement commands.
- The documents do not claim literal pixel/cell projection or completed Runtime implementation.
- Corps classes are required to own readable terrain- and command-facing mechanics rather than being scalar stat packages.
- Cavalry momentum-dependent impact and terrain/space constraints are confirmed without inventing unapproved tuning.
- Player-cast skills retain unit, cell, direction, area, and multi-stage precise targeting where their definitions require it; automatic target-and-release is not the default identity.
- Current authority routes point to the new focused documents and no accepted document still states that all region-to-detail-map mapping is unconfirmed.
- `rg` consistency checks and `git diff --check` pass for the scoped documentation changes.

## Current Progress Snapshot

### Completed

- User confirmed the direction in discussion.
- Relevant gameplay, architecture, active-task, implementation, and historical map-proposal boundaries were inspected.
- Current implementation evidence was separated from the confirmed long-term rules.
- Required repository routes and the affected current gameplay/system authority documents were read in full.
- Existing ownership was confirmed: strategic geography, Strategic Management state, authored site layout/markers, Bridge snapshot handoff, battle tactical regions, and settlement already have distinct authorities; the missing contract is the stable-id semantic mapping between them.
- Gameplay authority now defines multi-region strategic locations, semantic detailed-map mapping, terrain-facing corps mechanics, cavalry momentum at principle level, and precise player-directed skill targeting.
- System authority now defines one stable location/region/map/marker identity path through Strategic Management, authored detailed-map semantics, the Strategic Battle Bridge, Runtime context, settlement, and command writeback.
- Existing presentation, authoring, layout, marker, command, and skill-content boundaries were updated without making the standalone region prototype or detailed maps campaign authority.
- Scoped consistency searches, route-target checks, whitespace checks, `git diff --check`, and final diff review passed.
- The first-verification identity defects were corrected: the strategic location's `LayoutId` binding is now the sole detailed-map selector, while mapping assertions, `SemanticMapMarkerData.MapId`, and Bridge map identity only carry or validate that same value.
- Strategic-region ownership now uses one strategic-location owning identity; battle handoff role-names it `BattleLocationId`, with owned `ApproachRegionId`, and the current hostile-target flow explicitly uses the target location rather than the expedition source location.
- `BattleStartSnapshot`, `StrategicBattleResultSummary`, settlement, and Strategic Management writeback now preserve the same `BattleLocationId`/`ApproachRegionId`/`LayoutId` lineage.
- Combat-command and battle-content authority now treat unit, cell, direction, area, and multi-stage player input as accepted current contracts while allowing first-slice skill-identity repair to reuse existing effect primitives without claiming all possible modes are implemented.
- Correction-round stale-term searches, identity-chain searches, route-target checks, and `git diff --check` passed; no direction conflict was found.
- Third-round Strategic Management correction removed the remaining `source-region identity` ambiguity and role-named persistent approach facts, rules, battle intents, result application, the battle boundary, contracts, failures, and acceptance with target-owned `BattleLocationId`/`ApproachRegionId` plus authoritative `LayoutId` lineage.
- Expedition source location is now explicitly separate participant, station, or rollback context throughout Strategic Management and cannot substitute for `BattleLocationId` or own `ApproachRegionId`.
- Third-round scoped negative and positive identity searches and `git diff --check` passed; no authority conflict or scope expansion was found.

### Remaining

- None.

### Pause Or Blocker

None. The task is complete.

### Resume Condition

None. Completion is independently verified.

### Resume Entry

Archived completion record only. Any Runtime implementation, cavalry-mechanic implementation, or first-slice skill-content repair requires a separate confirmed active task.

### Latest Verification

- First independent verification passed the gameplay scope, four-region-concept separation, sole Strategic Management mutation authority, no-geometry-fallback boundary, no-Runtime-completion claim, cavalry tuning restraint, precise targeting contract, route checks, and whitespace checks.
- First independent verification did not pass the architecture identity chain: `DetailMapId`, marker `MapId`, Site Map `LayoutId`, and Bridge `map definition ID` were left without an explicit single-authority relationship, while the location already owns the authoritative `LayoutId` binding.
- First independent verification also found that strategic-world authoring still used the city-only `CityId + RegionId` contract and that Bridge wording called the approached region a `source strategic region`, which is ambiguous beside existing source/target location roles.
- Gameplay review found an internal stale-scope contradiction: the accepted first three thunder skills already require actor, mark-plus-landing-cell, and direction/area interaction, while gameplay and system authority still called actor-target attacks the only first implementation need and placed full projectile/area behavior wholly in the future. Correction must not claim every possible mode is already implemented.
- `git diff --check -- gameplay-design system-design work-items/active/2026-07-13-strategic-region-detail-map-combat-identity-authority.md` passed before correction; equivalent new-file checks emitted no whitespace diagnostics.
- The unrelated pre-existing untracked `.uid` and archived work item remain outside scope and untouched.
- Correction-round negative search found no `CityId`, ambiguous `source strategic region`/`source location/region`, `map definition ID`, actor-target-only sentence, or full-projectile/area-future sentence in the seven scoped authority documents. The sole `DetailMapId` occurrence is the explicit prohibition against treating it as an independent map-selection identity.
- Positive identity searches confirmed `BattleLocationId`, owned `ApproachRegionId`, authoritative `LayoutId`, `SemanticMapMarkerData.MapId`, `BattleStartSnapshot`, and `StrategicBattleResultSummary` across mapping, authoring, layout, marker, and Bridge authority.
- Skill-boundary searches confirmed current unit/cell/direction/area/multi-stage contracts, no automatic-target fallback, reuse of existing effect primitives for identity repair, and no claim that every mode is implemented.
- All referenced current authority route targets exist. `git diff --check -- gameplay-design system-design work-items/active/2026-07-13-strategic-region-detail-map-combat-identity-authority.md` returned exit 0 after the authority corrections.
- Trailing-whitespace search over the seven scoped authority documents and this task returned no matches; the new mapping authority and this task both end with a final newline.
- Second-round gameplay verification passed without findings: precise input is a current contract without a false Runtime-completeness claim, cavalry remains principle-only, and no skill content or implementation changed.
- Second-round architecture verification passed the sole-`LayoutId` selector, `OwningLocationId + RegionId`, target-owned Bridge/Snapshot/Result lineage, four-region separation, sole Strategic Management mutation authority, no-geometry-fallback, and no-Runtime-completeness checks.
- Second-round architecture verification did not pass `system-design/strategic-management-system-architecture.md`: its persistent-state list still named a `source-region identity`, and its intent/result/writeback clauses used generic location/region lineage rather than the confirmed `BattleLocationId`/owned `ApproachRegionId`/`LayoutId` roles.
- Third-round execution negative search over Strategic Management found no `source-region identity`, `source region identity`, `validated strategic location/region`, `validated location/region lineage`, or `stable location/region identities` matches.
- Third-round execution positive search confirmed `BattleLocationId`, owned `ApproachRegionId`, authoritative `LayoutId`, current target-location role, separate expedition source location, result-application persistence, and sole Strategic Management writeback across every requested section.
- `git diff --check -- system-design/strategic-management-system-architecture.md work-items/active/2026-07-13-strategic-region-detail-map-combat-identity-authority.md` returned exit 0 after the Strategic Management correction. These are execution checks, not independent verification.
- Third-round independent architecture verification passed without findings. It confirmed target-owned `BattleLocationId`/`ApproachRegionId`, authoritative `LayoutId`, separate expedition-source facts, result-application lineage, sole Strategic Management mutation authority, no duplicated mapping ownership, and no Runtime-completion claim across State, Rules, Commands, Strategic Battle Boundary, Contracts, Failure Rules, and Acceptance.
- Main Agent independently reviewed the final scoped diff and reran stale-term, positive identity-role, route, whitespace, and `git diff --check` checks. Every acceptance criterion passed; unrelated untracked files remained untouched.

## GodotPrompter Skills

No installed GodotPrompter skill applies. This task changes gameplay and architecture authority documents only.

## Execution Record

- 2026-07-13: Main Agent created this task after explicit user confirmation. No implementation files were changed.
- 2026-07-13: Execution Agent started the confirmed documentation-only authority synchronization on `main`; unrelated untracked files were identified and left untouched.
- 2026-07-13: Execution Agent read the required routes and affected accepted authority, then confirmed that no direction conflict blocks the documented stable-id mapping and combat-identity synchronization.
- 2026-07-13: Execution Agent added focused gameplay/system mapping authorities and synchronized concise parent, ownership, bridge, corps-identity, and skill-targeting rules. No GodotPrompter skill applied.
- 2026-07-13: Execution Agent completed scoped consistency and diff checks and handed the task to independent verification without changing code, scenes, resources, configuration, map artifacts, tests, history, unrelated tasks, or external state.
- 2026-07-13: Main Agent and a read-only architecture reviewer independently rejected the first handoff on scoped contract clarity. The task returned to `In Progress`: one canonical detailed-map identity must be defined, region ownership must use strategic-location identity consistently, and the approached/battle location region must not be confused with the expedition source location.
- 2026-07-13: Read-only gameplay review additionally found stale first-implementation wording that conflicts with the same authority's accepted three-skill interaction scope; this was added as a bounded documentation correction without expanding into skill-content or Runtime implementation.
- 2026-07-13: Execution Agent corrected only the seven Resume Entry authority documents using `apply_patch`. It made the battle location's existing `LayoutId` the sole map selector; role-named the target/approached location and its region as `BattleLocationId`/`ApproachRegionId`; unified strategic-world region ownership; and corrected the stale skill-input scope without claiming Runtime completeness.
- 2026-07-13: Execution Agent reran scoped stale-term, identity-chain, skill-contract, route-target, and diff checks. All passed, no direction conflict was found, and the task returned to `Awaiting Verification`. No GodotPrompter skill applied.
- 2026-07-13: Second-round gameplay verification passed. Second-round architecture verification returned the task to `In Progress` for one remaining Strategic Management identity-role ambiguity; no gameplay, mapping, Bridge, Runtime, code, resource, or content direction changed.
- 2026-07-13: Execution Agent corrected only the remaining Strategic Management identity-role ambiguity using `apply_patch`, reran scoped negative and positive identity searches plus `git diff --check`, found no conflict, and returned the task to `Awaiting Verification`. No GodotPrompter skill applied; no code, resources, configuration, tests, maps, history, or unrelated files were changed.
- 2026-07-13: A fresh read-only architecture verification passed the third-round correction with no findings. Main Agent accepted all gameplay and architecture criteria, set the task to `Completed`, and archived it under `work-items/archived/2026/`.

## Final Result

Completed and independently verified. The confirmed gameplay and architecture authority now covers semantic strategic-region mapping into one location-bound detailed layout, approach-dependent entrance/deployment context, terrain-facing corps identity with cavalry momentum at principle level, and precise player-directed skill targeting without default automatic release. Remaining risks within this documentation scope: None. Out-of-scope follow-up remains: no Runtime mapping or cavalry mechanic was implemented, exact per-location mappings and cavalry tuning are unconfirmed, and the current first-slice thunder-skill assignment still requires a separate confirmed repair task.
