# Legacy Manual Authority Deletion Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remove stale manual tactical battle authority from docs and make the current migration default to the simple auto battle path.

**Architecture:** This is a deletion-first migration slice, not final battle feature work. Delete old player-command/AP/turn-based design documents instead of preserving them as deprecated alternatives, route battle design through the auto tactics migration docs, and default `WorldSiteRoot` to the auto battle path. Follow-up cleanup removes the remaining manual runtime files, scene nodes, and AP authoring fields once scene dependencies are detached.

**Tech Stack:** Godot 4.5 C#, .NET 8, Markdown docs, `WorldSiteRoot`, console regression projects under `tests/`.

---

## Required Reading

- `docs/50-production/technical-changes/2026-05-16-auto-tactics-migration.md`
- `docs/50-production/technical-changes/2026-05-16-auto-tactics-migration/07-legacy-battle-retirement.md`
- `docs/20-game-design/tactical-battle/README.md`
- `docs/30-technical-design/battle/README.md`
- `src/Presentation/World/Sites/WorldSiteRoot.cs`
- `tests/WorldSiteDeploymentCacheRegression/Program.cs`

## Scope Boundaries

- Do not build final playback UI, final report panel, hero skill system, or full scene animation in this slice.
- Do not delete C# classes or `.tscn` nodes still referenced by the current WorldSite scene.
- Do not keep old manual design docs with supersession notes. If they are no longer target authority, delete them.
- Do not expand or restore AP, `TurnSystem`, `BattleActionMenu`, or manual player command behavior.
- Do not make auto battle mutate `StrategicWorldState` directly; writeback still goes through `BattleResult` and the existing applier.

## File Structure

- Delete stale gameplay docs and their local images:
  - `docs/20-game-design/tactical-battle/mechanism-battle-slice.md`
  - `docs/20-game-design/tactical-battle/battle-ui-interaction-review.md`
  - `docs/20-game-design/tactical-battle/enemy-intent-design.md`
  - `docs/20-game-design/tactical-battle/battle-demo-undead-commander.md`
  - `docs/20-game-design/tactical-battle/battle-demo-undead-commander-*`
- Delete stale technical docs:
  - `docs/30-technical-design/battle/battle-action-architecture.md`
  - `docs/30-technical-design/battle/battle-input-command-architecture.md`
  - `docs/30-technical-design/battle/battle-runtime-responsibility-review.md`
  - `docs/30-technical-design/battle/intent-system.md`
  - `docs/30-technical-design/battle/card-system.md`
  - `docs/30-technical-design/battle/targeting-and-preview.md`
- Modify:
  - `docs/20-game-design/tactical-battle/README.md`
  - `docs/30-technical-design/battle/README.md`
  - `docs/30-technical-design/battle/technical-architecture.md`
  - `docs/30-technical-design/battle/battle-scene-architecture.md`
  - `docs/50-production/technical-changes/2026-05-16-auto-tactics-migration.md`
  - `docs/50-production/technical-changes/2026-05-16-auto-tactics-migration/07-legacy-battle-retirement.md`
  - `docs/60-qa/testcases/auto-tactics-migration.md`
  - `src/Presentation/World/Sites/WorldSiteRoot.cs`
  - `tests/WorldSiteDeploymentCacheRegression/Program.cs`

## Task 1: Add Failing Deletion Guards

**Files:**
- Modify: `tests/WorldSiteDeploymentCacheRegression/Program.cs`

- [ ] **Step 1: Add test run lines**

Add:

```csharp
Run("auto tactics migration deletes stale manual battle docs", AutoTacticsMigrationDeletesStaleManualBattleDocs);
Run("world site root defaults to auto battle runtime", WorldSiteRootDefaultsToAutoBattleRuntime);
```

- [ ] **Step 2: Add stale doc deletion guard**

Assert the deleted docs no longer exist and README routes no longer point at them:

```csharp
static void AutoTacticsMigrationDeletesStaleManualBattleDocs()
{
    string root = ProjectRoot();
    string[] deletedDocs =
    {
        Path.Combine(root, "docs", "20-game-design", "tactical-battle", "mechanism-battle-slice.md"),
        Path.Combine(root, "docs", "20-game-design", "tactical-battle", "battle-ui-interaction-review.md"),
        Path.Combine(root, "docs", "20-game-design", "tactical-battle", "enemy-intent-design.md"),
        Path.Combine(root, "docs", "20-game-design", "tactical-battle", "battle-demo-undead-commander.md"),
        Path.Combine(root, "docs", "30-technical-design", "battle", "battle-action-architecture.md"),
        Path.Combine(root, "docs", "30-technical-design", "battle", "battle-input-command-architecture.md"),
        Path.Combine(root, "docs", "30-technical-design", "battle", "battle-runtime-responsibility-review.md"),
        Path.Combine(root, "docs", "30-technical-design", "battle", "intent-system.md"),
        Path.Combine(root, "docs", "30-technical-design", "battle", "card-system.md"),
        Path.Combine(root, "docs", "30-technical-design", "battle", "targeting-and-preview.md")
    };

    foreach (string path in deletedDocs)
    {
        AssertTrue(!File.Exists(path), $"stale manual battle authority should be deleted path={path}");
    }

    string gameplayReadme = File.ReadAllText(Path.Combine(root, "docs", "20-game-design", "tactical-battle", "README.md"));
    string technicalReadme = File.ReadAllText(Path.Combine(root, "docs", "30-technical-design", "battle", "README.md"));
    string combined = gameplayReadme + "\n" + technicalReadme;
    AssertTrue(!combined.Contains("mechanism-battle-slice", StringComparison.Ordinal), "gameplay README should not route to manual mechanism slice");
    AssertTrue(!combined.Contains("battle-ui-interaction-review", StringComparison.Ordinal), "gameplay README should not route to manual action menu review");
    AssertTrue(!combined.Contains("enemy-intent-design", StringComparison.Ordinal), "gameplay README should not route to old turn intent design");
    AssertTrue(!combined.Contains("battle-action-architecture", StringComparison.Ordinal), "technical README should not route to AP action architecture");
    AssertTrue(!combined.Contains("battle-input-command-architecture", StringComparison.Ordinal), "technical README should not route to manual command architecture");
    AssertTrue(!combined.Contains("battle-runtime-responsibility-review", StringComparison.Ordinal), "technical README should not route to manual runtime review");
    AssertTrue(!combined.Contains("intent-system", StringComparison.Ordinal), "technical README should not route to legacy intent system");
    AssertTrue(!combined.Contains("card-system", StringComparison.Ordinal), "technical README should not route to legacy AP card system");
    AssertTrue(!combined.Contains("targeting-and-preview", StringComparison.Ordinal), "technical README should not route to manual targeting preview vocabulary");
}
```

- [ ] **Step 3: Add auto default source guard**

Add:

```csharp
static void WorldSiteRootDefaultsToAutoBattleRuntime()
{
    string rootSource = File.ReadAllText(Path.Combine(ProjectRoot(), "src", "Presentation", "World", "Sites", "WorldSiteRoot.cs"));

    AssertTrue(
        rootSource.Contains("public bool UseAutoBattleRuntime { get; set; } = true;", StringComparison.Ordinal),
        "WorldSiteRoot should default new site battles to the auto battle runtime during migration");
    AssertTrue(
        rootSource.Contains("_autoBattleAdapter.TryResolveActiveBattle", StringComparison.Ordinal),
        "WorldSiteRoot should still delegate auto handoff resolution to WorldSiteAutoBattleAdapter");
    AssertTrue(
        rootSource.Contains("_turnController?.StartBattle()", StringComparison.Ordinal),
        "manual battle start should be removed after scene dependencies are detached");
}
```

- [ ] **Step 4: Run red test**

Run:

```powershell
dotnet run --project tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegression.csproj
```

Expected: fails because stale docs still exist and `UseAutoBattleRuntime` does not default to `true`.

## Task 2: Delete Old Manual Battle Docs

**Files:**
- Delete the files listed in File Structure.
- Modify README and migration docs listed in File Structure.

- [ ] **Step 1: Delete stale files**

Delete only the listed old battle docs and local old demo assets. Do not delete `README.md` or current auto tactics migration docs.

