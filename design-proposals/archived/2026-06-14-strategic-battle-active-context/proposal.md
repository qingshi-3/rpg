# Strategic Battle Active Context

Status: Archived
Created: 2026-06-14
Accepted: 2026-06-14
Merged: 2026-06-14
Archived: 2026-06-14

Requirement Id: SBACT-001
Parent Proposal: `design-proposals/archived/2026-06-13-strategic-battle-bridge-contract`
Supersedes:
Superseded By:
Amends: `design-proposals/archived/2026-06-13-strategic-battle-bridge-contract`
Amended By:
Affected Authority Documents:
- `system-design/strategic-battle-bridge-architecture.md`
- `system-design/scene-transition-router-architecture.md`
Related Implementation Proposals:
- Pending: `gameplay-alignment/implementation-proposals/2026-06-14-strategic-battle-active-context-cutover.md`

## Reason

Strategic Management battle results now apply through Strategic Management commands, but the live bridge path still uses legacy `BattleStartRequest`, static `BattleSessionHandoff`, and legacy `BattleResult` as active scene and result carriers. That keeps strategic battle entry in a migration shape and makes the bridge reconstruct strategic facts from old request/result objects.

This proposal accepts Bridge Active Context as the target mode: a typed active context owned by the Strategic Battle Bridge carries session, snapshot, preparation draft, scene route, Runtime output, settlement, report, and result-consumption state for Strategic Management-backed battles.

## Affected Authority Documents

- `system-design/strategic-battle-bridge-architecture.md`
- `system-design/scene-transition-router-architecture.md`

## Current Design Or Architecture

Current authority already says Strategic Battle Bridge owns the cross-system contract and that `BattleStartRequest`, `BattleResult`, `WorldBattleRequestBuilder`, `WorldBattleResultApplier`, and `BattleSessionHandoff` are legacy artifacts. It also allows typed bridge/session context for scene transition.

The current implementation still uses those legacy artifacts in the active path:

```text
StrategicBattleSession
-> legacy BattleStartRequest
-> static BattleSessionHandoff
-> WorldSiteRoot reads active request
-> Runtime result becomes legacy BattleResult
-> bridge builds StrategicBattleResultSummary from request/result
```

## Expected Design Or Architecture

Strategic Management-backed battles use a single Bridge Active Context:

```text
Strategic Management expedition
-> StrategicBattleBridge creates StrategicBattleSession
-> bridge compiles StrategicBattleActiveContext
-> SceneTransitionRouter carries the active bridge context
-> WorldSiteRoot consumes context for preparation and Runtime launch
-> Runtime produces outcome, event stream, settlement, and report
-> bridge produces StrategicBattleResultSummary from context + Runtime facts
-> Strategic Management command applies consequences
```

`BattleStartRequest` and `BattleResult` may remain as temporary adapters only for non-Strategic legacy battle paths or for presentation code not yet migrated. They must not be the active handoff authority for Strategic Management-backed battles.

`BattleSessionHandoff` must not carry Strategic Management-backed active battle state after this cutover. The bridge active context may be stored in a scoped runtime store or scene-transition context, but it must be typed around Strategic Battle Bridge state and remain independent from long-term Strategic Management state.

## Follow-Up Implementation Scope

- Add `StrategicBattleActiveContext` and a small runtime store/service for active Strategic Management battles.
- Route Strategic Management battle entry through the active context instead of `BattleSessionHandoff`.
- Let battle preparation and Runtime launch read the active context first.
- Preserve a temporary adapter from active context to legacy preparation surfaces only where a UI slice still requires it.
- Complete Runtime results into the active context and build `StrategicBattleResultSummary` without legacy `BattleResult`.
- Keep non-Strategic legacy battles on legacy handoff until explicitly deleted by a later slice.

## Acceptance

- Authority documents identify Bridge Active Context as the long-term Strategic Management battle handoff.
- Strategic Management-backed battle entry no longer depends on legacy request/result/handoff as authority.
- Runtime output, settlement, and report remain battle-side truth; Strategic Management state still mutates only through commands.
- Scene transition owns writing/canceling the active context, not gameplay validation or result fabrication.

## Merge Plan

- `design-proposals/active/2026-06-14-strategic-battle-active-context/expected/system-design/strategic-battle-bridge-architecture.md`
  -> `system-design/strategic-battle-bridge-architecture.md`
- `design-proposals/active/2026-06-14-strategic-battle-active-context/expected/system-design/scene-transition-router-architecture.md`
  -> `system-design/scene-transition-router-architecture.md`
