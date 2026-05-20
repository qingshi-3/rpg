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
docs/                existing project documentation and historical implementation material
```

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

- `hero-led-light-rts-system-architecture.md`: target battle architecture for hero-led light RTS runtime, settlement, and legacy adapter migration.
- `presentation-ui-layout-architecture.md`: Presentation/UI layout hosts, UI mode boundaries, and authority rules.
- `semantic-map-marker-architecture.md`: reusable editor-authored semantic map regions for building slots, deployment, entrances, events, and future tactical battle markers.
- `scene-transition-router-architecture.md`: root scene transition ownership, handoff boundaries, loading overlay, and conservative root-scene preload cache rules.
