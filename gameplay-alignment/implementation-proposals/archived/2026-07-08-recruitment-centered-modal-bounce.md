# Recruitment Centered Modal Bounce Implementation Proposal

Status: Archived By User Request - Implemented; Manual QA Not Retained As Active Work, Presentation Suite Blocked By Unrelated Taxonomy Guard

## Origin

- Requirement: UI-RECRUIT-MODAL-BOUNCE-001
- Authority:
  - `gameplay-design/content-systems-long-term-design.md`
  - `system-design/presentation-ui-layout-architecture.md`
- Parent Implementation Proposal: `gameplay-alignment/implementation-proposals/2026-07-08-site-management-fullscreen-tab-rail.md`
- Blocking Issues: None known.

## Requirement

Make the entered-city recruitment workbench use a centered-modal q-bounce transition rather than the left drawer transition. Clicking the recruitment tab should retract the tab first, then fade in the dim backdrop while the large modal emerges from a small point tight to the left screen edge, leaves a short translucent trail, accelerates as it scales up toward center, overshoots slightly to the right, and settles. Closing should reverse toward the left-side source point, then return the left tab rail.

## Scope

- Add a focused Presentation animator for centered management modals.
- Apply it only to `MilitaryWorkbenchBackdrop` and `MilitaryWorkbenchPanel`.
- Move workbench panel/backdrop visibility ownership out of `StrategicMilitaryWorkbenchBinder`; the binder remains a content binder.
- Keep tab-rail hiding/return behavior aligned with the existing site-management q-bounce interaction.
- Add regression guards for the centered-modal animator and binder visibility boundary.

## Non-Goals

- Do not change recruitment costs, refund settlement, command validation, or Strategic Management state.
- Do not restyle the recruitment cards or resize the workbench in this slice.
- Do not convert every project modal in this slice.
- Do not change strategic-world selected-location bottom-sheet behavior.

## Touched Systems

- `gameplay-alignment/implementation-proposals/README.md`
- `gameplay-alignment/implementation-proposals/2026-07-08-recruitment-centered-modal-bounce.md`
- `src/Presentation/World/Sites/`
- `tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegressionCases.PresentationAntiRot.cs`

## GodotPrompter Skills

- `godot-ui`
- `responsive-ui`
- `csharp-godot`

## Tests

- `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg`
- `dotnet build rpg.csproj -maxcpucount:2 -v:minimal`
- `git diff --check`

Known unrelated blockers may keep the full Presentation suite nonzero. The recruitment centered-modal guard must pass.

## Manual QA

- Enter a player-held city.
- Hover recruitment tab, click it, and confirm the tab retracts before the centered modal opens.
- Confirm the backdrop fades in and the modal starts as a small point tight to the left edge, shows a short translucent trail, accelerates as it scales up, overshoots slightly to the right, then settles centered.
- Close the modal and confirm it bumps right slightly, retracts left/fades, and the left tab rail pops back.
- Select heroes and recruit/replace corps after the animation to confirm content binding still refreshes normally.

## Acceptance

- The workbench remains a large centered modal under `ModalHost`.
- Opening uses backdrop fade, edge-tight left-side point start, transient non-input trail afterimages, accelerating scale-in, small right overshoot, and settle-back motion.
- Closing uses reverse right bump, leftward fade/retract, hidden cleanup, and tab-rail return.
- `StrategicMilitaryWorkbenchBinder` does not directly show or hide panel/backdrop visibility.
- Focused Presentation guards pass, with only documented unrelated blockers accepted.

## Verification Evidence

- 2026-07-08: The Presentation guard was verified red before implementation. `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg` failed on the new centered recruitment modal animator requirement because `SiteManagementCenteredModalAnimator.cs` did not exist.
- 2026-07-08: After implementation, `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg` passed the task-specific recruitment workbench guard, including centered modal animator ownership, tab-retract delayed open, backdrop fade, position/scale/modulate tweening, reverse close, rail return, and binder removal of direct panel/backdrop visibility writes. The suite still exits nonzero only on the unrelated `TileSets expected=0 actual=2` resource taxonomy guard.
- 2026-07-08: `dotnet build rpg.csproj -maxcpucount:2 -v:minimal` passed with 0 warnings and 0 errors after the implementation.
- 2026-07-08: Follow-up guard was verified red for the revised motion direction. The task guard failed until the modal used `ModalLeftEntryPixels`, a point-scale left start, and an EaseIn scale tween to the overshoot scale.
- 2026-07-08: After the left-source revision, `dotnet build rpg.csproj -maxcpucount:2 -v:minimal` passed with 0 warnings and 0 errors, and `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg` passed the task-specific recruitment workbench guard. The suite still exits nonzero only on the unrelated `TileSets expected=0 actual=2` resource taxonomy guard.
- 2026-07-08: Follow-up guard was verified red for edge-tight origin and trail VFX. After implementation, the recruitment guard passed with `ResolveLeftEdgeStartPosition`, `ModalLeftEdgeNudgePixels`, transient non-input trail afterimages, and automatic trail cleanup.
