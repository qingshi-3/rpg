# Strategic Battle Launch Snapshot Sync

Status: Implemented - Automated Verification Passed

## Authority

- `gameplay-design/content-systems-long-term-design.md`
- `system-design/strategic-battle-bridge-architecture.md`
- `system-design/battle-runtime-architecture.md`
- `system-design/battle-result-settlement-architecture.md`

## Scope

Fix the Strategic Management battle launch path so the Runtime consumes a launch snapshot that includes the final battle-preparation request facts: deployed player companies, enemy forces, placements, objective zones, navigation context, and plans.

The active bridge context remains the handoff authority. The legacy request is used only as the current presentation-preparation adapter while that UI slice is still migrated.

## Non-Goals

- Do not change battle rules, encounter content, AI behavior, settlement formulas, or Strategic Management result writeback.
- Do not revive legacy autobattle or legacy request/result authority.
- Do not add a new persistent battle state model.

## Touched Systems

- Strategic Battle Bridge active context launch handoff.
- World site battle Runtime adapter.
- Strategic Management / world-site regression tests.

## GodotPrompter Skills

- `godot-debugging`
- `godot-testing`
- `csharp-godot`

## Tests

- Add a regression test proving Strategic active-context launch refreshes its Runtime snapshot from the final preparation request while preserving bridge session identity.
- Run the focused regression suite that covers Strategic Management bridge behavior.

## Diagnostics

Launch logs should make the Runtime snapshot source clear enough to distinguish early active-context snapshots from synchronized launch snapshots.

## Manual QA

Start a Bonefield assault from Strategic World, deploy the hero company, select an objective and engagement rule, launch battle, and confirm the scene stays in battle Runtime instead of immediately entering aftermath.

## Acceptance

- Runtime launch snapshot has both player and enemy battle groups after deployment.
- Runtime snapshot keeps the active bridge session battle id and snapshot id.
- Strategic participant IDs remain mapped to hero/corps identities.
- Battle does not settle until Runtime produces a complete result from the launch snapshot.

## Verification Evidence

- `dotnet run --project tests\StrategicManagementRegression\StrategicManagementRegression.csproj -v:minimal`
- `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal`
- `dotnet build rpg.csproj -maxcpucount:2 -v:minimal`

Latest diagnostic evidence showed `StrategicBattleLaunchSnapshotSynced ... groups=6` followed by Runtime actor starts for both enemy actors and the deployed strategic participant actors.
