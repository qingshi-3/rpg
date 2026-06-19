# Strategic Management Multi-Company Expedition Implementation Proposal

Status: Implemented - Automated Verification Passed

## Origin And Authority

- Prior implementation proposals:
  - `gameplay-alignment/implementation-proposals/2026-06-14-strategic-management-expedition-authority.md`
  - `gameplay-alignment/implementation-proposals/2026-06-14-strategic-battle-bridge-identity-writeback.md`
- Gameplay authority:
  - `gameplay-design/content-systems-long-term-design.md`
  - `gameplay-design/vertical-slices/first-playable-slice.md`
- System authority:
  - `system-design/strategic-management-system-architecture.md`
  - `system-design/strategic-battle-bridge-architecture.md`

The first playable slice requires the player to select named hero companies and send one visible strategic expedition army carrying any available subset from one to three companies. The current Strategic Management expedition stores one hero/corps pair and the large-map draft clamps selection to one company.

## Goal

Promote first-slice expedition formation from a single hero company to one strategic expedition carrying 1-3 Strategic Management hero-company participants, while seeding the start state with three dispatchable first-slice hero companies.

## Scope

- Add the third first-slice Strategic Management hero and seed three starting corps instances assigned to shield, archer, and cavalry-style hero companies.
- Add participant state to `StrategicExpeditionState` while keeping primary `HeroId` / `CorpsInstanceId` as compatibility aliases during migration.
- Add a multi-hero `CreateExpedition(...)` command overload with validation for 1-3 unique dispatchable hero companies.
- Lock every selected hero and corps instance to the created expedition.
- Update Strategic Management dashboards and tests to expect three starting dispatchable hero companies.
- Update the large-map expedition draft to allow 1-3 selected hero companies and create one `WorldArmyState` movement adapter.
- Update `StrategicExpeditionWorldArmyAdapter` and `StrategicBattleBridgeService` to preserve all selected participants through legacy request identity metadata.

## Non-Goals

- Do not implement more than three carried companies.
- Do not add multi-corps-per-hero.
- Do not replace battle-preparation UI internals in this slice.
- Do not add mid-battle reinforcements; carried but undeployed reserves remain a battle-preparation concern already covered by existing deployment tests.
- Do not remove legacy world army or battle request adapters yet.

## Touched Systems

- `src/Definitions/StrategicManagement/*`
- `src/Domain/StrategicManagement/*`
- `src/Application/StrategicManagement/*`
- `src/Application/StrategicBattleBridge/*`
- `src/Application/World/StrategicExpeditionWorldArmyAdapter.cs`
- `src/Presentation/World/StrategicWorldRoot.Expedition.cs`
- `src/Presentation/World/StrategicWorldRoot.ExpeditionHud.cs`
- `tests/StrategicManagementRegression/Program.cs`
- `tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegressionCases.*.cs`

## Tests

Strategic Management and bridge behavior:

```powershell
dotnet run --project tests\StrategicManagementRegression\StrategicManagementRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg
```

Presentation and migration-boundary guard:

```powershell
dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg
```

Final implementation verification:

```powershell
dotnet build rpg.csproj -maxcpucount:2 -v:minimal
```

## Diagnostics

Expedition creation and world-army adapter logs should include expedition id and all selected hero/corps participant ids.

## Manual QA

Optional after automated verification: start an expedition from the player stronghold, select two or three hero companies, send them to Bonefield, and confirm the battle-preparation roster shows the carried companies.

## Acceptance Evidence

- `dotnet run --project tests\StrategicManagementRegression\StrategicManagementRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg`
- `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg`
- `dotnet build rpg.csproj -maxcpucount:2 -v:minimal`

Notes:

- Strategic Management now seeds three dispatchable first-slice hero companies.
- One Strategic Management expedition can carry one to three selected hero-company participants.
- `StrategicExpeditionState.Participants` is the authoritative participant list; `HeroId` and `CorpsInstanceId` remain first-participant compatibility aliases.
- The world-army adapter and Strategic Battle Bridge preserve stable strategic participant identity through legacy battle request metadata, including duplicate battle-unit cases.
