# Battle Movement Performance Implementation Proposal

Status: Archived - accepted with residual runtime-spike follow-up
Created: 2026-05-21
Accepted: 2026-05-21
Implemented: 2026-05-21
Verified: 2026-05-21

Originating Design Proposal: None
Requirement Id: None
Related Design Proposals: None
Authority Documents:
- `system-design/battle-runtime-architecture.md`
- `system-design/battle-navigation-topology-architecture.md`
- `system-design/battle-command-architecture.md`

## Goal

Diagnose and optimize unit movement stutter during live battle without changing gameplay rules, command rules, path legality, attack range, target choice semantics, settlement, or battle outcome authority.

## Architecture Boundary

This implementation works inside accepted battle architecture only:

- Runtime remains the authority for actor anchored cells, pathfinding, target choice, reservations, action timing, damage, and emitted semantic events.
- Presentation remains the owner of visual interpolation, animation, audio, selection, debug overlays, and feedback timing.
- Logging remains diagnostics infrastructure and must not become per-frame or per-search-node output.

No design proposal is required for this work unless implementation reveals that accepted runtime/navigation architecture cannot support the optimization without changing rules.

## Current Evidence

Static diagnosis found three likely cost centers that must be measured separately before optimization is accepted.

### Runtime Computation

Files:
- `src/Runtime/Battle/BattleRuntimeTickResolver.cs`
- `src/Runtime/Battle/BattleRuntimeTickResolver.Targeting.cs`
- `src/Runtime/Battle/Navigation/BattleFlowFieldCache.cs`
- `src/Runtime/Battle/Navigation/BattleFlowFieldBuilder.cs`
- `src/Runtime/Battle/Navigation/BattleCombatSlotAllocator.cs`
- `src/Runtime/Battle/Navigation/BattleCrowdMovementPlanner.cs`

Evidence:

- Each runtime tick builds tick-start facts, dynamic occupancy, and a tick-local `BattleFlowFieldCache`.
- Target scoring evaluates attack opportunity for each decision-ready actor against candidate enemies.
- Flow-field construction calls `BattleCombatSlotAllocator.FindSlots`, which scans graph anchors.
- `ResolveAttackOpportunityTravelCost` calls `BattleCombatSlotAllocator.FindSlots` again after reading the flow field, creating duplicate slot scans for the same actor-target facts.

Hypothesis:

Multi-unit battles pay avoidable `decisionActors * candidateTargets * graphAnchors` work during movement-heavy phases.

### Presentation Cost

Files:
- `src/Presentation/World/Sites/WorldSiteRoot.BattleRuntimeIncremental.cs`
- `src/Presentation/World/Sites/WorldSiteRoot.BattleRuntimePlayback.cs`
- `src/Presentation/World/Sites/WorldSiteRoot.BattleRuntime.cs`
- `src/Presentation/Battle/Entities/BattleUnitRoot.cs`
- `src/Presentation/Battle/Entities/UnitAnimationComponent.cs`
- `src/Presentation/Battle/Entities/BattleUnitAudioComponent.cs`

Evidence:

- Live runtime advances one action-time slice at a time and immediately observes movement events on Presentation.
- `BattleUnitRoot.MoveEntityTo` kills any existing movement tween for the actor before creating a new tween.
- Every committed movement step creates a new Godot `Tween`.
- If a follow-up movement event arrives before the previous visual step settles, the tween is interrupted and restarted from the current visual position.

Hypothesis:

Some "not smooth" movement is visual scheduling churn rather than Runtime pathfinding cost.

### Logging Cost

Files:
- `src/Infrastructure/Logging/GameLog.cs`
- `src/Runtime/Battle/BattleRuntimeTickResolver.cs`
- `src/Presentation/Battle/Entities/BattleUnitRoot.cs`
- `src/Presentation/Battle/Entities/UnitAnimationComponent.cs`
- `src/Presentation/Battle/Entities/BattleUnitAudioComponent.cs`

Evidence:

- `GameLog` uses a global lock and `File.AppendAllText` for each log entry.
- Runtime logs `BattleRuntimeAction` per decision actor except successful attack-charge waits.
- Presentation logs visual movement, animation playback, procedural animation playback, and audio cue playback.

Hypothesis:

Synchronous high-frequency file writes can stall battle presentation on Windows and should be reduced or buffered.

## Scope

Implement one performance workstream with three measured slices:

1. Add low-noise performance counters and summaries that distinguish Runtime computation, Presentation playback, and Logging overhead.
2. Optimize Runtime movement decision work without changing movement legality or target semantics.
3. Optimize Presentation movement playback and high-frequency logging only where counters show measurable cost.

## Non-Goals

- No gameplay rule changes.
- No accepted design or architecture document changes.
- No implementation from `design-proposals/active/`.
- No new pathfinding authority in Presentation.
- No TileMapLayer, water, bridge, height-link, or raw authoring parsing in Runtime pathfinding.
- No conversion to physics, NavigationServer, freeform movement, hexes, or AP/turn systems.
- No balance changes to move speed, attack speed, damage, range, or unit footprint rules.
- No broad logging rewrite outside battle-related high-frequency paths.

## Blocking Issue

Resolved before implementation. `dotnet run --project tests/TargetBattleArchitectureRegression/TargetBattleArchitectureRegression.csproj -v:minimal` compiled and ran; no helper repair was needed in this workstream.

## Implementation Plan

### Phase 1: Measured Baseline

Files:
- Modify: `src/Runtime/Battle/BattleRuntimeSessionController.cs`
- Modify: `src/Runtime/Battle/BattleRuntimeTickResolver.cs`
- Modify: `src/Runtime/Battle/Navigation/BattleFlowFieldCache.cs`
- Modify: `src/Runtime/Battle/Navigation/BattleFlowFieldBuilder.cs`
- Modify: `src/Runtime/Battle/Navigation/BattleCombatSlotAllocator.cs`
- Modify: `src/Presentation/World/Sites/WorldSiteRoot.BattleRuntimeIncremental.cs`
- Modify: `src/Presentation/Battle/Entities/BattleUnitRoot.cs`
- Modify: `src/Infrastructure/Logging/GameLog.cs`
- Test: `tests/TargetBattleArchitectureRegression/TargetBattlePerformanceRegressionCases.cs`

Add counters that can be read by headless tests and optionally summarized in low-noise logs:

- runtime tick count;
- decision-ready actor count;
- flow-field cache hit/miss/build count;
- combat slot scan count;
- graph anchor count scanned for slots;
- movement event count;
- movement tween created count;
- movement tween interrupted count;
- battle log write count by category;
- elapsed milliseconds for runtime advance slices, presentation event observation, and log writes.

Counters must be disabled or near-zero overhead when not requested by tests or diagnostics.

Acceptance:

- A headless regression can run a deterministic many-vs-many battle and report separate Runtime, Presentation-observation, and Logging counters.
- Diagnostics emit one battle summary, not per-frame detail.
- Existing runtime movement event facts remain unchanged.

### Phase 2: Runtime Navigation Optimization

Files:
- Modify: `src/Runtime/Battle/Navigation/BattleFlowField.cs`
- Modify: `src/Runtime/Battle/Navigation/BattleFlowFieldCache.cs`
- Modify: `src/Runtime/Battle/Navigation/BattleFlowFieldBuilder.cs`
- Modify: `src/Runtime/Battle/BattleRuntimeTickResolver.Targeting.cs`
- Modify: `src/Runtime/Battle/BattleRuntimeSessionController.cs`
- Test: `tests/TargetBattleArchitectureRegression/TargetBattlePerformanceRegressionCases.cs`
- Test: existing `tests/TargetBattleArchitectureRegression/*Navigation*` and `*MovementIntent*` cases.

Expected implementation:

