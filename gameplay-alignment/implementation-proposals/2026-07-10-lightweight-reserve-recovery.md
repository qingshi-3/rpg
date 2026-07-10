# Lightweight Reserve Recovery Implementation Proposal

Status: Implementation Complete — Pending Manual QA

> **For agentic workers:** execute this plan task by task with `superpowers:executing-plans`. Subagent execution requires an explicit user request. Each task uses checkbox tracking and ends with an independently verifiable result.

**Goal:** Replace the unaccepted manual/automatic conscription system with configured passive recovery of `2` reserve soldiers per elapsed world-map pulse, capped by remaining city force capacity.

**Architecture:** Strategic Management economy configuration owns the recovery rate. `SettleElapsedWorldTime` applies one aggregated recovery mutation per controlled city and emits one low-noise event. Presentation reads the rate from the city view model; no command, policy state, or dedicated conscription UI remains.

**Tech Stack:** Godot 4.5, C#, .NET, JSON configuration, authored `.tscn` UI, source-oriented regression executables.

## Global Constraints

- Work on `main`; do not create or switch branches.
- Preserve all unrelated user worktree changes.
- Use `dotnet build rpg.sln -maxcpucount:2 -v:minimal` when a full build is required.
- Do not start Godot or trigger resource import unless static checks and focused regression suites are insufficient.
- Player-visible text remains Chinese.
- Runtime recovery logs/events must be aggregated per city and settlement command, never per pulse.
- No temporary compatibility command, hidden policy fallback, or dormant conscription UI may remain.
- Do not commit unless the user explicitly requests it; use focused diffs and verification outputs as checkpoints in the dirty shared worktree.

## Traceability

- Requirement: `SM-RESERVE-RECOVERY-001`
- Originating Design Proposal: `design-proposals/archived/2026-07-10-lightweight-reserve-recovery/`
- Parent Implementation Record: `gameplay-alignment/implementation-proposals/archived/2026-06-20-strategic-operation-foundation-loop.md`
- Amends: the parent record's manual and automatic conscription slice only.
- Supersedes: None.
- Blocking Issues: None known. Automated verification is green; in-game manual QA remains pending.
- Accepted Authority:
  - `gameplay-design/content-systems-long-term-design.md`
  - `gameplay-design/details/cities-and-locations/README.md`
  - `system-design/strategic-management-system-architecture.md`
  - `system-design/presentation-ui-layout-architecture.md`

## Required GodotPrompter Skills During Execution

- `csharp-godot`: C# and Godot API conventions.
- `godot-ui`: authored scene and Control-node cleanup.
- `save-load`: backward-compatible removal of the persisted policy field.
- `godot-testing`: focused regression-first implementation.
- `godot-code-review`: final Godot-specific review before acceptance.

## Scope

- Add one configured `ReserveRecoveryPerElapsedPulse` economy fact with value `2`.
- Apply passive reserve recovery during accepted elapsed world-map settlement.
- Cap recovery by `ActiveForces + ReserveForces <= CityForceCapacity`.
- Emit one `StrategicCityReserveRecovered` event per changed city per settlement command.
- Remove manual conscription commands, automatic intensity rules, policy persistence, policy configuration, policy view models, and dedicated UI.
- Keep old version-1 saves loadable when they contain the removed `AutoConscriptionIntensityId` JSON property.
- Show the passive rate as a read-only city fact in existing overview/summary presentation.

## Non-Goals

- Population, public order, recruitment pools, training queues, or demographics.
- Resource, building, hero, technology, or city-role modifiers to recovery.
- Changes to corps creation or replenishment costs.
- Changes to world-map time ownership, cadence, pause rules, or production order.
- General Strategic Management decomposition or unrelated UI redesign.
- Repairing unrelated README, resource-taxonomy, or proposal-index debt.

## File Responsibility Map

