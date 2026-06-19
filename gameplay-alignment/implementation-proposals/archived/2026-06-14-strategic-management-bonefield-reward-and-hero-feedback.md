# Strategic Management Bonefield Reward And Hero Feedback Implementation Proposal

Status: Implemented - Amended By Direct Battle Trigger Entry

## Origin And Authority

- Gameplay authority:
  - `gameplay-design/vertical-slices/first-playable-slice.md` VS-03, VS-12, VS-13, VS-14, VS-15, and VS-16.
  - `gameplay-design/content-systems-long-term-design.md`.
  - `gameplay-design/details/progression-and-equipment/README.md`.
  - `gameplay-design/details/heroes-and-corps/README.md`.
- System authority:
  - `system-design/strategic-management-system-architecture.md`.
  - `system-design/strategic-battle-bridge-architecture.md`.
- Depends on:
  - `gameplay-alignment/implementation-proposals/2026-06-14-strategic-battle-bridge-identity-writeback.md`
- Amended by:
  - `gameplay-alignment/implementation-proposals/2026-06-14-direct-battle-trigger-entry.md`

## Goal

Close the first-slice strategic loop after Bonefield battle resolution by recording and showing clear world, reward, hero, progression, and equipment feedback from Strategic Management state.

## Scope

- Add first-slice strategic reward and equipment sample definitions for Bonefield.
- Add a lightweight Strategic Management battle-result feedback record keyed by resolved expedition/session.
- When `ApplyBattleResultSummary` resolves Bonefield, record:
  - outcome and target control feedback;
  - participant corps strength/loss feedback;
  - one hero reaction line based on outcome and selected hero;
  - a visible reward or unlock;
  - at least one named equipment sample and the remaining sample set for first-slice visibility.
- Apply small strategic rewards on victory through Strategic Management commands/state only.
- Expose latest battle feedback through Strategic Management view models.
- Include that strategic feedback in the existing post-battle return notice.
- Keep player-visible text in Chinese.

## Non-Goals

- Do not build full inventory, equipment assignment, random affixes, item comparison, or hero relationship systems.
- Do not make rewards a generic scripting/effect framework.
- Do not change battle Runtime simulation, AI, movement, damage, skills, or target selection.
- Do not fabricate Runtime-only facts such as exact skill timing or individual unit events.
- Do not add a full report panel or new Godot UI resource unless existing text surfaces cannot show the information.

## Touched Systems

- `src/Definitions/StrategicManagement/*`
- `src/Domain/StrategicManagement/*`
- `src/Application/StrategicManagement/*`
- `src/Presentation/World/Sites/WorldSiteRoot.BattleRuntime.cs`
- `tests/StrategicManagementRegression/Program.cs`
- `tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegressionCases.Architecture.cs`

## Tests

```powershell
dotnet run --project tests\StrategicManagementRegression\StrategicManagementRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg
```

```powershell
dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg
```

```powershell
dotnet build rpg.csproj -maxcpucount:2 -v:minimal
```

## Diagnostics

Strategic result writeback should log low-noise accepted command details. The recorded feedback should be inspectable from Strategic Management state and view models without reading legacy world state.

## Manual QA

Optional after automated verification: send one or more hero companies to Bonefield, resolve victory and defeat paths, and confirm the returned notice includes world outcome, reward/unlock, hero reaction, corps loss/progression, and a named equipment sample.

## Acceptance Evidence

- `dotnet run --project tests\StrategicManagementRegression\StrategicManagementRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg`
  - Passed on 2026-06-14.
  - Covers Bonefield reward/equipment/hero feedback records, defeat feedback, dashboard feedback exposure, duplicate expedition reward prevention, null participant rejection, and existing Strategic Management result writeback.
  - Known Godot source-generator warning appeared in the test project: `GodotProjectDir is null or empty`.
- `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg`
  - Passed on 2026-06-14.
  - Covers post-battle return notice consumption of Strategic Management feedback without adding a full report panel.
  - Known Godot source-generator warning and pre-existing nullable warnings appeared in the test project.
- `dotnet build rpg.csproj -maxcpucount:2 -v:minimal`
  - Passed on 2026-06-14 with 0 warnings and 0 errors.

## Review Follow-Up

- Superseded session preparation binding: `2026-06-14-direct-battle-trigger-entry` removed strategic-preparation metadata from result summaries and Strategic Management feedback.
- Fixed one-time Bonefield rewards: state records claimed battle reward ids so simultaneous or repeated successful expeditions can resolve without granting the same target reward twice.
- Fixed malformed participant summary handling: null participant lists or null participant entries are rejected before state mutation.

Residual migration risks remain outside this slice:

- `WorldSiteRoot` still runs the legacy world result applier beside Strategic Management writeback as part of the accepted bridge-migration compatibility path.
- Strategic Management definitions still reuse first-slice battle unit ids currently hosted under `Rpg.Application.World.FirstSliceHeroCompanyIds`; removing that dependency should be handled by a focused content-id extraction slice.
