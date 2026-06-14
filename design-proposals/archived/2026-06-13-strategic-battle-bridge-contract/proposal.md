# Strategic Battle Bridge Contract Proposal

Status: Archived

## Relationship Metadata

Requirement Id: `REQ-2026-06-13-strategic-battle-bridge-contract`

Parent Proposal: `2026-06-13-strategic-management-system-architecture`

Supersedes: None

Superseded By: None

Amends: `2026-06-13-strategic-management-system-architecture`

Amended By: None

Affected Authority Documents:

- `system-design/README.md`
- `system-design/hero-led-light-rts-system-architecture.md`
- `system-design/strategic-management-system-architecture.md`
- `system-design/strategic-battle-bridge-architecture.md`
- `system-design/world-battle-entry-architecture.md`
- `system-design/battle-result-settlement-architecture.md`
- `system-design/scene-transition-router-architecture.md`

Related Implementation Proposals: None yet

## Requirement

Define the long-term bridge contract between the clean Strategic Management system and the hero-led battle system before strategic rebuild implementation starts.

The bridge must prevent the new strategic layer from depending on legacy `StrategicWorldState`, `WorldArmyState`, `GarrisonState`, `BattleStartRequest`, `BattleResult`, `WorldBattleRequestBuilder`, or `WorldBattleResultApplier` as long-term authorities.

## Current Architecture

Current battle entry is a migration stack:

```text
legacy strategic world state
-> WorldBattleRequestBuilder
-> BattleStartRequest
-> BattleSessionHandoff
-> battle preparation mutates the same request
-> LegacyBattleStartSnapshotAdapter
-> BattleStartSnapshot
-> Runtime
-> BattleOutcomeResult and BattleEventStream
-> LegacyBattleResultAdapter
-> BattleResult
-> WorldBattleResultApplier
-> legacy strategic world state
```

This keeps the battle Runtime usable, but the contract is too broad and still carries old world concepts into battle entry and result writeback.

## Expected Architecture

Strategic Management requests battles through a narrow Strategic Battle Bridge. The bridge owns the transient battle session, battle-preparation draft, conversion to `BattleStartSnapshot`, scene handoff payload, and conversion from Runtime facts into a strategic result summary.

The long-term flow becomes:

```text
Strategic Management command
-> StrategicBattleIntent or PendingBattle
-> Strategic Battle Bridge session
-> battle preparation draft
-> immutable BattleStartSnapshot
-> Battle Runtime
-> BattleOutcomeResult and BattleEventStream
-> settlement and report facts
-> StrategicBattleResultSummary
-> Strategic Management ApplyBattleResult command
```

`BattleStartRequest` and `BattleResult` are not long-term bridge DTOs. If temporary adapters remain during implementation, they must be isolated migration adapters and must not own new strategic facts or gameplay rules.

## Acceptance

The user accepted this direction in discussion on 2026-06-13:

- do not continue the new Strategic Management rebuild through the legacy `BattleStartRequest/BattleResult` bridge;
- use a dedicated Strategic Battle Bridge contract;
- keep battle Runtime on `BattleStartSnapshot` and `BattleOutcomeResult`;
- make strategic state writeback happen through Strategic Management commands;
- handle scene transition as a carrier of bridge session context, not gameplay authority.
