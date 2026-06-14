# Strategic Army Command Application Boundary Implementation Proposal

Status: Implemented - Automated Verification Passed

## Origin And Authority

- Review batch: architecture review cleanup batch 2.
- Authority documents:
  - `system-design/strategic-world-runtime-architecture.md`
  - `system-design/presentation-ui-layout-architecture.md`
  - `system-design/world-battle-entry-architecture.md`

The accepted architecture says strategic runtime/Application owns army movement state and battle-ready paths. Presentation may submit intent and display feedback, but it must not be the durable writer of `WorldArmyState` command facts such as target site, destination, intent, status, approach metadata, or navigation path cache.

No design proposal is required for this batch because the target authority is already accepted.

## Scope

- Add an Application service that applies strategic army command state transitions to `WorldArmyState`.
- Move selected-army move-to-position, reinforce-site, assault-site, and unsupported-assault reset writes out of `StrategicWorldRoot`.
- Move post-expedition command metadata writes, including navigation path cache and site approach metadata, out of `StrategicWorldRoot`.
- Move post-command site navigation point re-resolution writes out of `StrategicWorldRoot`; Presentation may still compute the resolved points from current scene data, but Application applies the resulting state changes.
- Move selected-army commandability validation for ownership and blocking statuses into the Application command service; Presentation may still pre-filter for local UX.
- Keep Presentation responsible only for pointer handling, selected-army lookup, current visual navigation-point resolution, user notices, and refresh.
- Add regression guards so `StrategicWorldRoot` delegates command-state writes to the Application service.

## Non-Goals

- Do not move site visual footprint or approach-point resolution in this batch; it still depends on Presentation map authoring and will be a later strategic navigation-authority batch.
- Do not generalize Bonefield-only battle request construction in this batch.
- Do not refactor `WorldSiteRoot` battle-preparation draft mutation in this batch.
- Do not make `WorldArmyState` immutable; Domain state remains mutable during migration.
- Do not change player-facing strategic command behavior.

## Touched Systems

- `src/Application/World/` strategic army command service and result contracts.
- `src/Presentation/World/StrategicWorldRoot*.cs` command call sites.
- `tests/WorldSiteDeploymentCacheRegression` architecture/behavior regression coverage.
- Implementation proposal index.

## Tests

- Application service regression covers:
  - move-to-position clears target site, sets destination, intent, status, clears approach metadata, and applies path cache.
  - command-to-site sets target site, destination, intent, status, arrival approach, approach direction, and path cache.
  - reset unsupported assault clears intent/path and returns the army to idle.
  - resolved site navigation points can update world position, destination, approach metadata, and invalidate old path cache through the Application service.
  - created-expedition command metadata applies path and approach state through the Application service.
  - selected-army commands reject armies that are not owned by the expected player faction or are in non-commandable statuses.
- Presentation architecture regression covers:
  - `StrategicWorldRoot` uses the new Application service.
  - selected-army move and site command method bodies do not directly assign durable `WorldArmyState` command fields.
  - expedition creation does not directly assign durable `WorldArmyState` command metadata after Application creates the army.
  - unsupported assault handling delegates army reset instead of directly mutating army state.
  - site navigation point re-resolution delegates state writes instead of mutating army command facts directly.

## Diagnostics

- Application service logs low-noise command application events with command kind, army count, destination/target, and intent.
- Unsupported assault reset logs the army id and target site through the Application service.

## Manual QA

No Godot editor launch is required for this batch unless automated checks expose an interaction-only issue. Optional manual QA: select an army, right-click map to move, right-click friendly site to reinforce, right-click configured hostile site to assault, and confirm movement/battle entry behavior is unchanged.

## Review Corrections

- 2026-06-13: Read-only architecture review found remaining Presentation command-state writes after expedition creation. The batch scope now routes post-expedition navigation path and approach metadata through `WorldArmyCommandService`.
- 2026-06-13: Read-only architecture review found Application command validation was too thin. The command service now rejects armies outside the expected player faction and statuses that are not commandable.

## Acceptance Evidence

- 2026-06-13: RED verification: `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal` failed before the review fix because `ApplyCreatedExpeditionCommandState` and `requiredOwnerFactionId` validation were missing.
- 2026-06-13: `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal` passed. Existing nullable/source-generator warnings remain outside this batch.
- 2026-06-13: `dotnet run --project tests\WorldArmyMovementRegression\WorldArmyMovementRegression.csproj -v:minimal` passed. Existing source-generator warning remains outside this batch.
- 2026-06-13: `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal` passed. Existing nullable/source-generator warnings remain outside this batch.
- 2026-06-13: `dotnet build rpg.csproj -maxcpucount:2 -v:minimal` passed with 0 warnings and 0 errors.
- 2026-06-13: `git diff --check -- <batch-2-files>` passed with only the existing CRLF normalization warning for `StrategicWorldRoot.BattleEntry.cs`.
