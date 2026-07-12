# Battle Plan State Machine Implementation Proposal

Status: Archived - historical plan-state implementation record; enemy tactical-region behavior superseded by 2026-05-29 authority
Created: 2026-05-23

Originating Design Proposal:
- `design-proposals/archived/2026-05-23-battle-plan-state-machine`

Amendment Design Proposals:
- `design-proposals/archived/2026-05-29-enemy-region-directed-combat-ai`

Requirement Id:
- REQ-BATTLE-PLAN-STATE-MACHINE-2026-05-23

Authority Documents:
- `gameplay-design/content-systems-long-term-design.md`
- `gameplay-design/details/combat-command/README.md`
- `system-design/hero-led-light-rts-system-architecture.md`
- `system-design/world-battle-entry-architecture.md`
- `system-design/presentation-ui-layout-architecture.md`
- `system-design/battle-command-architecture.md`
- `system-design/battle-runtime-architecture.md`
- `system-design/battle-navigation-topology-architecture.md`
- `system-design/battle-ai-boundary-architecture.md`
- `system-design/battle-group-tactical-region-architecture.md`
- `system-design/semantic-map-marker-architecture.md`

## 2026-05-29 Authority Amendment Note

Enemy objective-plan defaults recorded in Phase 3 and Phase 4 are historical implementation evidence, not the final enemy AI authority. The accepted `design-proposals/archived/2026-05-29-enemy-region-directed-combat-ai` proposal now requires enemy non-engaged movement to use battle-group-owned fixed or temporary target regions, engaged combat to use bounded local combat regions, and player groups to remain command-controlled. Do not use the older `nearest player deployment zone + AttackFirst` wording as future enemy AI design authority.

## Goal

Implement the first authoritative slice of battle-group planning so movement is driven by player-authored battle-group intent before local target acquisition.

The target runtime shape is:

```text
BattleGroupPlan
-> battle-group objective and engagement rule
-> objective-first movement
-> local perception and sticky target lock
-> target-specific attack-slot approach only when needed
-> return/hold/retreat/protect behavior from the active rule
```

## Scope

Phase 1 must establish the core contracts and runtime behavior without requiring the full polished battle-preparation UI:

1. Add data contracts for `BattleGroupPlan`, `BattleObjectiveZone`, `BattleEngagementRule`, and battle-group plan state.
2. Carry plan facts through battle snapshots or runtime initialization with sensible default plans when UI has not authored them yet.
3. Add battle-group runtime plan state and low-noise plan-transition diagnostics.
4. Route default assault target acquisition through the plan's engagement rule and local perception guardrails.
5. Add or extend regression tests proving that moving units do not globally rescore every enemy every movement boundary under default plan execution.

Later phases:

1. Author and extract `ObjectiveZone` semantic markers.
2. Add battle-preparation UI for hero/corps deployment, objective selection, and rule selection.
3. Add objective-zone route fields and route previews.
4. Expand rule-specific behavior for hold, retreat-first, and protect-hero.

## Non-Goals

- No individual soldier micro.
- No freeform physics movement.
- No Presentation-side pathfinding or target choice.
- No full UI polish pass in the first runtime slice.
- No campaign settlement rewrite.
- No hidden fallback that creates competing battle snapshots or UI-owned unit pools.

## Touched Systems

- Runtime battle snapshots and battle-group initialization.
- Runtime actor and battle-group state-machine data.
- Default target acquisition and movement continuation.
- Battle navigation tests and performance counters.
- Later UI and semantic marker systems after the core runtime slice is stable.

## Diagnostics

Add low-noise diagnostics for:

- accepted or defaulted battle-group plan;
- plan state transition when it changes movement or target choice;
- target reacquisition reason under an engagement rule;
- objective/path fallback failure.

Diagnostics must not log per frame, per search node, or every unchanged plan-state tick.

## Verification

Run after implementation:

```powershell
dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal
dotnet run --project tests\BattleHitFeedbackRegression\BattleHitFeedbackRegression.csproj -v:minimal
dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal
dotnet build rpg.csproj -maxcpucount:2 -v:minimal
dotnet build-server shutdown
```

