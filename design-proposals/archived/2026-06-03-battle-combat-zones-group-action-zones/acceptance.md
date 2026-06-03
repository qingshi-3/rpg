# Acceptance

Accepted by user direction on 2026-06-03 during the battle movement and local combat debugging session.

User-approved terms:

- `CombatZone`: global combat area, not owned by a unit or group.
- `编组行动区` / `GroupActionZone`: commander-group-owned movement or tactical intent area.

Accepted behavior:

- Combat-zone construction is triggered by contact/engagement changes and operates over all living units.
- Group action-zone construction is triggered at battle start, command/objective changes, invalidation, and a fixed tick interval.
- Both construction moments must log a complete area snapshot with combat zones, deployment zones, group action zones, and unit positions.