- Reuse `BattleFlowField.GoalSlots` for open attack-slot checks instead of calling `BattleCombatSlotAllocator.FindSlots` again for the same actor-target facts.
- Keep cache keys tied to actor footprint, actor attack range, target id, target anchor, and support/attack mode.
- Consider promoting flow-field cache lifetime from tick-local to controller-local only if invalidation facts are explicit: topology identity, target anchor, actor footprint, attack range, command/movement intent, dynamic occupancy revision when occupancy-sensitive data is stored.
- Keep dynamic occupancy out of static flow-field costs unless invalidation includes occupancy revision.

Acceptance:

- Slot scan count decreases in the deterministic performance scenario.
- Movement destinations, target selection, damage events, and final outcomes match pre-optimization expectations in existing regression tests.
- No Runtime code references Godot, Presentation, Domain save state, or raw map authoring concepts.

### Phase 3: Presentation Movement Smoothness

Files:
- Modify: `src/Presentation/World/Sites/WorldSiteRoot.BattleRuntimeIncremental.cs`
- Modify: `src/Presentation/World/Sites/WorldSiteRoot.BattleRuntimePlayback.cs`
- Modify: `src/Presentation/Battle/Entities/BattleUnitRoot.cs`
- Modify: `src/Presentation/Battle/Entities/UnitAnimationComponent.cs`
- Test: `tests/BattleHitFeedbackRegression/BattleHitFeedbackRegressionCases.RuntimePlayback.cs`
- Test: `tests/BattleHitFeedbackRegression/BattleHitFeedbackRegressionCases.BattlePresentation.cs`

Expected implementation:

- If counters show movement tween interruption, serialize or stitch same-actor movement presentation so a follow-up runtime movement event does not abruptly kill a still-valid visual step.
- Runtime cells remain authoritative; Presentation may only adjust interpolation scheduling.
- Preserve current damage-impact ordering: same-tick damage that depends on movement still waits for relevant movement presentation delay.
- Keep `restartMoveAnimation: false` behavior for continuous movement where it avoids animation restart churn.

Acceptance:

- Movement tween interrupted count decreases or reaches zero in continuous movement scenarios.
- Runtime event order and settlement facts are unchanged.
- Attack impact and defeat feedback still occur at runtime impact timing.
- Manual QA confirms units visually continue movement without snapping or stutter in a many-unit battle.

### Phase 4: Logging Throttle Or Buffer

Files:
- Modify: `src/Infrastructure/Logging/GameLog.cs`
- Modify: `src/Runtime/Battle/BattleRuntimeTickResolver.cs`
- Modify: `src/Presentation/Battle/Entities/BattleUnitRoot.cs`
- Modify: `src/Presentation/Battle/Entities/UnitAnimationComponent.cs`
- Modify: `src/Presentation/Battle/Entities/BattleUnitAudioComponent.cs`
- Test: `tests/TargetBattleArchitectureRegression/TargetBattlePerformanceRegressionCases.cs`
- Test: `tests/TargetBattleArchitectureRegression/TargetBattleAttackCadenceRegressionCases.cs`

Expected implementation:

- Introduce category-level suppression or buffering for high-frequency battle logs.
- Keep warnings and errors immediate.
- Keep existing tests that assert required diagnostics, including runtime action diagnostics, by enabling the relevant diagnostic category in test setup or by preserving low-noise summary output.
- Do not log per frame or per path search node.

Acceptance:

- Log write count decreases during movement-heavy battle playback with default settings.
- Required warning/error diagnostics still write immediately.
- Tests that assert battle diagnostics can enable the diagnostic category explicitly.

## Tests

Run after implementation:

```powershell
dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal
dotnet run --project tests\BattleHitFeedbackRegression\BattleHitFeedbackRegression.csproj -v:minimal
dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal
dotnet build rpg.csproj -maxcpucount:2 -v:minimal
dotnet build-server shutdown
```

If a test fails because of the known helper compile issue, fix only that helper mismatch first, then rerun the same command.

## Manual QA

Use a battle with several friendly and enemy corps moving at once.

Check:

- units advance without visible snapping when moving multiple cells over consecutive runtime actions;
- frame time does not spike during mass movement compared with baseline;
- logs do not grow rapidly during ordinary movement;
- command pause/resume still works during battle presentation waits;
- damage numbers, hit reactions, and defeated presentation still align with attack impact timing;
- final battle outcome and report still match runtime facts.