## Acceptance

Implementation is acceptable when:

- battle snapshots/runtime can carry a battle-group plan or generate an explicit default plan;
- runtime has battle-group plan state separate from low-level actor action phases;
- engagement rules influence target acquisition and movement without bypassing action locks;
- default plan execution keeps target acquisition sticky during movement and bounds flow-field builds in regression tests;
- existing battle, hit-feedback, and deployment-cache regressions pass.

## Implementation Evidence

- Added snapshot contracts:
  - `BattleEngagementRule`
  - `BattleGroupPlanSnapshot`
  - `BattleObjectiveZoneSnapshot`
  - `BattleStartSnapshot.ObjectiveZones`
  - `BattleGroupSnapshot.Plan`
- Added runtime plan state:
  - `BattleGroupPlanRuntimeState`
  - corps actors now carry engagement rule, objective-zone anchor, objective size, and current plan state.
- Runtime resolves explicit default plans for legacy snapshots. This attack-first legacy fallback is implementation evidence only; future enemy behavior must follow the 2026-05-29 battle-group tactical-region authority.
- Runtime emits low-noise plan facts:
  - `BattleGroupPlanAccepted`
  - `BattleGroupPlanStateChanged`
- Added `AdvanceTowardObjective` as a runtime AI action. This lets a battle group advance toward its objective without inventing a target actor.
- Added cached objective-zone movement fields through `BattleFlowFieldCache.GetOrBuildObjective` and `BattleCrowdMovementPlanner.FindNextStepCandidatesTowardObjective`.
- Updated target acquisition so battle groups with an authored objective use local, plan-scoped sensing instead of global attack-slot scoring while marching.
- Split runtime resolver plan and diagnostic helpers into partial files to keep oversized-file guards green.
- Added regression coverage:
  - move-first plans advance toward objective before distant enemies;
  - plan-scoped objective movement does not scan far enemy attack slots.

## Verification Evidence

- Passed: `dotnet run --project tests/TargetBattleArchitectureRegression/TargetBattleArchitectureRegression.csproj -v:minimal`
- Passed: `dotnet run --project tests/BattleHitFeedbackRegression/BattleHitFeedbackRegression.csproj -v:minimal`
- Passed: `dotnet run --project tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegression.csproj -v:minimal`
- Passed: `dotnet build rpg.csproj -maxcpucount:2 -v:minimal`
- Passed: `dotnet build-server shutdown`

Known warnings during test runs:

- Existing Godot source-generator warning in test projects: `Property 'GodotProjectDir' is null or empty.`
- Existing nullable warnings in `TargetBattleArchitectureRegression`.

## Phase 2 UI Contract Evidence

- Added request-level planning fields:
  - `BattleStartRequest.ObjectiveZones`
  - `BattleStartRequest.PlayerBattleGroupPlan`
- Battle preparation now exposes target-area choices and engagement-rule choices before the Start Battle action.
- The selected objective zone is written back into the same `BattleStartRequest` used for deployment and runtime activation.
- `LegacyBattleStartSnapshotAdapter` copies objective zones into `BattleStartSnapshot.ObjectiveZones`.
- `BattleGroupSessionProbeService` copies the player battle-group plan into player `BattleGroupSnapshot.Plan` entries and does not copy it to enemy groups.
- Runtime regression now covers plans that carry only `ObjectiveZoneId` plus snapshot `ObjectiveZones`, proving runtime resolves the movement anchor from snapshot facts rather than UI state.

## Phase 2 Marker-Backed UI Correction

- Replaced the abstract generated target buttons with a `ModalHost` tactical objective selector.
- The selector shows a simplified whole-map thumbnail from TileMapLayer-derived grid data, including land/water color blocks.
- Objective choices are marker-backed: dedicated `ObjectiveZone` markers take precedence, while current V0 assault maps can expose enemy-side `DeploymentZone` markers as visible target regions until dedicated objective markers are authored.
- The modal workflow is company-first: select the player battle group / hero company, then click a target region marker. The selected plan is stored per player battle group and carried through `BattleStartRequest.PlayerBattleGroupPlans`.
- Launch validation now fails when a required player battle group has no target marker selection instead of silently choosing a fabricated default.