- `config/strategic_management/economy/resources.json`: resource definitions plus the one base reserve-recovery rate.
- `src/Application/Config/StrategicManagementContentConfigLoader.cs`: deserialize and validate the economy rate; remove the policy loader.
- `src/Definitions/StrategicManagement/StrategicManagementDefinitions.cs`: expose the rate on the definition set; remove policy definition types.
- `src/Definitions/StrategicManagement/FirstStrategicManagementDefinitions.cs`: copy the configured rate into runtime definitions.
- `src/Application/StrategicManagement/StrategicManagementCommandService.Resources.cs`: authoritative passive recovery settlement and event emission.
- `src/Application/StrategicManagement/StrategicManagementRules.cs`: retain force-capacity queries; remove policy-only rules.
- `src/Domain/StrategicManagement/StrategicCityState.cs`: remove persistent policy state.
- `src/Application/StrategicManagement/StrategicManagementViewModels.cs` and `StrategicManagementViewModelService.cs`: expose a read-only recovery rate and remove policy view models.
- `src/Presentation/World/Sites/StrategicManagementDashboardPanelBinder.cs`: show the rate in existing city summary text.
- `scenes/world/ui/WorldSitePeacetimeHud.tscn` and related node-reference/root partials: remove the dedicated conscription tab and section.
- Strategic Management and Presentation regression suites: replace policy expectations with passive-recovery and anti-regrowth coverage.

---

### Task 1: Introduce the configured passive-recovery definition

**Files:**

- Modify: `config/strategic_management/economy/resources.json`
- Modify: `config/README.md`
- Modify: `src/Application/Config/StrategicManagementContentConfigLoader.cs`
- Modify: `src/Definitions/StrategicManagement/StrategicManagementDefinitions.cs`
- Modify: `src/Definitions/StrategicManagement/FirstStrategicManagementDefinitions.cs`
- Modify: `tests/StrategicManagementRegression/StrategicManagementRegressionCases.State.cs`
- Modify: `tests/StrategicManagementRegression/Program.cs`

**Interfaces:**

- Produces: `StrategicManagementDefinitionSet.ReserveRecoveryPerElapsedPulse : int`.
- Produces: `StrategicManagementContentConfig.ReserveRecoveryPerElapsedPulse : int`.
- Preserves temporarily: existing policy definitions and state until Task 3 removes every remaining consumer in one buildable slice.

- [x] **Step 1: Add failing economy-configuration tests**

Add tests equivalent to:

```csharp
internal static void StrategicManagementLoadsPassiveReserveRecoveryFromEconomyConfig()
{
    StrategicManagementDefinitionSet definitions = FirstStrategicManagementDefinitions.Create();
    AssertEqual(2, definitions.ReserveRecoveryPerElapsedPulse,
        "first-version economy config should define passive reserve recovery");
}

```

- [x] **Step 2: Run the Strategic Management suite and confirm the new tests fail**

Run:

```powershell
dotnet run --project tests\StrategicManagementRegression\StrategicManagementRegression.csproj -v:minimal
```

Expected: failure because the recovery definition is absent.

- [x] **Step 3: Add the economy definition without changing behavior**

The resulting contracts must have this shape:

```csharp
public sealed class StrategicManagementContentConfig
{
    public int ReserveRecoveryPerElapsedPulse { get; set; }
    public List<StrategicResourceDefinition> Resources { get; set; } = new();
    public StrategicConscriptionDefinition Conscription { get; set; } = new();
    public List<StrategicBuildingDefinition> Buildings { get; set; } = new();
    public List<StrategicCorpsDefinition> Corps { get; set; } = new();
}

public sealed class StrategicManagementResourceDefinitionConfig
{
    public int ReserveRecoveryPerElapsedPulse { get; set; }
    public List<StrategicResourceDefinition> Resources { get; set; } = new();
}

public static StrategicManagementResourceDefinitionConfig LoadResourceEconomy(string path)
{
    string text = ProjectConfigFileReader.ReadAllText(path);
    StrategicManagementResourceDefinitionConfig config =
        JsonSerializer.Deserialize<StrategicManagementResourceDefinitionConfig>(text, ProjectJson.Options)
        ?? throw new InvalidOperationException($"Invalid strategic management economy config path={path}");
    ValidateResources(config, path);
    if (config.ReserveRecoveryPerElapsedPulse <= 0)
    {
        throw new InvalidOperationException(
            $"Strategic management reserve recovery must be positive path={path}");
    }

    return config;
}
```

`LoadDefaultContent()` must call `LoadResourceEconomy(ResourceConfigPath)` once and copy both `Resources` and `ReserveRecoveryPerElapsedPulse` into `StrategicManagementContentConfig`. The existing `LoadResources(path)` API may delegate to `LoadResourceEconomy(path).Resources`. `FirstStrategicManagementDefinitions.Create()` must assign the same value to `StrategicManagementDefinitionSet.ReserveRecoveryPerElapsedPulse`.

