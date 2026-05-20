# Hero-Led Bridge Battle V0 Design Proposal

Status: Archived
Workflow Role: Historical legacy mixed proposal. Current authority lives in focused `gameplay-design/` and `system-design/battle-*.md` documents; do not use this archived body as implementation authority.
Archive Note: Closed after the battle runtime/navigation/playback repair was validated on 2026-05-21. The broad bridge playable slice was not merged as a single active authority change; future bridge-slice work should start a new focused proposal against current authority documents.
Requirement Id: REQ-BRIDGE-SPATIAL-BATTLE-V0
Parent Proposal: `design-proposals/archived/2026-05-18-spatial-tactical-combat-mechanics`
Supersedes: None
Superseded By: None
Amends: None
Amended By:
- `design-proposals/archived/2026-05-20-battle-navigation-topology-decoupling`
- `design-proposals/archived/2026-05-20-battle-system-authority-doc-split`
Affected Authority Documents:
- `gameplay-design/details/combat-command/README.md`
- `system-design/hero-led-light-rts-system-architecture.md`
- `system-design/battle-runtime-architecture.md`
- `system-design/battle-navigation-topology-architecture.md`
- `system-design/battle-command-architecture.md`
- `system-design/battle-ai-boundary-architecture.md`
- `system-design/battle-result-settlement-architecture.md`
- `system-design/semantic-map-marker-architecture.md`
Related Implementation Proposals: No separate active implementation proposal remains. `runtime-navigation-playback-repair.md` is archived here as historical source material; accepted runtime/navigation behavior is covered by current authority documents and regression tests.

## Purpose

Define the first playable validation slice for the accepted hero-led, low-frequency spatial realtime combat direction.

This proposal keeps implementation narrow: one bridge-style battle validation scene, durable auto-fighting heroes, rough battle-only down feedback, optional minimal command/posture handling, tactical semantic markers, and enough battle feedback for hands-on play. It does not claim the full combat design is implemented.

See `playable-slice-v0.md` for the first playable experience contract.

Runtime movement, navigation, and playback repair was validated through the focused battle runtime/navigation authority and regression tests. `runtime-navigation-playback-repair.md` remains archived source material, not an active plan.

## Current Architecture

The accepted gameplay direction now says combat should be judged by spatial decisions such as chokepoints, timing, reserves, telegraphed enemy actions, and retreat windows.

The current implementation already has useful foundations:

- battle groups with separate hero and corps state;
- square-grid realtime movement, occupancy, reservation, footprints, and actor-target basic attacks;
- semantic map markers and side-aware deployment zones;
- battle runtime event stream, settlement, and report skeletons;
- a LimboAI migration branch in progress for AI decision boundaries.

The missing bridge between design and implementation is a first battle slice where a raised hero enters the fight early, automatic combat remains readable, and feedback is clear enough for the player to judge whether the core loop feels good.

## Expected Implementation Slice

Implement one bridge assault or bridge defense validation slice.

The slice should prove these facts:

- a player hero company auto-enters meaningful combat early instead of waiting behind soldiers until cleanup;
- hero defeat is a battle-only down state, not permanent loss;
- existing basic attacks and coarse tuning are enough to produce a playable first loop without building a new skill system;
- the battle map has an authored chokepoint or bridge lane that constrains movement through existing square-grid navigation and occupancy;
- tactical semantic markers such as `ChokePoint`, `Lane`, `ReservePoint`, `RangedPoint`, or `DefendPoint` can enter a battle snapshot as pure data;
- the battle gives enough low-noise feedback to judge hero survival, hero contribution, corps contribution, and outcome.

## Phase Boundaries

Phase 1: author and validate the Bonefield Bridgehead scene shape, deployment zones, and tactical markers.

Phase 2: make the first hero company auto-enter the fight with a durability floor and battle-only down feedback.

Phase 3: tune existing attacks, unit counts, HP, damage, and target pressure until the battle is playable without precision babysitting.

Phase 4: pass tactical marker data through Application snapshots without letting Runtime query Godot marker nodes.

Phase 5: add minimal command/posture controls only if the first loop needs them and they route through accepted command/runtime-order boundaries.

Phase 6: extend battle feedback or the battle report only after the first loop is playable enough to evaluate.

## Coordination With LimboAI

The LimboAI migration is separate. This proposal may consume the accepted AI facade boundary after it lands, but it must not move movement legality, occupancy, damage, outcome, settlement, or report truth into behavior trees.

## Non-Goals

- No full hero/corps/combined command UI.
- No new skill system in the rough first implementation.
- No broad skill system or many spatial skills.
- No permanent hero death or campaign-loss implication from ordinary battle down.
- No fragile-backline combat where the player must constantly babysit a hero through precision movement.
- No full reserve/reinforcement system.
- No multi-map tactical campaign.
- No direct LimboAI ownership of runtime truth.
- No hardcoded battle scene structure in C# when an authored resource can carry it.

## Acceptance Criteria

- One authored bridge-style battle scene or map slice exists and is used by the playable battle path.
- A hero company fights from the start and survives ordinary early contact long enough to show battle identity.
- The first loop works without adding a new skill system.
- The scene has visible tactical semantic markers and regression coverage for marker extraction or routing.
- Runtime receives tactical marker facts through snapshots or Application services, not scene-node queries.
- Command/posture, spatial event facts, and report explanations are added only if they are needed for the first playable loop or are cheap to expose from existing runtime facts.
- Existing regression tests for deployment zones, battle group snapshots, runtime movement, and LimboAI boundary still pass after the slice lands.