## Acceptance Evidence

Implementation evidence:

- Added `BattlePerformanceCounters` for Runtime tick count, decision-ready actor count, flow-field cache hit/miss/build count, combat slot scan count, scanned anchor count, movement event count, movement tween create/interruption count, log write count, suppressed trace count, Runtime elapsed ticks, and log elapsed ticks.
- Runtime optimization reuses `BattleFlowField.GoalSlots` for open attack-slot checks in target scoring instead of rescanning `BattleCombatSlotAllocator.FindSlots` after the flow-field build.
- Runtime combat slot allocation now enumerates only target-local candidate anchors and still validates each candidate through `BattleNavigationGraph.CanPlaceFootprint` and `BattleActorFootprint.GetGap`. This preserves slot legality and attack/support semantics while removing full-topology scans from the movement decision path.
- High-frequency Presentation movement, animation, procedural animation, and audio diagnostics now use `GameLog.Trace`, which is disabled by default and can be enabled per category.
- Live Presentation now serializes same-actor movement observation through the actor action tail and waits for that actor's movement visual duration before starting the actor's next visual movement step. Runtime simulation slices still advance on the live action clock and are not globally blocked by movement presentation.
- Godot custom performance monitors are registered from code when `WorldSiteRoot` enters a site scene and removed on exit. The monitors reuse the same battle counters that feed headless regression tests and expose Runtime, navigation, movement tween, and logging signals in `Debugger > Monitors`.
- Runtime movement event facts, target semantics, attack range, command lifecycle, settlement, and battle outcome authority were not changed.

Godot FPS monitor screenshot after manual run:

- `FPS=60`
- `Process=2.35 ms`
- `Physics Process=0.18 ms`
- `Navigation Process=0.00 ms`
- `Objects=6359`
- `Resources=584`
- `Nodes=223`
- `Draw Objects=5429`
- `Draw Primitives=12546`
- `Draw Calls=154`
- Conclusion: the captured frame did not show sustained renderer, physics, or Godot NavigationServer pressure. Combined with the custom battle monitor `RuntimeAdvanceMsMax=102`, the remaining stutter evidence points to intermittent battle Runtime navigation spikes rather than steady Presentation rendering or logging cost.

Godot app-log diagnosis after manual run:

- Latest Godot session started at `2026-05-21 01:55:38`.
- In that latest session, high-frequency Presentation logs were no longer written: `Entity visual move=0`, `Animation played=0`, `Audio cue played=0`, `TRACE=0`.
- The same session still reported visible movement stutter/flicker during battle movement.
- Runtime movement cadence emitted many actor movement slices at `0.16s` action intervals. Before the follow-up fix, live movement observation called `MoveEntityTo` immediately for every slice instead of serializing same-actor visual steps, so a follow-up movement event could interrupt a still-active tween.

Counter summary after implementation from `TargetBattlePerformanceRegressionCases.RuntimePerformanceCountersSeparateNavigationAndLoggingCosts`:

- `runtimeTicks=125`
- `decisionActors=304`
- `flowBuilds=223`
- `flowHits=246`
- `flowMisses=223`
- `slotScans=223`
- `slotAnchors=17394`
- `movementEvents=36`
- `logWrites=319`
- `logTicks=464868`
- `runtimeTicksElapsed=798900`

Key acceptance checks:

- `slotScans=223` and `flowBuilds=223`, confirming open attack-slot checks reused flow-field goal slots instead of adding duplicate slot scans.
- Follow-up target-local slot allocation reduced `slotAnchors` in this same deterministic scenario from `17394` to `4522` while keeping `slotScans=223` and `flowBuilds=223`.
- Disabled trace diagnostics were counted as suppressed and did not write by default; explicitly enabled trace diagnostics wrote normally.
- Required Runtime action diagnostics remained `Info` and continued to satisfy existing diagnostic tests.

Large-topology regression evidence:

- Added `TargetBattlePerformanceRegressionCases.RuntimeCombatSlotScansStayBoundedNearTargetOnLargeTopology`.
- Red before optimization: 64x64 topology reported `average=4096`, `scans=8`, `anchors=32768`, proving combat slot scans walked the whole topology.
- Green after optimization: the same regression passed with average scanned anchors below the fixed `128` bound, proving slot scan work is target-local instead of topology-size proportional.

Verification commands:

- Passed: `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal`
- Passed: `dotnet run --project tests\BattleHitFeedbackRegression\BattleHitFeedbackRegression.csproj -v:minimal`
- Passed after strategic cleanup alignment: `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal`
- Passed: `dotnet build rpg.csproj -maxcpucount:2 -v:minimal`
- Passed: `dotnet build-server shutdown`

Follow-up verification after same-actor movement serialization:

- Passed: `dotnet run --project tests\BattleHitFeedbackRegression\BattleHitFeedbackRegression.csproj -v:minimal`
- Passed: `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal`
- Passed: `dotnet build rpg.csproj -maxcpucount:2 -v:minimal`
- Passed: `dotnet build-server shutdown`

Pure-code Godot monitor hookup:

- Registered custom monitor ids:
  - `Battle/RuntimeAdvanceMsLast`
  - `Battle/RuntimeAdvanceMsMax`
  - `Battle/RuntimeTicks`
  - `Battle/DecisionActors`
  - `Battle/FlowFieldBuilds`
  - `Battle/FlowFieldCacheHits`
  - `Battle/FlowFieldCacheMisses`
  - `Battle/CombatSlotScans`
  - `Battle/CombatSlotAnchors`
  - `Battle/MovementEvents`
  - `Battle/MovementTweensCreated`
  - `Battle/MovementTweensInterrupted`
  - `Battle/ActiveMovementTweens`
  - `Battle/LogWrites`
  - `Battle/LogWriteMsLast`
  - `Battle/LogWriteMsMax`
- Counter reset point: `ActivateBattleRuntime()` before live runtime starts.
- Runtime counter owner: `WorldSiteBattleGroupRuntimeAdapter` passes the same `BattlePerformanceCounters` into `BattleRuntimeSession`.
- Verification after monitor hookup:
  - Passed: `dotnet run --project tests\BattleHitFeedbackRegression\BattleHitFeedbackRegression.csproj -v:minimal`
  - Passed: `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal`
  - Passed: `dotnet build rpg.csproj -maxcpucount:2 -v:minimal`
  - Passed: `dotnet build-server shutdown`

Follow-up verification after target-local combat slot allocation:

- Passed: `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal`
  - Updated deterministic summary: `runtimeTicks=125 decisionActors=304 flowBuilds=223 flowHits=246 flowMisses=223 slotScans=223 slotAnchors=4522 movementEvents=36 logWrites=319 runtimeTicksElapsed=831571`.
- Passed: `dotnet run --project tests\BattleHitFeedbackRegression\BattleHitFeedbackRegression.csproj -v:minimal`
- Passed: `dotnet build rpg.csproj -maxcpucount:2 -v:minimal`

Manual QA:

- User reran live battle manual QA after target-local slot allocation, Presentation motion-lane smoothing, and slower movement tuning.
- User reported the movement feel was close enough for this performance workstream to archive.

Residual risk:

- The latest observed custom monitor still showed intermittent Runtime spike potential (`RuntimeAdvanceMsMax` around the 90 ms range before the motion-lane follow-up), with flow-field builds still high. This is no longer blocking the accepted movement-feel slice, but a future Runtime-only optimization can add flow-field searched-node timing if stutter returns.
- Strategic/world-site cleanup guard now passes after removing the obsolete `BattleAlertDialog.tscn` resource.

## Stop Conditions

Stop implementation and return to design/proposal discussion if any optimization requires:

- changing path legality, attack slot semantics, command lifecycle, or target selection rules;
- letting Presentation create movement or combat truth;
- adding a fallback runtime authority for navigation or combat;
- changing accepted battle outcome or settlement facts to hide a performance issue.