`resources.json` must contain:

```json
{
  "reserveRecoveryPerElapsedPulse": 2,
  "resources": [
    { "resourceId": "resource_money", "displayName": "资金" },
    { "resourceId": "resource_food", "displayName": "粮食" },
    { "resourceId": "resource_wood", "displayName": "木材" },
    { "resourceId": "resource_ore", "displayName": "矿石" }
  ]
}
```

Keep the existing policy loader intact at this checkpoint so the project remains buildable. Update `config/README.md` so `resources.json` also owns the base recovery rate; Task 3 will delete the policy entry and file after all consumers are removed.

- [x] **Step 4: Add invalid-rate validation coverage**

Load a temporary economy config with `reserveRecoveryPerElapsedPulse` equal to `0` and assert `InvalidOperationException`. The accepted first-version definition must be positive.

- [x] **Step 5: Run the focused suite**

Run the Strategic Management regression command again. Expected: the new configuration tests pass and existing policy behavior remains unchanged until later tasks.

- [x] **Step 6: Review the focused diff checkpoint**

Confirm that only economy configuration, definition projection, config documentation, and their tests changed. Do not commit in the shared dirty worktree.

---

### Task 2: Implement passive recovery and stop automatic-policy settlement

**Files:**

- Modify: `src/Application/StrategicManagement/StrategicManagementCommandService.Resources.cs`
- Modify: `tests/StrategicManagementRegression/StrategicManagementRegressionCases.DashboardsAndTimeflow.cs`
- Modify: `tests/StrategicManagementRegression/Program.cs`

**Interfaces:**

- Consumes: `StrategicManagementDefinitionSet.ReserveRecoveryPerElapsedPulse`.
- Consumes: `StrategicManagementRules.GetRemainingCityForceCapacity(state, cityId)`.
- Produces: event kind `StrategicCityReserveRecovered` with `faction`, `elapsedPulses`, `recoveryPerPulse`, and `reserveGain` facts.
- Replaces: automatic-policy settlement with passive recovery; policy commands and types remain temporarily until Task 3 removes the complete surface.

- [x] **Step 1: Replace policy tests with failing passive-recovery tests**

Cover these deterministic cases:

```csharp
// One pulse: 0 -> 2, no resource mutation, one recovery event.
// Three pulses: 0 -> 6, still one aggregated recovery event.
// One point of remaining capacity: gain exactly 1, never overflow capacity.
// Full city: gain 0 and emit no recovery event.
// Enemy-held city: gain 0.
// Invalid elapsed pulse count: no mutation.
```

Also assert `StrategicCityReserveRecovered` carries the configured rate and actual bounded gain. Remove the three automatic-policy settlement test registrations because that behavior is replaced here; keep manual-command tests until Task 3.

- [x] **Step 2: Run the suite and confirm passive-recovery tests fail**

Run the Strategic Management regression command. Expected: failures because elapsed time still routes through automatic policy settlement.

- [x] **Step 3: Implement one aggregated recovery helper**

Use this contract and bounded arithmetic:

```csharp
private void SettleCityReserveRecovery(
    StrategicManagementState state,
    string factionId,
    int elapsedPulses,
    StrategicCityState city,
    StrategicCommandResult result)
{
    int rate = System.Math.Max(0, _definitions.ReserveRecoveryPerElapsedPulse);
    int remaining = _rules.GetRemainingCityForceCapacity(state, city.LocationId);
    long requested = (long)rate * elapsedPulses;
    int gain = (int)System.Math.Min(remaining, requested);
    if (gain <= 0)
    {
        return;
    }

    city.ReserveForces += gain;
    AddUnique(result.ChangedFactIds, city.LocationId);
    result.Events.Add(Event(
        "StrategicCityReserveRecovered",
        city.LocationId,
        ("faction", factionId),
        ("elapsedPulses", elapsedPulses.ToString()),
        ("recoveryPerPulse", rate.ToString()),
        ("reserveGain", gain.ToString())));
}
```

Call it once for each `GetControlledCities(state, factionId)` result during `SettleElapsedWorldTime`. Replace the stale comment that says reserve recovery is outside the economy slice.

- [x] **Step 4: Disconnect automatic policy settlement**

