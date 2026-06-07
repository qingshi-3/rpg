# Battle Runtime Fullscreen Hero HUD Implementation Proposal

Status: Archived - implementation record
Created: 2026-06-07
Implemented: 2026-06-07
Verified: 2026-06-07 automated checks passed for related scope; manual QA pending
Archived: 2026-06-07

Originating Design Discussion: live conversation on battle-start UI optimization.
Requirement Id: `battle-runtime-fullscreen-hero-hud`

Authority Documents:
- `gameplay-design/details/combat-command/README.md`
- `gameplay-design/vertical-slices/first-playable-slice.md`
- `system-design/presentation-ui-layout-architecture.md`
- `system-design/battle-command-architecture.md`

## Goal

Implement the first battle-start runtime HUD as a fullscreen battlefield view with a persistent hero combat frame and a tactical-pause command layer.

## Boundary

- Presentation/UI owns the authored HUD resource nodes, layout, labels, buttons, cooldown presentation, and command feedback.
- Runtime remains the authority for battle state, skill command acceptance, cooldown/use state, damage, death, and outcome.
- The HUD may read the active battle request, runtime controller state, selected command group, and command availability hints.
- The HUD may submit `CommandRequest` for the selected hero skill through the existing command path.

## Current UI Cleanup Requirements

Remove from the active battle-runtime player flow:

- the left `BattleRuntimeCommandPanel` under `SitePeacetimePanel`;
- hero/corps/combined command lists shown as large textual lists;
- runtime viewport shrinking because a left management panel is visible;
- post-start command UI that reads like a command encyclopedia.

Keep or migrate:

- existing `Space` tactical pause behavior;
- existing selected hero-company highlight;
- existing targeted hero skill selection and map range/target overlay;
- existing command request submission and runtime rejection feedback.

## Scope

- Make battle runtime use the full world viewport; no left management panel during live battle or tactical pause.
- Add an authored persistent hero combat frame under the runtime HUD:
  - avatar placeholder;
  - hero name and compact state;
  - health bar;
  - mana bar;
  - selected hero skill icon button with MOBA-like cooldown overlay;
  - regroup button for the first live-intervention command surface.
- Keep the frame visible during live battle.
- During tactical pause, expand only the bottom command layer enough to make selected-company commands usable.
- Clicking the hero skill while live should enter tactical pause first, then start target picking.
- Keep skill targeting feedback on the map through existing highlight overlay.
- Do not introduce long text panels or broad command lists in this slice.

## Non-Goals

- No new Runtime command semantics beyond existing hero skill path and the first regroup surface.
- No individual soldier control, RTS box selection, or AP/turn flow.
- No final art pass or inventory/equipment UI.
- No second source of hero HP/mana truth; V0 may display derived/request-backed values until Runtime exposes richer hero resource state.

## Expected Implementation

- Update `WorldSitePeacetimeHud.tscn` to replace the current `BattleRuntimeCommandBar` body with an authored `BattleRuntimeHeroFrame`.
- Remove the `BattleRuntimeCommandPanel` subtree from `SitePeacetimePanel`.
- Bind the hero frame in `WorldSiteRoot.SiteManagementHud.cs`.
- Refactor `WorldSiteRoot.BattleRuntimeCommandHud.cs` so:
  - selected group state refreshes the frame;
  - the old hero/corps/combined list binders are not used;
  - live skill clicks toggle tactical pause and begin target picking;
  - pause presentation never shows the left primary panel.
- Keep player-facing battle HUD text Chinese and compact.

## Tests

Primary verification:

```powershell
dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal
dotnet build rpg.csproj -maxcpucount:2 -v:minimal
dotnet build-server shutdown
```

Regression additions:

- runtime scene has no `BattleRuntimeCommandPanel`, `BattleRuntimeHeroCommandList`, `BattleRuntimeCorpsCommandList`, or `BattleRuntimeCombinedCommandList`;
- runtime scene has an authored `BattleRuntimeHeroFrame` with hero name, HP bar, mana bar, skill button, cooldown overlay, and regroup button;
- `WorldSiteRoot` does not bind left-panel runtime command nodes;
- pause presentation keeps the left primary panel hidden and does not reserve side-panel viewport space;
- skill button entry goes through the hero frame and starts tactical pause before target picking.

## Diagnostics

Add low-noise logs only for state transitions:

- `BattleRuntimeHeroFrameRefreshed group=... skillReady=...`
- `BattleRuntimeHeroSkillPressed group=... pause=...`
- `BattleRuntimeRegroupPressed group=...`

## Manual QA

1. Enter the battle from the normal deployment flow.
2. The battlefield fills the screen; no left runtime panel appears.
3. The hero combat frame stays visible.
4. HP/mana bars and skill cooldown state are readable without opening a panel.
5. Pressing Space pauses the battle and keeps command input interactive.
6. Clicking the skill while live pauses the battle and starts target selection.
7. Target selection shows range and valid target feedback on the map.
8. Resuming battle returns to the minimal frame.

## Stop Conditions

Stop and return to design if implementation requires:

- UI-owned battle state or cooldown truth;
- restoring a left runtime command panel;
- adding a large log/description panel as the primary feedback surface;
- Runtime combat changes unrelated to HUD behavior.
