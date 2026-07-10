# System Design

This directory stores the current accepted system architecture for the new gameplay direction.

Use `system-design/` for implementation-facing design: module ownership, runtime responsibilities, persistent state, data flow, scene/resource boundaries, contracts, failure rules, and acceptance criteria.

Do not use this directory for product pitch, temporary plans, implementation progress, or historical migration notes.

## Relationship To Other Directories

```text
gameplay-design/     accepted player-facing gameplay and content rules
system-design/       accepted implementation architecture and contracts
design-proposals/    local proposal copies before merge into authority documents
gameplay-alignment/  gap tracking and steady migration workstreams
```

Deleted legacy `docs/` routes are not active inputs. Historical proposals and implementation records remain discoverable through the archived indexes and do not override accepted authority.

`system-design/` is not edited directly for architecture changes. Use the proposal flow in `../design-proposals/README.md`.

## Document Shape

Each system document should answer:

```text
Gameplay Authority
Responsibility
Does Not Own
Persistent State
Runtime State
Inputs
Outputs
Contracts
Failure Rules
Acceptance
```

Keep documents focused. Split large systems into smaller documents when one file starts mixing ownership, runtime flow, data model, and migration status.

## First Target Areas

Initial architecture documents should be created through proposals for:

- hero/corps light-RTS combat;
- hero command and corps command separation;
- corps level and equipment-level progression;
- city management and resource flow;
- battle result writeback.

## Accepted Documents

- `hero-led-light-rts-system-architecture.md`: battle-system architecture index and stable cross-cutting invariants.
- `battle-runtime-architecture.md`: live battle runtime ownership, actor phases, runtime events, presentation boundary, and failure rules.
- `battle-navigation-topology-architecture.md`: battle topology compilation, runtime pathfinding, footprints, occupancy, reservations, and path diagnostics.
- `battle-command-architecture.md`: hero/corps/combined command lifecycle, validation boundaries, runtime order events, and command failure semantics.
- `battle-ai-boundary-architecture.md`: tactical autonomy, player-intent precedence, and LimboAI behavior-tree boundary.
- `battle-tactical-intent-architecture.md`: battle target objects, tactical intent plans, target selectors, tactical capability boundaries, and enemy-first intent migration scope.
- `battle-group-tactical-region-architecture.md`: battle-group-owned target objects/regions, temporary regions, local combat regions, enemy intent consumption, and player-command separation.
- `battle-result-settlement-architecture.md`: snapshot/result contracts, settlement, report attribution, recovery, rollback, and campaign writeback rules.
- `battle-content-progression-architecture.md`: ability/effect definitions, battle content resourceization, and resource/progression loops.
- `emotion-system-architecture.md`: character emotional traits, relationships, memories, social gates, and decision support.
- `presentation-ui-layout-architecture.md`: Presentation/UI layout hosts, UI mode boundaries, and authority rules.
- `resource-authoring-taxonomy.md`: repository directory ownership for raw assets, authored resources, scenes, source code, config indexes, and resource-migration exception rules.
- `semantic-map-marker-architecture.md`: reusable editor-authored semantic map regions for construction regions, deployment, entrances, events, and future tactical battle markers.
- `site-map-layout-architecture.md`: reusable base terrain scenes, inherited site layout variants, bridge marker rules, layout extraction, validation, and per-location state isolation.
- `scene-transition-router-architecture.md`: root scene transition ownership, handoff boundaries, loading overlay, and conservative root-scene preload cache rules.
- `strategic-management-system-architecture.md`: clean Strategic Management authority for strategic content, state, rules, commands, presentation view models, and strategic-side battle boundary.
- `strategic-battle-bridge-architecture.md`: accepted bridge contract between Strategic Management and battle Runtime, including battle sessions, preparation drafts, snapshot compilation, result summaries, and strategic command writeback.

## Retired Documents

- `strategic-world-runtime-architecture.md`: retired first-slice strategic runtime authority. Use `strategic-management-system-architecture.md` for new strategic-management work.
- `world-site-management-architecture.md`: retired first-slice world-site management authority. Use `strategic-management-system-architecture.md` for new strategic-management work.
- `world-battle-entry-architecture.md`: retired legacy strategic-world battle entry authority. Use `strategic-battle-bridge-architecture.md` for new Strategic Management to battle integration.