Remove `SettleCityAutoConscription` and route controlled cities only through `SettleCityReserveRecovery`. Keep policy commands, rules, state, and UI untouched at this intermediate checkpoint so the project still compiles; Task 3 deletes them together. Do not call or consult `AutoConscriptionIntensityId` from elapsed-time settlement.

- [x] **Step 5: Run the Strategic Management suite**

Expected: all passive-recovery, city capacity, corps creation/replenishment, and elapsed-time cases pass. Manual policy command tests still pass only as temporary pre-Task-3 coverage.

- [x] **Step 6: Review the focused diff checkpoint**

Confirm elapsed-time production ordering is unchanged, recovery is one additional aggregated city settlement after production, and automatic intensity no longer affects settlement. Do not commit.

---

### Task 3: Retire the complete conscription policy surface and expose a read-only rate

**Files:**

- Delete: `scenes/world/ui/WorldConscriptionPanel.tscn`
- Delete: `src/Presentation/World/Sites/WorldConscriptionPanel.cs`
- Delete: `src/Presentation/World/Sites/WorldConscriptionPanel.cs.uid`
- Delete: `config/strategic_management/economy/conscription_policies.json`
- Modify: `config/README.md`
- Modify: `src/Application/Config/StrategicManagementContentConfigLoader.cs`
- Modify: `src/Definitions/StrategicManagement/StrategicManagementDefinitions.cs`
- Modify: `src/Definitions/StrategicManagement/FirstStrategicManagementDefinitions.cs`
- Modify: `src/Definitions/StrategicManagement/StrategicManagementIds.cs`
- Modify: `src/Domain/StrategicManagement/StrategicCityState.cs`
- Modify: `src/Application/StrategicManagement/StrategicManagementCommandService.CityCommands.cs`
- Modify: `src/Application/StrategicManagement/StrategicManagementRules.cs`
- Modify: `src/Application/StrategicManagement/StrategicFailureReasons.cs`
- Modify: `scenes/world/ui/WorldSitePeacetimeHud.tscn`
- Modify: `src/Application/StrategicManagement/StrategicManagementViewModels.cs`
- Modify: `src/Application/StrategicManagement/StrategicManagementViewModelService.cs`
- Modify: `src/Presentation/Common/GameUiSceneFactory.cs`
- Modify: `src/Presentation/World/Sites/StrategicManagementDashboardPanelBinder.cs`
- Modify: `src/Presentation/World/Sites/WorldSitePeacetimeHudNodeRefs.cs`
- Modify: `src/Presentation/World/Sites/WorldSiteRoot.cs`
- Modify: `src/Presentation/World/Sites/WorldSiteRoot.SiteManagementHud.cs`
- Modify: `tests/StrategicManagementRegression/StrategicManagementRegressionCases.DashboardsAndTimeflow.cs`
- Modify: `tests/StrategicManagementRegression/StrategicManagementRegressionCases.CityAndCorps.cs`
- Modify: `tests/StrategicManagementRegression/StrategicManagementRegressionCases.State.cs`
- Modify: `tests/StrategicManagementRegression/StrategicManagementRegressionCases.Support.cs`
- Modify: `tests/StrategicManagementRegression/Program.cs`
- Modify: `tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegressionCases.PresentationAntiRot.cs`
- Modify: `tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegressionCases.PresentationResourceAuthoring.cs`
- Modify: `tests/WorldSiteDeploymentCacheRegression/Program.cs`

**Interfaces:**

- Produces: `StrategicCityManagementViewModel.ReserveRecoveryPerElapsedPulse : int`.
- Removes: persisted policy state, policy definitions/config/ids, manual and automatic commands/rules, `StrategicConscriptionViewModel` and its option types, `BuildConscription`, factory methods, signals, callbacks, node refs, scene nodes, and `SiteManagementSection.Conscription`.

- [x] **Step 1: Add failing Presentation and view-model guards**

Update regression expectations so they require:

```csharp
AssertEqual(2, dashboard.SelectedCity.ReserveRecoveryPerElapsedPulse,
    "city dashboard should expose configured passive reserve recovery");
```

Source/scene guards must reject `ConscriptionTabButton`, `SiteConscriptionSection`, `WorldConscriptionPanel`, `ManualConscriptRequested`, `AutoConscriptionIntensityRequested`, and both retired command calls. Retain the existing authored-resource guard for the remaining build, recruitment, and overview tabs.

