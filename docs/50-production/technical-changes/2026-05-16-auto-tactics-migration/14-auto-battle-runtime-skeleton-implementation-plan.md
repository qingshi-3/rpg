# Auto Battle Runtime Skeleton Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the first pure C# automated battle simulation skeleton that reads `BattleStartRequest`, resolves deterministic combat ticks, emits structured runtime events, and returns `BattleResult.ForceResults`.

**Architecture:** Add an isolated `Rpg.Application.Battle.Auto` runtime with no scene, HUD, AP, `TurnSystem`, or strategic-world mutation dependencies. This slice is simulation-only: it spawns combatants from request forces and preferred placements, advances fixed ticks, resolves target acquisition, simple movement, basic attacks, defeated state, and battle outcome.

**Tech Stack:** Godot 4.5 C#, .NET 8, `BattleStartRequest`, `BattleForceRequest`, `BattleResult`, console regression project under `tests/`.

---

## Required Reading

- `docs/50-production/technical-changes/2026-05-16-auto-tactics-migration.md`
- `docs/50-production/technical-changes/2026-05-16-auto-tactics-migration/04-auto-battle-runtime.md`
- `docs/30-technical-design/world/strategic-world-v1-battle-contract.md`
- `src/Application/Battle/BattleStartRequest.cs`
- `src/Application/Battle/BattleForceRequest.cs`
- `src/Application/Battle/BattleForcePlacementRequest.cs`
- `src/Application/Battle/BattleResult.cs`
- `src/Application/Battle/BattleForceResult.cs`
- `tests/WorldSiteDeploymentCacheRegression/Program.cs`

## Scope Boundaries

- Do not connect the runtime to `WorldSiteRoot`, battle scenes, HUD, camera, animation, or playback UI in this slice.
- Do not mutate `StrategicWorldState`, `WorldSiteState`, or any persistent world object.
- Do not add dependencies on `BattleTurnController`, AP components, manual battle commands, or legacy battle phase ownership.
- Do not create a full ability library or hero skill system in this slice; the event stream should make a later report/skill slice possible.
- Do not invent a second deployment authority. Runtime combatants spawn from `BattleForceRequest.PreferredPlacements`, which must already have been projected from `WorldSiteState.UnitPlacements`.

## File Structure

- Create `src/Application/Battle/Auto/AutoBattleSimulationConfig.cs`
  - Holds deterministic tick, stat, attack range, cooldown, damage, and max-tick tuning.
- Create `src/Application/Battle/Auto/AutoBattleEventKind.cs`
  - Enumerates structured events emitted by the simulation.
- Create `src/Application/Battle/Auto/AutoBattleEvent.cs`
  - Value object for report/playback events.
- Create `src/Application/Battle/Auto/AutoBattleCombatant.cs`
  - Runtime combatant state copied from a force unit and preferred placement.
- Create `src/Application/Battle/Auto/AutoBattleRuntimeState.cs`
  - Runtime snapshot containing combatants, current tick, and outcome.
- Create `src/Application/Battle/Auto/AutoBattleSimulationResult.cs`
  - Bundles final state, event stream, and `BattleResult`.
- Create `src/Application/Battle/Auto/AutoBattleSimulation.cs`
  - Owns spawn, fixed tick advancement, target acquisition, simple movement, basic attack resolution, outcome checks, and force-result building.
- Create `tests/AutoBattleRuntimeRegression/AutoBattleRuntimeRegression.csproj`
  - Console regression project referencing `rpg.csproj`.
- Create `tests/AutoBattleRuntimeRegression/Program.cs`
  - Focused tests for spawning, events, victory/defeat outcome, `ForceResults`, and source guards.

## Task 1: Add Failing Runtime Tests

**Files:**
- Create: `tests/AutoBattleRuntimeRegression/AutoBattleRuntimeRegression.csproj`
- Create: `tests/AutoBattleRuntimeRegression/Program.cs`

- [ ] **Step 1: Create the test project**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\rpg.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Add tests that describe the desired runtime API**

The regression should:

- create an `AutoBattleSimulation` with a deterministic config;
- run a request with two player units and one enemy unit;
- assert combatants spawn from `PreferredPlacements`;
- assert events include battle start, unit spawn, target acquisition, movement, attack, defeat, and battle end;
- assert `BattleResult.Outcome == BattleOutcome.Victory`;
- assert player/enemy `ForceResults` preserve initial, survived, and defeated counts;
- run a second request where the player is outnumbered and assert `BattleOutcome.Defeat`;
- scan `src/Application/Battle/Auto` and assert no source references `BattleTurnController`, `TurnSystem`, `ActionPoint`, or `StrategicWorldState`.

- [ ] **Step 3: Run tests and verify red**

Run:

```powershell
dotnet run --project tests/AutoBattleRuntimeRegression/AutoBattleRuntimeRegression.csproj
```

Expected: compile fails because `Rpg.Application.Battle.Auto` and `AutoBattleSimulation` do not exist yet.