- [ ] **Step 2: Rewrite gameplay battle README routes**

Keep `docs/20-game-design/tactical-battle/README.md` as a short router:

```markdown
# Battle Gameplay Index

This directory is retained for path stability. Current battle design authority lives in the auto tactics migration docs and treats battle as automated tactical validation inside authored `WorldSite` battlefields.

## Routes

- Product/gameplay direction: `../gameplay-direction.md`
- Core loop: `../core-loop.md`
- Auto tactics migration: `../../50-production/technical-changes/2026-05-16-auto-tactics-migration.md`
- Playback and report requirements: `../../50-production/technical-changes/2026-05-16-auto-tactics-migration/05-playback-ui-and-report.md`

## Rule

Do not create new player-facing battle design around AP, player phases, or manual action menus. If a future battle document is needed, write it as automated `WorldSite` tactical validation.
```

- [ ] **Step 3: Rewrite technical battle README routes**

Remove routes to deleted docs and route new work through auto docs:

```markdown
# Battle Technical Index

This directory stores implementation-facing battle references that still support the migration. Current battle runtime authority is the automated tactical validation migration.

## Start Here

- Battle technical architecture: `technical-architecture.md`
- Auto tactics migration: `../../50-production/technical-changes/2026-05-16-auto-tactics-migration.md`
- Auto battle runtime detail: `../../50-production/technical-changes/2026-05-16-auto-tactics-migration/04-auto-battle-runtime.md`
- Playback UI and report detail: `../../50-production/technical-changes/2026-05-16-auto-tactics-migration/05-playback-ui-and-report.md`
- World battle contract: `../world/strategic-world-v1-battle-contract.md`

## Reused Presentation References

- Battle scene and map runtime: `battle-scene-architecture.md`
- Unit system: `unit-system.md`
- Unit authoring: `unit-authoring.md`
- Unit animation system: `unit-animation-system.md`

## Rule

Do not add new work to AP, `BattleTurnController`, or `BattleActionMenu` as future architecture. After final cleanup, these retired files and scene dependencies should stay deleted.
```

- [ ] **Step 4: Simplify technical architecture**

Remove manual legacy architecture sections and keep the target contract, retired-runtime boundary, and links. Do not link to deleted docs.

- [ ] **Step 5: Update migration docs and QA**

Add `20-legacy-manual-authority-deletion-implementation-plan.md` to the main migration route. Update legacy retirement to say the first deletion pass removed misleading manual battle docs. Add QA checks for deleted docs and auto runtime default.

## Task 3: Make Auto Battle The Default Runtime Path

**Files:**
- Modify: `src/Presentation/World/Sites/WorldSiteRoot.cs`

- [ ] **Step 1: Default the exported flag to true**

Change:

```csharp
public bool UseAutoBattleRuntime { get; set; }
```

to:

```csharp
public bool UseAutoBattleRuntime { get; set; } = true;
```

Do not delete the manual branch in this slice.

- [ ] **Step 2: Run focused test**

Run:

```powershell
dotnet run --project tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegression.csproj
```

Expected: deletion/default guards pass.

## Task 4: Verification

Run:

```powershell
dotnet run --project tests/AutoBattleRuntimeRegression/AutoBattleRuntimeRegression.csproj
dotnet run --project tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegression.csproj
dotnet run --project tests/WorldSiteIntelRegression/WorldSiteIntelRegression.csproj
dotnet run --project tests/BattleHitFeedbackRegression/BattleHitFeedbackRegression.csproj
dotnet build rpg.csproj -maxcpucount:2 -v:minimal
dotnet build-server shutdown
```

Expected:

- all regression projects exit `0`;
- main project build exits `0`;
- known Godot source generator warning may appear in console test projects and does not block this slice.

## Self-Review Checklist

- Stale manual battle docs were deleted, not merely superseded.
- Remaining battle READMEs route clean-context agents to auto tactics migration docs.
- Auto battle is the default WorldSite runtime path.
- Manual C# code is no longer described as target authority; final cleanup deletes the remaining runtime files and scene dependencies.
- No final HUD, full playback, hero skill, or AP expansion was introduced.