Add contract guards equivalent to:

```csharp
AssertTrue(typeof(StrategicCityState).GetProperty("AutoConscriptionIntensityId") == null,
    "city state must not persist a retired conscription policy");
AssertTrue(typeof(StrategicManagementCommandService).GetMethod("ManualConscriptReserveForces") == null,
    "manual conscription must not remain as a command contract");
AssertTrue(typeof(StrategicManagementCommandService).GetMethod("SetAutoConscriptionIntensity") == null,
    "automatic conscription must not remain as a command contract");
```

- [x] **Step 2: Run both focused suites and confirm the new guards fail**

Run:

```powershell
dotnet run --project tests\StrategicManagementRegression\StrategicManagementRegression.csproj -v:minimal
dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal
```

Expected: failures while the dedicated policy UI still exists.

- [x] **Step 3: Project the passive rate and remove policy view models**

Add only this read-only city fact:

```csharp
public int ReserveRecoveryPerElapsedPulse { get; set; }
```

`BuildSelectedCity` assigns it from `_definitions.ReserveRecoveryPerElapsedPulse`. Delete `StrategicConscriptionViewModel`, its option classes, and `BuildConscription`.

- [x] **Step 4: Remove policy state, commands, rules, and configuration**

Delete `AutoConscriptionIntensityId`, the policy definition types/property, policy ids, loader/path/validation, manual and automatic command methods, policy-only rules and failure reason, the policy JSON, policy tests, and reflection helpers. Remove the policy entry from `config/README.md`; keep the passive rate documented under `resources.json`. Do not leave wrappers or fallbacks.

- [x] **Step 5: Prove legacy version-1 saves remain readable**

Save a version-1 state, add an `AutoConscriptionIntensityId` property to a city JSON object with `System.Text.Json.Nodes`, reload it through `StrategicManagementSaveService`, and assert the city, reserve count, buildings, and corps still load. Keep save version `1`: the removed JSON property is ignored and no migration adapter owns gameplay facts.

- [x] **Step 6: Remove the dedicated UI surface**

Delete the panel scene/script and factory path/method. Remove the conscription tab icon resource, tab button, section, and list from `WorldSitePeacetimeHud.tscn`; remove matching node refs, root fields, enum value, signal wiring, visibility routing, command handlers, and binder constructor dependencies.

Update the existing city summary text to include a Chinese read-only fact equivalent to:

```text
兵力 {used}/{capacity}    预备 {reserve}    恢复 +{rate}/世界脉冲
```

Do not build a replacement panel.

- [x] **Step 7: Run Strategic Management and Presentation suites**

Expected: both focused suites pass with no dedicated conscription surface and the passive rate visible through the existing overview/summary path.

- [x] **Step 8: Review the focused diff checkpoint**

Confirm remaining site-management tabs and recruitment resource-cost presentation are unchanged. Do not commit.

---

### Task 4: Verify the full slice and record acceptance evidence

**Files:**

- Update after verification: `gameplay-alignment/implementation-proposals/2026-07-10-lightweight-reserve-recovery.md`
- Update after acceptance: `gameplay-alignment/implementation-proposals/README.md`
- Move after acceptance: this proposal to `gameplay-alignment/implementation-proposals/archived/`

- [x] **Step 1: Run anti-regrowth searches**

Run:

```powershell
rg -n "ManualConscript|AutoConscription|ConscriptionIntensity|conscription_policies|WorldConscriptionPanel|ConscriptionTabButton|SiteConscriptionSection" src scenes config tests
```

Expected: no matches except explicit negative assertions in regression tests.

- [ ] **Step 2: Run focused verification**

```powershell
dotnet run --project tests\StrategicManagementRegression\StrategicManagementRegression.csproj -v:minimal
dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal
```

Expected: exit code `0` from both suites.

- [x] **Step 3: Run the low-concurrency solution build**

```powershell
dotnet build rpg.sln -maxcpucount:2 -v:minimal
dotnet build-server shutdown
```

Expected: build exit code `0`; compiler servers shut down cleanly.

- [ ] **Step 4: Perform manual QA only if the static and focused suites pass**

Verify in Godot:

- the managed-city tab rail has build, recruitment, overview, and return functions but no conscription tab;
- city summary displays reserve soldiers, capacity, and `+2/世界脉冲` recovery;
- after returning to the world map and settling one pulse, a non-full controlled city gains exactly `2` reserve soldiers;
- a near-full city stops at capacity;
- no Money or Food is consumed by recovery;
- logs contain one `StrategicCityReserveRecovered` fact for the changed city and no per-pulse spam.

- [x] **Step 5: Record evidence and archive only after acceptance**

Append exact command outputs, manual-QA results, diagnostics, and final file inventory to this proposal. Mark it accepted and archive it only when all required evidence passes; otherwise keep it active with the precise blocker.

## Diagnostics

- Keep `StrategicWorldTimeSettled` as the command-level time event.
- Add only `StrategicCityReserveRecovered` for cities whose reserve count changed.
- Event facts: `faction`, `elapsedPulses`, `recoveryPerPulse`, `reserveGain`.
- Do not log full city state, per-frame values, or one line per elapsed pulse.

## Verification Evidence — 2026-07-10

- TDD RED was observed for the missing configured rate, non-positive validation, passive settlement behavior, removed policy contracts, read-only city rate, and deleted UI surface before the matching production changes.
- `rg -n "ManualConscript|AutoConscription|ConscriptionIntensity|conscription_policies|WorldConscriptionPanel|ConscriptionTabButton|SiteConscriptionSection" src scenes config` returned no production matches. Remaining test matches are the version-1 legacy JSON fixture and explicit negative contract guards.
- `dotnet run --project tests\StrategicManagementRegression\StrategicManagementRegression.csproj -v:minimal` verified every requirement-specific test as `PASS`, including one/multiple pulse recovery, capacity bounding, no-cost behavior, enemy/full-city skips, retired contracts, view-model projection, and legacy save loading. Exit code remains `1` only because the pre-existing foundation-building atlas test fails before this slice.
- `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal` verified the authored tab layout, dashboard binder, command routing cleanup, scene binding, and dedicated-conscription-surface guard as `PASS`. Exit code remains `1` only because the pre-existing `assets/TileSets` taxonomy count is `2` instead of `0`.
- After the tracked legacy TileSet resources were removed on 2026-07-10, a fresh complete `WorldSiteDeploymentCacheRegression` run passed with exit code `0`, including resource taxonomy and the dedicated-conscription-surface guard.
- The foundation-building regression was corrected after repository history and current authority showed that its required unified atlas, generator, and fixed footprint table had never been an accepted or implemented contract. The test still verifies config routing, authored per-building `AtlasTexture` resources, existing source textures, and focused icon regions.
- A fresh complete `StrategicManagementRegression` run then passed with exit code `0`; all `84` cases passed. The build emitted the existing non-fatal source-generator warning because this regression project does not supply `GodotProjectDir`.
- `dotnet build rpg.sln -maxcpucount:2 -v:minimal` completed with exit code `0`, `0` errors, and `24` existing warnings. `dotnet build-server shutdown` completed with exit code `0`.
- `git diff --check` reported no whitespace errors; only line-ending normalization warnings for two edited text files.
- Godot-specific review found no critical or important issue: the existing authored HUD scene remains the UI authority, removed nodes have matching C# node-reference/field/callback cleanup, and no per-frame work or runtime-built replacement UI was introduced.
- Manual Godot QA was not started because the proposal explicitly gates it on both focused suites exiting `0`.

## Implemented File Inventory

- Economy definition and validation: `config/strategic_management/economy/resources.json`, `StrategicManagementContentConfigLoader`, and Strategic Management definition projection.
- Runtime behavior: elapsed-time passive recovery and `StrategicCityReserveRecovered` aggregation in `StrategicManagementCommandService.Resources.cs`.
- Retired policy surface: policy JSON, ids, definitions, rules, state field, commands, failure reason, view models, factory method, panel scene/script, tab/section/node refs, callbacks, and policy-specific tests.
- Presentation: existing city selection summary now shows used capacity, reserve soldiers, and configured recovery per world pulse; the remaining authored rail contains build, recruitment, overview, and return entries.
- Compatibility and regression evidence: Strategic Management configuration/settlement/legacy-save tests plus Presentation anti-regrowth and authored-resource checks.

## Acceptance Evidence Rule

Implementation evidence is recorded above. The proposal remains active and unarchived only until the gated manual QA is completed or the user explicitly accepts it without that QA.
