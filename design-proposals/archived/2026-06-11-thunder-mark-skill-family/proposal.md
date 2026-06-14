# Thunder Mark Skill Family

Status: Archived
Created: 2026-06-11
Accepted: 2026-06-11
Merged: 2026-06-11
Archived: 2026-06-11

Requirement Id: thunder-mark-skill-family
Parent Proposal: None
Supersedes: None
Superseded By: None
Amends: 2026-06-06-runtime-skill-effect-architecture
Amended By: None
Affected Authority Documents: `gameplay-design/details/combat-command/README.md`; `system-design/battle-content-progression-architecture.md`; `system-design/battle-runtime-architecture.md`
Related Implementation Proposals: `gameplay-alignment/implementation-proposals/2026-06-11-thunder-mark-demo-skill-family.md`

## Reason

The demo needs a high-impact hero skill family that demonstrates hero-level strength, spatial tactics, and Runtime-backed technical credibility. A lightning-mark skill family is accepted as the first flagship example because it turns hero mobility into battlefield coordinate control instead of only adding damage.

## Affected Authority Documents

- `gameplay-design/details/combat-command/README.md`
- `system-design/battle-content-progression-architecture.md`
- `system-design/battle-runtime-architecture.md`

## Current Design Or Architecture

Current authority supports player-cast hero skills, targeted/non-targeted ability contracts, source-agnostic damage effects, action locks, and square-grid Runtime movement truth. It does not yet define a durable design pattern for mark-created coordinates, legal teleport placement, offhand mark projectiles, channeled melee output that can continue through teleport, or high-tier skill/event redirection.

## Expected Design Or Architecture

Accepted design adds a thunder-mark skill family:

- a mark projectile creates either a ground mark or an attached unit mark while still functioning as a combat projectile;
- a teleport skill moves the hero to a player-selected legal anchor near a live mark;
- a channeled melee skill may continue while the hero teleports, without refreshing duration;
- a later high-tier spatial transfer skill may redirect a unit or skill event only through Runtime-owned event and placement facts.

The demo implementation may ship only the first three skills. Spatial transfer remains accepted design but is not first implementation scope.

## Follow-Up Implementation Scope

- Add focused implementation proposal under `gameplay-alignment/implementation-proposals/`.
- Extend skill snapshots/effects with mark, teleport, and channeled area-damage semantics.
- Add Runtime state for temporary marks and channeled skill windows.
- Add tests before production code.
- Add low-noise Runtime events so Presentation, report, and diagnostics can explain mark creation, teleport, and channeled damage.

## Acceptance

- Authority documents describe thunder-mark gameplay and Runtime boundaries.
- Design does not make Presentation authoritative for teleport or damage.
- Implementation proposal limits the first slice to skills 1-3 and explicitly defers spatial transfer.

## Merge Plan

- `expected/gameplay-design/details/combat-command/README.md` -> `gameplay-design/details/combat-command/README.md`
- `expected/system-design/battle-content-progression-architecture.md` -> `system-design/battle-content-progression-architecture.md`
- `expected/system-design/battle-runtime-architecture.md` -> `system-design/battle-runtime-architecture.md`
