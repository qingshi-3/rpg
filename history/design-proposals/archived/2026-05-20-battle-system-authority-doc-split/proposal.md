# Battle System Authority Document Split Proposal

Status: Archived
Created: 2026-05-20
Accepted: 2026-05-20
Merged: 2026-05-20
Archived: 2026-05-20

Requirement Id: REQ-BATTLE-SYSTEM-AUTHORITY-DOC-SPLIT
Parent Proposal: `design-proposals/archived/2026-05-17-hero-led-light-rts-system-architecture`
Supersedes: None
Superseded By: None
Amends:
- `REQ-BNAV-TOPOLOGY-DECOUPLING`
Amended By: None
Affected Authority Documents:
- `system-design/README.md`
- `system-design/hero-led-light-rts-system-architecture.md`
- `system-design/battle-runtime-architecture.md`
- `system-design/battle-navigation-topology-architecture.md`
- `system-design/battle-command-architecture.md`
- `system-design/battle-ai-boundary-architecture.md`
- `system-design/battle-result-settlement-architecture.md`
- `system-design/battle-content-progression-architecture.md`
Related Implementation Proposals: None. This proposal changes documentation authority only.

## Reason

`system-design/hero-led-light-rts-system-architecture.md` had grown into a large mixed authority document. Future work had to reread unrelated runtime, navigation, command, AI, settlement, ability, progression, and migration content just to make a local design decision.

The current workflow needs progressive disclosure: future agents should read only the smallest authority document relevant to the task.

## Current Design Or Architecture

The current accepted architecture stores most battle-system rules in one file:

- stable battle identity and cross-cutting invariants;
- runtime ownership;
- battle-space and navigation rules;
- command lifecycle;
- tactical AI and LimboAI boundary;
- ability/effect definitions;
- resource/progression flow;
- settlement/report attribution;
- runtime persistence and migration rules.

The battle navigation rule also still conflicted with the accepted topology-decoupling requirement because the authority text allowed the static runtime graph to own terrain legality and authored movement cost.

## Expected Design Or Architecture

Keep `hero-led-light-rts-system-architecture.md` as a short architecture index and stable invariant document.

Move detailed authority into focused documents:

- `battle-runtime-architecture.md`
- `battle-navigation-topology-architecture.md`
- `battle-command-architecture.md`
- `battle-ai-boundary-architecture.md`
- `battle-result-settlement-architecture.md`
- `battle-content-progression-architecture.md`

The navigation authority must use the latest accepted rule:

```text
Godot TileMapLayer and authored map topology
-> immutable BattleNavigationTopology data
-> runtime pathfinding, occupancy, and reservations
```

Runtime pathfinding consumes only topology, actor footprint facts, dynamic occupancy, same-tick reservations, and current command/target facts. It must not parse TileMapLayer, water, bridge, layer roles, or raw height-link authoring concepts.

Every runtime decision boundary recalculates pathfinding from current actor, target, topology, footprint, occupancy, reservations, and command facts.

## Follow-Up Implementation Scope

No code implementation is authorized by this proposal.

Future pathfinding implementation must use a separate focused technical-change proposal under `docs/50-production/technical-changes/`, referencing `battle-navigation-topology-architecture.md` and `battle-runtime-architecture.md`.

## Acceptance

- `hero-led-light-rts-system-architecture.md` is reduced to index, invariants, layers, and routing.
- Focused battle-system authority documents exist under `system-design/`.
- `system-design/README.md` routes to each focused authority document.
- Navigation authority states the topology data-layer split and runtime decision-boundary replanning.
- `2026-05-20-battle-navigation-topology-decoupling` can be archived because its expected rule is merged into authority.

## Merge Plan

- `expected/system-design/README.md` -> `system-design/README.md`
- `expected/system-design/hero-led-light-rts-system-architecture.md` -> `system-design/hero-led-light-rts-system-architecture.md`
- `expected/system-design/battle-runtime-architecture.md` -> `system-design/battle-runtime-architecture.md`
- `expected/system-design/battle-navigation-topology-architecture.md` -> `system-design/battle-navigation-topology-architecture.md`
- `expected/system-design/battle-command-architecture.md` -> `system-design/battle-command-architecture.md`
- `expected/system-design/battle-ai-boundary-architecture.md` -> `system-design/battle-ai-boundary-architecture.md`
- `expected/system-design/battle-result-settlement-architecture.md` -> `system-design/battle-result-settlement-architecture.md`
- `expected/system-design/battle-content-progression-architecture.md` -> `system-design/battle-content-progression-architecture.md`
