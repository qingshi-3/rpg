# Strategic Battle Active Context Snapshot Authority Implementation Proposal

Status: Implemented - Automated Verification Passed

Priority: High

## Relationship Metadata

- Origin: 2026-06-18 modular code/document consistency audit high-risk finding.
- Requirement slice: Strategic Management-backed battle launch must use the bridge-owned active context snapshot as the Runtime launch authority.
- Originating design proposal: Not required; current accepted authority already defines the contract.
- Amendment proposals: None.
- Blocking issues: Active-context launch currently recompiles or replaces the snapshot through a legacy request/probe path.
- Verification records: Pending implementation verification.

## Authority

- Implements `gameplay-design/content-systems-long-term-design.md`.
- Implements first playable battle entry and result-loop cases in `gameplay-design/vertical-slices/first-playable-slice.md`, especially VS-03, VS-06, VS-07, VS-08, VS-12, and VS-13.
- Implements `system-design/strategic-management-system-architecture.md`.
- Implements `system-design/strategic-battle-bridge-architecture.md`, especially Bridge Active Context, Snapshot Boundary, Scene Transition Handoff, and Legacy Retirement.
- Follows `system-design/scene-transition-router-architecture.md` for typed battle handoff ownership.

## Current Inconsistency

The accepted contract says Strategic Management-backed battles use Bridge Active Context as the long-term active handoff, and `BattleStartSnapshot` is the immutable Runtime input compiled by the Strategic Battle Bridge.

The current active-context launch path still treats the legacy request/probe as launch authority:

- `src/Application/StrategicBattleBridge/StrategicBattleBridgeService.cs` compiles a bridge snapshot for the active context.
- `src/Application/World/WorldSiteBattleGroupRuntimeAdapter.cs` calls `_sessionService.PrepareSnapshot(request)` inside the active-context path.
- The same adapter replaces `activeContext.Snapshot` with the probe result, so the bridge-compiled snapshot is not guaranteed to be the Runtime input.

This leaves two competing authorities for the same strategic battle start: bridge active context and legacy `BattleStartRequest` snapshot preparation.

## Goal

Make the active Bridge Context snapshot the only Runtime launch authority for Strategic Management-backed battles.

After this optimization, a Strategic Management-backed battle should follow this invariant:

```text
Strategic Battle Bridge compiles activeContext.Snapshot
-> Scene transition carries the active context or typed reference
-> Runtime adapter consumes activeContext.Snapshot directly
-> no active-context launch path replaces it from BattleStartRequest
```

## Scope

- Change the active-context launch path in `WorldSiteBattleGroupRuntimeAdapter.TryStartActiveBattle` so Runtime consumes the precompiled `activeContext.Snapshot`.
- Reject active-context launch explicitly when the active context is missing, has no snapshot, has a mismatched session ID, or has a malformed Runtime scene path.
- Preserve legacy `BattleStartRequest` / `PrepareSnapshot(request)` only for explicitly non-Strategic compatibility paths.
- Prevent active-context launch code from mutating `activeContext.Snapshot` after bridge compilation, except for an explicit bridge-owned rebuild API if a future accepted proposal defines one.
- Keep snapshot ID, session ID, strategic battle context ID, scene path, return path, and rollback context stable across handoff.
- Add low-noise diagnostics so a future reader can tell which snapshot actually entered Runtime.

## Non-Goals

- Do not remove all legacy battle request code in this slice.
- Do not change battle Runtime simulation, damage, movement, AI, skill execution, or settlement rules.
- Do not redesign battle preparation UI.
- Do not add save/resume for active battle sessions.
- Do not use `BattleStartRequest` as a compatibility alias for new Strategic Management authority.

## Touched Systems

- `src/Application/StrategicBattleBridge/`
- `src/Application/World/WorldSiteBattleGroupRuntimeAdapter.cs`
- `src/Application/World/` legacy battle request compatibility boundaries only as needed
- `src/Presentation/World/Sites/` battle entry boot path only if it assumes the legacy request snapshot
- `tests/StrategicManagementRegression/`
- `tests/WorldSiteDeploymentCacheRegression/`
- `tests/TargetBattleArchitectureRegression/`

## Implementation Guidance

1. Identify the exact branch that handles `StrategicBattleActiveContext` launch.
2. Split active-context launch from legacy request launch if they currently share authority-bearing code.
3. In the active-context branch, validate and pass `activeContext.Snapshot` directly into the Runtime launch result.
4. Keep any legacy `BattleStartRequest` projection as a read-only compatibility payload for old UI fields only; it must not create or replace the authoritative snapshot.
5. Add an explicit failure when the active context cannot supply a valid snapshot.
6. Add a source-shape or behavior regression that would fail if the active-context branch calls `PrepareSnapshot(request)` as launch authority again.

## Tests

- Add a Strategic Management regression proving that an active-context launch consumes the precompiled bridge snapshot and preserves the same snapshot ID.
- Add a regression proving `activeContext.Snapshot` is not replaced by `WorldSiteBattleGroupRuntimeAdapter` during Strategic Management-backed launch.
- Add a regression proving the active-context path does not call `_sessionService.PrepareSnapshot(request)` as launch authority.
- Add a negative regression proving missing or mismatched active-context snapshot fails explicitly without falling back to legacy request compilation.
- Re-run:
  - `dotnet run --project tests\StrategicManagementRegression\StrategicManagementRegression.csproj -v:minimal`
  - `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal`
  - `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal`
  - `dotnet build rpg.csproj -maxcpucount:2 -v:minimal`

## Diagnostics

- Log active-context launch accepted with strategic battle context ID, session ID, snapshot ID, target location ID, and scene path.
- Log active-context launch rejected with a stable failure reason when snapshot, session, context, or scene path is missing or mismatched.
- Do not log per-frame or high-frequency Runtime state.

## Manual QA

- Start from the strategic world.
- Send the selected expedition to Bonefield.
- Confirm the battle trigger.
- Enter battle preparation/runtime through the Strategic Management path.
- Confirm the battle starts with the expected deployed hero companies and no fallback/debug battle composition.
- Confirm cancellation or return still leaves strategic world state consistent.

## Acceptance Evidence

- 2026-06-18: Active-context Runtime launch now consumes the bridge-compiled `activeContext.Snapshot` directly and rejects missing or mismatched active-context snapshots instead of rebuilding from the legacy `BattleStartRequest`. Added regression coverage proving the Runtime start preserves the bridge snapshot ID and does not replace strategic participant identities with probe-generated identities. Verification passed: `dotnet run --project tests\StrategicManagementRegression\StrategicManagementRegression.csproj -v:minimal`, `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal`, `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal`, `dotnet build rpg.csproj -maxcpucount:2 -v:minimal`, and `git diff --check -- src tests gameplay-alignment/implementation-proposals`. Regression project runs still report existing Godot source-generator / nullable warnings; project build reports 0 warnings and 0 errors.