## Phase 3 Enemy Objective Plan Correction

This section records historical implementation evidence for the older objective-plan default. It is superseded for future enemy behavior by the 2026-05-29 battle-group tactical-region authority.

- Added enemy-side plan request fields:
  - `BattleStartRequest.EnemyBattleGroupPlan`
  - `BattleStartRequest.EnemyBattleGroupPlans`
- `BattleGroupSessionProbeService` now resolves battle-group plans by side, so enemy plans are copied into enemy snapshots without inheriting player objective choices.
- Battle preparation now creates deterministic enemy direct-sortie defaults when no enemy plan is authored:
  - target zones come from player-side deployment markers;
  - each enemy battle group chooses the nearest player deployment zone from its prepared placement;
  - the generated engagement rule is `MoveFirst`, so enemy marching uses objective-first movement and local sensing instead of global target scoring.
- Authored enemy `Hold` plans remain supported through the same request contract. A hold plan without an objective keeps defenders in place and only responds to immediate/local contact.
- Added regression coverage:
  - request-to-snapshot probe copies enemy objective plans without leaking them to player groups;
  - enemy move-first objective plans do not scan far player attack slots while advancing.

## Phase 4 Local Perception Correction

This section records historical implementation evidence for the older `AttackFirst` enemy default. It is superseded for future enemy behavior by the 2026-05-29 battle-group tactical-region authority.

- Promoted the shared local perception radius to `BattlePerceptionPolicy.DefaultLocalPerceptionRange = 4` so Runtime and Presentation debug overlays read the same value.
- Corrected enemy direct-sortie defaults from `MoveFirst` to `AttackFirst`. Enemy groups still advance toward player deployment objectives, but now acquire player units through plan-scoped local sensing before reaching the objective.
- Authored hold plans now wake on local contact through the same plan-scoped sensing path. Player `HoldLine` commands remain fixed and only attack immediate opportunities.
- Added the battle runtime perception debug overlay:
  - perception ranges show by default when battle runtime starts;
  - `E` toggles visibility while battle runtime is active;
  - player perception cells render through `FriendlyPerception`;
  - enemy perception cells render through `EnemyPerception`;
  - overlay cells are footprint-aware and use the same square-grid gap semantics as Runtime target acquisition.
- Added regression coverage:
  - enemy attack-first objective plans sense a local player before continuing to objective;
  - battle runtime perception overlay shares the runtime range policy and is toggled through `Key.E`.

## Objective Flow Field Correction

- Root cause: large authored battle-site topology could report `objective_path_not_found` even when the graph was connected, because the objective flow-field builder counted duplicate priority-queue pops against the topology search limit.
- Runtime now caps objective/target flow expansion by settled unique graph nodes, so far-side deployment objectives remain reachable on the full site topology.
- Added regression coverage for a move-first battle group advancing across a large authored topology toward a marker-backed deployment objective.
- Passed: `dotnet run --project tests/TargetBattleArchitectureRegression/TargetBattleArchitectureRegression.csproj -v:minimal`, including the large authored-topology objective regression.

## Battle Entity Physics Presentation Cleanup

- Battle movement and selection authority remains grid/footprint-driven; Godot `Area2D` and collision shapes must not become movement, sensing, or selection authority for battle units.
- The base battle unit scene should use a plain `Node2D` entity root and should not author an interaction collision circle.
- Defeated-state cleanup should update component state and visuals without disabling collision shapes, because battle units no longer own collision-shape interaction nodes.

## Perception Overlay Style Correction

- Runtime perception cells remain grid/footprint-derived debug information; the style change is presentation-only.
- Friendly and enemy perception layers now use pale low-alpha colors, soft aura tiles, and a dedicated canvas shader for edge glow plus low-noise interior motion.
- The generic tile highlight renderer can configure materials per runtime layer, while combat rules continue to read only authored navigation/grid data.
