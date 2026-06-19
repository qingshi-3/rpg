# Strategic Battle Legacy Garrison Retirement Implementation Proposal

Status: Implemented - Automated Verification Passed

Priority: High

## Relationship Metadata

- Origin: 2026-06-18 modular code/document consistency audit high-risk finding.
- Requirement slice: Strategic Management-backed battle flow must retire legacy WorldSite/garrison ownership for strategic participants.
- Originating design proposal: Not required; current accepted authority already defines the contract.
- Amendment proposals: None.
- Blocking issues: Strategic expedition participants can still be imported into legacy `WorldSiteState.Garrison` and survive as stale player-army garrison rows.
- Verification records: Pending implementation verification.

## Authority

- Implements `gameplay-design/content-systems-long-term-design.md`.
- Implements first playable expedition, battle entry, and result cases in `gameplay-design/vertical-slices/first-playable-slice.md`, especially VS-04, VS-05, VS-07, VS-12, and VS-13.
- Implements `system-design/strategic-management-system-architecture.md`, especially Clean Rebuild Policy and Legacy Authority Retirement.
- Implements `system-design/strategic-battle-bridge-architecture.md`, especially Participant Identity and Legacy Retirement.
- Follows `system-design/battle-result-settlement-architecture.md` for corps-strength and result-writeback ownership.

## Current Inconsistency

Accepted authority says Strategic Management owns durable hero/corps/expedition state, and legacy world army, garrison, and site-state source kinds must not receive new Strategic Management behavior. Migration adapters may only convert facts at boundaries and must remain removable.

The current implementation still lets strategic expedition participants pass through legacy garrison structures:

- `StrategicExpeditionWorldArmyAdapter` describes `WorldArmyState` as a temporary carrier for Strategic Management expedition facts.
- `WorldBattleRequestBuilder` can add strategic expedition participants as `PlayerArmy` site-garrison forces.
- `WorldSiteRoot.BattleRuntime.cs` removes transient `UnitPlacements` after strategic results, but does not fully retire related legacy `Garrison` rows.
- Legacy garrison rows can therefore retain strategic participants after Strategic Management result writeback.

This creates a second stale owner for hero-company presence and corps strength after battle.

## Goal

Stop Strategic Management-backed battles from writing new strategic participants into `WorldSiteState.Garrison`, and clean up transition leftovers that were already created by legacy adapters.

After this optimization, strategic participant ownership should follow this invariant:

```text
Strategic Management expedition/hero/corps state
-> Strategic Battle Bridge participant references
-> BattleStartSnapshot force/actor source mappings
-> StrategicBattleResultSummary
-> Strategic Management commands
```

Legacy `WorldSiteState.Garrison` may remain for non-strategic resident defenders or old compatibility flows only. It must not own new Strategic Management hero-company facts.

## Scope

- Stop the Strategic Management-backed battle path from importing strategic participants into `targetSite.Garrison`.
- Keep participant force composition sourced from bridge session, active context, snapshot, or explicit strategic participant references.
- Add transitional cleanup for stale `PlayerArmy` `Garrison` rows related to strategic battle participants when applying or returning from a Strategic Management-backed result.
- Keep existing cleanup for related `UnitPlacements`, and extend diagnostics to report both placement and garrison cleanup counts.
- Preserve non-strategic legacy site garrison behavior for old resident defenders and explicitly scoped compatibility battles.
- Ensure no new Strategic Management code treats legacy garrison count as corps strength, hero company count, or expedition truth.

## Non-Goals

- Do not remove all legacy WorldSite management or all resident garrison behavior in this slice.
- Do not redesign city garrison capacity or future city defense rules.
- Do not change battle Runtime participant identity once the snapshot is compiled.
- Do not add a new strategic persistence schema beyond existing Strategic Management state.
- Do not use double writes to keep Strategic Management and legacy garrison in sync.

## Touched Systems

- `src/Application/World/StrategicExpeditionWorldArmyAdapter.cs`
- `src/Application/World/WorldBattleRequestBuilder.cs`
- `src/Application/World/WorldSiteBattleUnitPoolService.cs`
- `src/Application/World/WorldBattleResultApplier.cs` only if compatibility cleanup is still routed there
- `src/Presentation/World/Sites/WorldSiteRoot.BattleRuntime.cs`
- Strategic Management battle bridge participant mapping tests
- Strategic Management regression tests for expedition launch/result writeback

## Implementation Guidance

1. Identify every Strategic Management-backed path that writes `SourceKind == "PlayerArmy"` rows into `WorldSiteState.Garrison`.
2. Replace those writes with bridge/session/snapshot participant references for strategic battle launch.
3. Add explicit guards so strategic paths do not call legacy garrison import helpers as authoritative participant setup.
4. Remove stale strategic-derived `PlayerArmy` garrison rows after strategic result consumption or return, using session/request participant IDs to avoid deleting unrelated resident defender data.
5. Keep cleanup idempotent so repeated return/result handling does not mutate unrelated state.
6. Add diagnostics that distinguish removed placements from removed garrison rows.

## Tests

- Add a regression proving building or launching a Strategic Management-backed Bonefield battle does not mutate `targetSite.Garrison` with strategic `PlayerArmy` rows.
- Add a regression proving strategic force composition still comes from bridge active context/snapshot participants after legacy garrison import is removed.
- Add a regression proving strategic result cleanup removes stale strategic-derived `PlayerArmy` `Garrison` rows and matching `UnitPlacements`.
- Add a regression proving cleanup does not remove non-strategic resident defender garrison rows.
- Add a regression proving repeated cleanup is idempotent.
- Re-run:
  - `dotnet run --project tests\StrategicManagementRegression\StrategicManagementRegression.csproj -v:minimal`
  - `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal`
  - `dotnet build rpg.csproj -maxcpucount:2 -v:minimal`

## Diagnostics

- Log strategic garrison import skipped or blocked with session ID, target location ID, and participant count.
- Log strategic legacy cleanup with removed placement count, removed garrison row count, target site ID, expedition ID, and session ID when available.
- Warn when a Strategic Management-backed launch still sees strategic-derived `PlayerArmy` garrison rows before cleanup.
- Do not log every garrison row in normal play.

## Manual QA

- Start from the strategic world and inspect the starting stronghold and Bonefield before battle.
- Send the selected hero-company expedition to Bonefield and enter battle through the confirmation gate.
- Complete victory and return to the strategic world.
- Confirm Strategic Management state records the result while old site garrison UI does not show duplicated or stale player-army participants at Bonefield.
- Repeat with defeat or cancellation when supported and confirm no stale garrison rows remain.

## Acceptance Evidence

- 2026-06-18: Strategic Management-backed assault requests now skip legacy target-site garrison import and build player forces directly from the strategic world army carrier while preserving `PlayerArmy` source identity and strategic participant command grouping. Added cleanup for stale `PlayerArmy` garrison rows through `WorldSiteBattleUnitPoolService.RemoveImportedArmyForSiteBattle`, with strategic result cleanup logging both placement and legacy-garrison removal counts. Verification passed: `dotnet run --project tests\StrategicManagementRegression\StrategicManagementRegression.csproj -v:minimal`, `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal`, `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal`, `dotnet build rpg.csproj -maxcpucount:2 -v:minimal`, and `git diff --check -- src tests gameplay-alignment/implementation-proposals`. Regression project runs still report existing Godot source-generator / nullable warnings; project build reports 0 warnings and 0 errors.
