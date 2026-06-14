# Runtime Displacement Boundary

Status: Archived
Created: 2026-06-12
Accepted: 2026-06-12
Merged: 2026-06-12
Archived: 2026-06-12

Requirement Id: runtime-displacement-boundary-2026-06-12
Parent Proposal: `design-proposals/archived/2026-06-11-thunder-mark-skill-family/proposal.md`
Supersedes:
Superseded By:
Amends: `design-proposals/archived/2026-06-11-thunder-mark-skill-family/proposal.md`
Amended By:
Affected Authority Documents: `system-design/battle-runtime-architecture.md`
Related Implementation Proposals: `gameplay-alignment/implementation-proposals/2026-06-11-thunder-mark-demo-skill-family.md`

## Reason

Thunder Mark Fold revealed a missing Runtime contract: teleport can legally change an actor anchor, but any active or passive displacement must also invalidate movement and tactical context that was derived from the old position. This rule is general to teleport, knockback, pull, charge displacement, and future spatial transfer.

## Affected Authority Documents

- `system-design/battle-runtime-architecture.md`

## Current Design Or Architecture

Runtime already owns teleport destination legality, anchor mutation, occupancy updates, and teleport events. The accepted architecture does not explicitly define which transient actor and tactical facts must be reset when a displacement moves an actor to a new anchor.

## Expected Design Or Architecture

Runtime owns a shared displacement commit boundary. Any effect that moves an actor without ordinary neighbor movement must commit the new anchor through that boundary, clear old movement segment state, reservations, movement intent snapshots, local steering and backtrack memory, and stale target or local-combat context tied to the previous anchor. The actor then returns to the normal Runtime decision flow from the new anchor unless an active skill lock or queued command explicitly keeps owning the actor.

Displacement must mark tactical observations and local combat facts for rebuild from the new authoritative position. Presentation may snap or animate the emitted displacement event, but it must not decide the post-displacement target, movement continuation, or combat state.

## Follow-Up Implementation Scope

- Add a Runtime displacement helper or service.
- Route Thunder Mark Fold through the helper.
- Add regression coverage proving teleport clears stale movement/target state and resumes decision-making from the new anchor.
- Keep Thunder Spiral channel ownership intact when the displacement happens during the channel.

## Acceptance

- User approved execution after reviewing the displacement-boundary design in chat.
- Authority document updated before implementation.
- Focused Runtime regression added before production code.

## Merge Plan

- Merged this proposal's expected displacement contract into `system-design/battle-runtime-architecture.md`.
