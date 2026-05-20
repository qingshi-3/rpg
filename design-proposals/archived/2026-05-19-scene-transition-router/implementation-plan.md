# Scene Transition Router Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Route the current strategic-world, battle-entry, and site-return scene changes through one tested scene transition boundary.

**Architecture:** Add a small `SceneTransitionRouter` in `src/Infrastructure/Scenes/` with an injectable scene gateway so handoff and failure behavior can be tested without booting Godot. Presentation roots keep their existing validation and UI, but submit transition requests to the router instead of directly calling `ChangeSceneToFile`. Preload cache and loading overlay remain out of this first implementation slice.

**Tech Stack:** Godot 4.5 C#, .NET 8, existing static handoff stores (`StrategicWorldRuntime`, `BattleSessionHandoff`), console regression tests under `tests/WorldSiteDeploymentCacheRegression`.

---

### Task 1: Router Contract Tests

**Files:**
- Create: `tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegressionCases.SceneTransitionRouter.cs`
- Modify: `tests/WorldSiteDeploymentCacheRegression/Program.cs`

- [x] **Step 1: Write failing router behavior tests**

Add tests for:

```csharp
WorldSiteDeploymentCacheRegressionCases.Run(
    "scene transition router begins and clears site visit on scene failure",
    WorldSiteDeploymentCacheRegressionCases.SceneTransitionRouterBeginsAndClearsSiteVisitOnSceneFailure);
WorldSiteDeploymentCacheRegressionCases.Run(
    "scene transition router begins battle and cancels handoff on scene failure",
    WorldSiteDeploymentCacheRegressionCases.SceneTransitionRouterBeginsBattleAndCancelsHandoffOnSceneFailure);
WorldSiteDeploymentCacheRegressionCases.Run(
    "scene transition router rejects overlapping transitions",
    WorldSiteDeploymentCacheRegressionCases.SceneTransitionRouterRejectsOverlappingTransitions);
WorldSiteDeploymentCacheRegressionCases.Run(
    "root scene changes are routed through scene transition router",
    WorldSiteDeploymentCacheRegressionCases.RootSceneChangesAreRoutedThroughSceneTransitionRouter);
```

- [x] **Step 2: Run tests to verify RED**

Run:

```powershell
dotnet run --project tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegression.csproj
```

Expected: compile fails because `Rpg.Infrastructure.Scenes.SceneTransitionRouter` does not exist.

### Task 2: Router Core

**Files:**
- Create: `src/Infrastructure/Scenes/ISceneTransitionGateway.cs`
- Create: `src/Infrastructure/Scenes/SceneTransitionResult.cs`
- Create: `src/Infrastructure/Scenes/SceneTransitionRequests.cs`
- Create: `src/Infrastructure/Scenes/SceneTransitionRouter.cs`

- [x] **Step 1: Implement minimal router API**

The router must expose:

```csharp
public bool IsTransitioning { get; }
public SceneTransitionResult EnterSiteDetail(SceneTransitionSiteVisitRequest request);
public SceneTransitionResult EnterBattlePreparation(SceneTransitionBattleRequest request);
public SceneTransitionResult ReturnFromSite(SceneTransitionReturnRequest request);
```

The gateway must expose:

```csharp
Error ChangeSceneToFile(string scenePath);
```

- [x] **Step 2: Run tests to verify GREEN for router behavior**

Run:

```powershell
dotnet run --project tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegression.csproj
```

Expected: router behavior tests pass except the source guard may still fail until presentation roots are migrated.

### Task 3: Presentation Migration

**Files:**
- Create: `src/Infrastructure/Scenes/GodotSceneTransitionGateway.cs`
- Modify: `src/Presentation/World/StrategicWorldRoot.cs`
- Modify: `src/Presentation/World/StrategicWorldRoot.SiteEntry.cs`
- Modify: `src/Presentation/World/StrategicWorldRoot.BattleEntry.cs`
- Modify: `src/Presentation/World/Sites/WorldSiteRoot.cs`
- Modify: `src/Presentation/World/Sites/WorldSiteRoot.SiteManagementHud.cs`

- [x] **Step 1: Add Godot gateway**

`GodotSceneTransitionGateway` adapts a `Func<SceneTree>` to `ISceneTransitionGateway`.

- [x] **Step 2: Add routers to scene roots**

Each root creates one router in its constructor:

```csharp
_sceneTransitionRouter = new SceneTransitionRouter(new GodotSceneTransitionGateway(() => GetTree()));
```

- [x] **Step 3: Migrate strategic site entry**

Replace direct site visit handoff and `ChangeSceneToFile` with:

```csharp
SceneTransitionResult transition = _sceneTransitionRouter.EnterSiteDetail(...);
```

- [x] **Step 4: Migrate strategic battle entry**

Replace direct battle handoff and `ChangeSceneToFile` with:

```csharp
SceneTransitionResult transition = _sceneTransitionRouter.EnterBattlePreparation(...);
```

On failure, keep existing user notice, rollback, and refresh behavior.

- [x] **Step 5: Migrate site return**

Replace `MarkWorldResumeAfterSiteReturn` plus direct `ChangeSceneToFile` with:

```csharp
SceneTransitionResult transition = _sceneTransitionRouter.ReturnFromSite(...);
```

### Task 4: Verification

**Files:**
- Test: `tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegression.csproj`
- Test: `rpg.csproj`

- [x] **Step 1: Run focused regression**

Run:

```powershell
dotnet run --project tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegression.csproj
```

Expected: all WorldSiteDeploymentCacheRegression cases pass.

- [x] **Step 2: Run build**

Run:

```powershell
dotnet build rpg.csproj -maxcpucount:2 -v:minimal
```

Expected: build succeeds.

- [x] **Step 3: Shut down build server**

Run:

```powershell
dotnet build-server shutdown
```

Expected: build servers shut down or report no active server.
