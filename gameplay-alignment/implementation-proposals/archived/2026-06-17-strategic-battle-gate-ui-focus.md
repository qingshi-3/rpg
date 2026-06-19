# Strategic Battle Gate UI Focus Implementation Proposal

Status: Accepted And Archived

## Origin And Authority

- User direction: enemy-city arrival is a forced battle decision context. The first popup should briefly identify where and which army is involved, then offer enter battle, view details, or handle later. View details opens a larger centered battle-info panel. Handle later closes the decision UI without retreating the army.
- System authority:
  - `system-design/presentation-ui-layout-architecture.md`
  - `system-design/strategic-battle-bridge-architecture.md`
- Related implementation:
  - `gameplay-alignment/implementation-proposals/2026-06-14-direct-battle-trigger-entry.md`

## Goal

Replace the single-button strategic pre-battle confirmation with a focused two-stage ModalHost flow, and prevent the selected-site bottom sheet from remaining visible while the battle gate owns player focus.

## Scope

- Reauthor `scenes/world/ui/PreBattleDialog.tscn` as a reusable battle-gate UI resource with brief and detail states.
- Add a small presenter/control script for binding battle-gate text and routing button signals.
- Update `StrategicWorldRoot.BattleEntry.cs` to open the brief state first, open the detail state from "查看详情", enter battle from "进入战斗", and defer the decision from "稍后处理".
- Hide the selected-site bottom sheet while a battle gate dialog is open.
- Keep world time pause/resume semantics consistent with the existing battle announcement flow.

## Non-Goals

- Do not change camera, map movement, navigation, army pathing, battle runtime, battle settlement, or scene transition routing.
- Do not make "稍后处理" retreat or cancel the army.
- Do not redesign the battle-preparation scene.

## Tests

```powershell
dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg
```

## Acceptance

- The battle gate resource exposes "进入战斗", "查看详情", and "稍后处理" in the brief state.
- The detail state exposes only "进入战斗" and "稍后处理".
- `ShowPreBattleDialog()` hides the selected-site sheet before opening the modal battle gate.
- "稍后处理" closes the battle gate, moves the arrived assault army aside into a commandable standby move, clears the selected-location context, resumes world time, and does not call retreat/cancel-expedition code.

## Acceptance Evidence

- 2026-06-17: `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg` passed.
- 2026-06-17: `dotnet build rpg.csproj -maxcpucount:2 -v:minimal` passed with 0 warnings and 0 errors.
- 2026-06-17: Added regression coverage for deferred arrived assault standby movement and no bottom-sheet reopen after battle-gate defer.
- 2026-06-18: User accepted the optimized battle-gate UI focus flow and requested archiving.
