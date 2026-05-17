# Root File Decomposition

Status: Archived

## Problem

Several root-level scene and service files have grown past 1000 lines. These files mix lifecycle, UI, input, runtime orchestration, data mutation, diagnostics, and presentation helpers in the same class. This makes architecture rules difficult to enforce and encourages local shortcuts.

## Scope

This proposal covers mechanical and architectural decomposition of oversized code files. It does not change gameplay rules.

## Current Oversized Code Files

| File | Lines | Issue |
|---|---:|---|


`src/Presentation/World/StrategicWorldRoot.cs` is no longer oversized after the second split. The main file now keeps scene lifecycle and top-level input/update routing only; focused partials hold map drawing, fog/intel, UI bootstrap, selection input, army commands, expedition, detail HUD, battle entry, map setup, world clock, persistence, geometry/formatting, and local DTOs.



`src/Application/Emotion/EmotionSystem.cs` is no longer oversized after the final split. The main file now keeps construction and shared indexes; focused partials hold generation, event building/application, queries, evaluations, conditions, state/indexing, and local social context type.

The oversized regression entry files are no longer oversized after the test split. `Program.cs` files now keep only test runner entrypoints; grouped `*Cases.*.cs` files hold focused test cases and shared helpers.

`src/Presentation/World/Sites/WorldSiteRoot.cs` is no longer oversized after the first split. The main file now keeps scene lifecycle and map loading only; focused partials hold battle runtime, site HUD, site map presentation, exploration, interaction, formatting, and local DTOs.

## Target Rules

- Production `.cs` files should stay under 1000 lines.
- Test `.cs` files should stay under 1000 lines after migration. During migration, existing oversized test files are allowlisted but no new oversized test files may be introduced.
- Root scene classes should act as composition roots only:
  - bind Godot nodes;
  - forward lifecycle/input events;
  - delegate behavior to focused partials or controllers;
  - avoid owning application rules or long-term state mutation.
- Decomposition must preserve architecture ownership:
  - Presentation may display state and submit commands;
  - Application services own use-case rules and long-term writes;
  - Runtime owns in-battle memory state;
  - Settlement owns battle result writeback.
- Mechanical partial extraction is allowed only as an intermediate step when it reduces risk. Follow-up steps must move logic into focused services/controllers where ownership boundaries require it.

## Execution Order

1. Add architecture regression coverage for oversized files.
2. Split `WorldSiteRoot.cs` into focused partial files. Done:
   - `WorldSiteRoot.Types.cs`
   - `WorldSiteRoot.BattleRuntime.cs`
   - `WorldSiteRoot.SiteManagementHud.cs`
   - `WorldSiteRoot.SiteMapPresentation.cs`
   - `WorldSiteRoot.SiteExplorationFlow.cs`
   - `WorldSiteRoot.SiteExplorationPresentation.cs`
   - `WorldSiteRoot.SiteExplorationBattle.cs`
   - `WorldSiteRoot.SiteInteraction.cs`
   - `WorldSiteRoot.SiteFormatting.cs`
3. Split `StrategicWorldRoot.cs` into focused partial files. Done:
   - `StrategicWorldRoot.Types.cs`
   - `StrategicWorldRoot.MapDrawing.cs`
   - `StrategicWorldRoot.FogIntel.cs`
   - `StrategicWorldRoot.UiBootstrap.cs`
   - `StrategicWorldRoot.SelectionInput.cs`
   - `StrategicWorldRoot.ArmyCommands.cs`
   - `StrategicWorldRoot.Expedition.cs`
   - `StrategicWorldRoot.ExpeditionHud.cs`
   - `StrategicWorldRoot.DetailHud.cs`
   - `StrategicWorldRoot.SiteEntry.cs`
   - `StrategicWorldRoot.BattleEntry.cs`
   - `StrategicWorldRoot.Persistence.cs`
   - `StrategicWorldRoot.MapSetup.cs`
   - `StrategicWorldRoot.WorldClock.cs`
   - `StrategicWorldRoot.GeometryFormatting.cs`
4. Split regression test files by topic. Done:
   - `tests/BattleHitFeedbackRegression/Program.cs` now delegates to `BattleHitFeedbackRegressionCases.*.cs` files.
   - `tests/WorldSiteDeploymentCacheRegression/Program.cs` now delegates to `WorldSiteDeploymentCacheRegressionCases.*.cs` files.
5. Split `EmotionSystem.cs` into focused partial files. Done:
   - `EmotionSystem.Generation.cs`
   - `EmotionSystem.Events.cs`
   - `EmotionSystem.Queries.cs`
   - `EmotionSystem.Evaluation.cs`
   - `EmotionSystem.Conditions.cs`
   - `EmotionSystem.StateIndex.cs`
   - `EmotionSystem.Types.cs`

## Progress

- Added architecture regression coverage that tracks existing oversized files and rejects newly introduced oversized `.cs` files.
- Split `WorldSiteRoot.cs` from 5181 lines to 360 lines. All generated `WorldSiteRoot.*.cs` partials are under 1000 lines.
- Split `StrategicWorldRoot.cs` from 5207 lines to 253 lines. All generated `StrategicWorldRoot.*.cs` partials are under 1000 lines.
- Updated source-based regression checks to read the complete `WorldSiteRoot*.cs` and `StrategicWorldRoot*.cs` partial sets instead of the old monolithic files.
- Split the two oversized regression entry files into focused cases files; all generated test case files are under 1000 lines.
- Split `EmotionSystem.cs` from 1555 lines to 31 lines. All generated `EmotionSystem.*.cs` partials are under 1000 lines.
- Cleared the oversized file allowlist; the regression now rejects any `.cs` file over 1000 lines.

- Split battle preparation HUD logic out of `WorldSiteRoot.SiteManagementHud.cs` into `WorldSiteRoot.BattlePreparationHud.cs`, reducing the peacetime HUD partial from 959 lines to 781 lines.

## Acceptance

- No production code file remains over 1000 lines, or remaining exceptions are explicitly tracked by the regression allowlist during migration.
- `WorldSiteRoot.cs` and `StrategicWorldRoot.cs` no longer contain unrelated large responsibility blocks.
- Existing target regression projects and solution build pass after each step.
- New behavior changes are not introduced by mechanical extraction.