## Task 2: Implement Runtime Value Types

**Files:**
- Create: `src/Application/Battle/Auto/AutoBattleSimulationConfig.cs`
- Create: `src/Application/Battle/Auto/AutoBattleEventKind.cs`
- Create: `src/Application/Battle/Auto/AutoBattleEvent.cs`
- Create: `src/Application/Battle/Auto/AutoBattleCombatant.cs`
- Create: `src/Application/Battle/Auto/AutoBattleRuntimeState.cs`
- Create: `src/Application/Battle/Auto/AutoBattleSimulationResult.cs`

- [ ] **Step 1: Add deterministic tuning config**

`AutoBattleSimulationConfig` should expose `MaxTicks`, `HealthPerUnit`, `AttackDamage`, `AttackRange`, and `AttackCooldownTicks`, clamping invalid values inside the simulation constructor or run path.

- [ ] **Step 2: Add event and runtime state DTOs**

Events must be structured enough for later playback/report work without referencing scene nodes:

```csharp
public sealed class AutoBattleEvent
{
    public int Tick { get; init; }
    public AutoBattleEventKind Kind { get; init; }
    public string ActorId { get; init; } = "";
    public string TargetId { get; init; } = "";
    public string ForceId { get; init; } = "";
    public string UnitDefinitionId { get; init; } = "";
    public int CellX { get; init; }
    public int CellY { get; init; }
    public int CellHeight { get; init; }
    public int Damage { get; init; }
    public int RemainingHealth { get; init; }
    public BattleOutcome Outcome { get; init; } = BattleOutcome.None;
}
```

- [ ] **Step 3: Run focused tests**

Run:

```powershell
dotnet run --project tests/AutoBattleRuntimeRegression/AutoBattleRuntimeRegression.csproj
```

Expected: compile fails because `AutoBattleSimulation` still does not exist.

## Task 3: Implement `AutoBattleSimulation`

**Files:**
- Create: `src/Application/Battle/Auto/AutoBattleSimulation.cs`

- [ ] **Step 1: Add `RunToEnd(BattleStartRequest request)`**

The method should:

- validate `request` is not null;
- spawn one combatant per requested force count;
- require one preferred placement per spawned combatant;
- emit `BattleStarted` and `UnitSpawned` events;
- run fixed ticks until victory, defeat, disaster, or max tick exhaustion;
- return `AutoBattleSimulationResult`.

- [ ] **Step 2: Add unit behavior loop**

For each non-defeated combatant in spawn order:

1. If the current target is missing or defeated, pick the nearest hostile living combatant by Manhattan distance, then by combatant id.
2. Emit `TargetAcquired` when the target changes.
3. If target is outside attack range, move one cell toward it and emit `MovementStarted` and `MovementCompleted`.
4. If target is in range and cooldown is ready, apply basic attack damage and emit `AttackResolved`.
5. If health reaches zero, emit `UnitDefeated`.

- [ ] **Step 3: Add result building**

Build `BattleResult` with:

- `RequestId`, `ContextId`, and `BattleKind` copied from request;
- `Outcome` from runtime outcome;
- one `BattleForceResult` for every player and enemy force;
- `InitialCount`, `SurvivedCount`, and `DefeatedCount` derived from runtime combatants.

- [ ] **Step 4: Run focused tests**

Run:

```powershell
dotnet run --project tests/AutoBattleRuntimeRegression/AutoBattleRuntimeRegression.csproj
```

Expected: all auto battle runtime regression tests pass.

## Task 4: Source Guards And Broader Verification

**Files:**
- Modify only if verification finds a real integration issue.

- [ ] **Step 1: Run focused regression**

```powershell
dotnet run --project tests/AutoBattleRuntimeRegression/AutoBattleRuntimeRegression.csproj
```

Expected: exit code `0`.

- [ ] **Step 2: Run existing migration regressions**

```powershell
dotnet run --project tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegression.csproj
dotnet run --project tests/WorldSiteIntelRegression/WorldSiteIntelRegression.csproj
dotnet run --project tests/BattleHitFeedbackRegression/BattleHitFeedbackRegression.csproj
```

Expected: exit code `0` for each regression.

- [ ] **Step 3: Build the project**

```powershell
dotnet build rpg.csproj -maxcpucount:2 -v:minimal
dotnet build-server shutdown
```

Expected: build exits `0`. The known Godot source generator warning about `GodotProjectDir` may remain in console test projects and does not block this slice.

## Self-Review Checklist

- Runtime code lives under `Rpg.Application.Battle.Auto`.
- Runtime reads `BattleStartRequest` and request `PreferredPlacements`; it does not read or mutate `WorldSiteState`.
- Runtime returns `BattleResult.ForceResults`.
- Runtime emits structured events for report/playback follow-up work.
- Runtime has no scene, HUD, AP, `TurnSystem`, manual command, or strategic-world mutation dependency.
- No legacy battle runtime has been deleted or rewired in this slice.
