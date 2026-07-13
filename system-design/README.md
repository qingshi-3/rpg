# System Design

This directory stores the current accepted system architecture for the new gameplay direction.

Use `system-design/` for implementation-facing design: module ownership, runtime responsibilities, persistent state, data flow, scene/resource boundaries, contracts, failure rules, and acceptance criteria.

Do not use this directory for product pitch, temporary plans, implementation progress, or historical migration notes.

## Relationship To Other Directories

```text
gameplay-design/     accepted player-facing gameplay and content rules
system-design/       accepted implementation architecture and contracts
work-items/           confirmed active tasks and execution handoffs
gameplay-alignment/  gap tracking and steady migration workstreams
history/              legacy records, outside normal bootstrap
```

Deleted legacy `docs/` routes are not active inputs. Historical proposal and implementation records are discoverable only through `history/README.md` and do not override accepted authority.

Architecture changes must complete the global discussion stage first. After user confirmation, update the relevant `system-design/` authority document at the start of execution before changing code, scenes, resources, or persistent state.

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

## Accepted Documents

- `hero-led-light-rts-system-architecture.md`: battle-system architecture index and stable cross-cutting invariants.
- `battle-runtime-architecture.md`: live battle runtime ownership, actor phases, runtime events, presentation boundary, and failure rules.
- `battle-navigation-topology-architecture.md`: battle topology compilation, runtime pathfinding, footprints, occupancy, reservations, and path diagnostics.
- `battle-command-architecture.md`: hero/corps/combined command lifecycle, validation boundaries, runtime order events, and command failure semantics.
- `battle-ai-boundary-architecture.md`: tactical autonomy, player-intent precedence, and LimboAI behavior-tree boundary.
- `battle-tactical-intent-architecture.md`: battle target objects, tactical intent plans, target selectors, side-neutral player/enemy intent, and tactical capability boundaries.
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
- `strategic-world-map-authoring-architecture.md`: local Web geographic authoring, shared world/chunk contracts, Godot navigation authoring and compilation, dynamic passage access, and runtime visual loading.
- `strategic-region-detail-map-mapping-architecture.md`: stable-id semantic mapping from a province's member cities into its one authored detailed-map layout, entrances, deployment contexts, and battle handoff facts.
- `strategic-battle-bridge-architecture.md`: accepted bridge contract between Strategic Management and battle Runtime, including battle sessions, preparation drafts, snapshot compilation, result summaries, and strategic command writeback.

## Retired Documents

- `strategic-world-runtime-architecture.md`: retired first-slice strategic runtime authority. Use `strategic-management-system-architecture.md` for new strategic-management work.
- `world-site-management-architecture.md`: retired first-slice world-site management authority. Use `strategic-management-system-architecture.md` for new strategic-management work.
- `world-battle-entry-architecture.md`: retired legacy strategic-world battle entry authority. Use `strategic-battle-bridge-architecture.md` for new Strategic Management to battle integration.
