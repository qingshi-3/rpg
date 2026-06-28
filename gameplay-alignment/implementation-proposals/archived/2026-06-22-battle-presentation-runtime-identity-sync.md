# Battle Presentation Runtime Identity Sync

Status: Accepted

## Origin

- Requirement: BATTLE-PRESENTATION-RUNTIME-ID-001
- User Report: Battle visuals and logic diverged after the combat refactor; enemies appear stationary while Runtime logs show enemy movement and attacks.
- Design Proposal: None required. This implements current accepted battle Runtime and Strategic Battle Bridge architecture.
- Authority:
  - `system-design/battle-runtime-architecture.md`
  - `system-design/strategic-battle-bridge-architecture.md`
  - `system-design/battle-navigation-topology-architecture.md`

## Scope

- Align battle presentation entity IDs from the authoritative launched `BattleStartSnapshot`, not from request-only force IDs, before live Runtime events are observed.
- Preserve Runtime as combat truth; Presentation must consume movement, damage, and defeat events by Runtime actor ID.
- Keep legacy request-only identity mapping only as a compatibility helper where no launched snapshot exists.
- Add low-noise diagnostics or comments only where they explain the launch-boundary identity contract.

## Non-Goals

- No new enemy AI, movement planner, or navigation behavior.
- No Presentation-authored movement, damage, target choice, or combat fallback.
- No change to strategic result settlement, rewards, or writeback.
- No Godot scene/resource restructuring.

## GodotPrompter Skills

- `csharp-godot`
- `ai-navigation`
- `godot-debugging`
- `godot-testing`

## Tests

- Add a regression that a strategic active battle maps enemy visual force IDs such as `bonefield:f6_spiritwolf:1` to launched Runtime actor IDs such as `bonefield:3`.
- Keep the existing strategic participant mapping regression for player hero companies.
- Run `WorldSiteDeploymentCacheRegression`, `TargetBattleArchitectureRegression`, and `dotnet build`.

## Acceptance

- In a strategic active battle, all player and enemy presentation entities are re-keyed to the Runtime actor IDs emitted by the active `BattleRuntimeSessionController`.
- Runtime movement and damage events for enemies resolve to visual entities instead of being skipped as missing actor/target entities.
- Existing battle and world-site regression suites pass.

## Acceptance Evidence

- `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal`: pass with existing Godot source-generator / nullable warnings.
- `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal`: pass with existing Godot source-generator / nullable warnings.
- `dotnet build rpg.csproj -maxcpucount:2 -v:minimal`: pass with 0 warnings and 0 errors.
- `git diff --check`: pass with existing CRLF normalization warnings.
