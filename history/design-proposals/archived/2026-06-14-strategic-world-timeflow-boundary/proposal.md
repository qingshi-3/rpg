# Strategic World Timeflow Boundary Proposal

Status: Archived

## Relationship Metadata

- Requirement Id: `REQ-STRATEGIC-TIME-001`
- Parent Proposal:
  - `design-proposals/archived/2026-06-13-city-corps-muster-economy/`
  - `design-proposals/archived/2026-06-13-strategic-management-system-architecture/`
- Supersedes: None
- Superseded By: None
- Amends:
  - `design-proposals/archived/2026-06-13-city-corps-muster-economy/`
  - `design-proposals/archived/2026-06-13-strategic-management-system-architecture/`
- Amended By: None
- Affected Authority Documents:
  - `gameplay-design/content-systems-long-term-design.md`
  - `gameplay-design/details/cities-and-locations/README.md`
  - `system-design/strategic-management-system-architecture.md`
- Related Implementation Proposals:
  - `gameplay-alignment/implementation-proposals/2026-06-14-strategic-management-step-advancement.md`

## Current Design

The accepted documents define Sanguo Qunying-style conquest, city-led strategic management, faction-shared resources, resource-site production, and strategic state containing world step or strategic time. They do not explicitly define whether strategic management is turn-based, realtime, or paused while the player is inside city management.

This ambiguity allowed a technical settlement command to be described as a possible "end step" or "end turn" UI flow, which contradicts the intended Sanguo Qunying-style pacing.

## Expected Design

Strategic management should use a Sanguo Qunying-style realtime world-map timeline:

- World-map time runs while the player is on the large strategic map.
- Armies, enemy actions, resource production, opportunity refreshes, and later timed projects advance from elapsed world-map time.
- Entering city management pauses world-map time.
- Battle preparation, battle execution, dialogue, and other modal management states also pause world-map time unless a later accepted proposal says otherwise.
- City management commands are issued while paused. They may start projects that later complete on world-map time, but the act of opening or operating the city screen must not advance time.
- Technical settlement ticks or pulses may exist as internal granularity, but they are not player-facing turns and must not imply a Civilization-style "end turn" button.

## Acceptance

The user clarified the intended model on 2026-06-14: the game references Sanguo Qunying; entering a city for management pauses the timeline, and returning to the world map resumes the world timeline. The user then requested that this authority concept be filled in.
