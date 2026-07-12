# Strategic Operation Foundation Loop

Status: Archived - Automated Verification Passed, Desktop QA Deferred

Archive Note: Archived by user request on 2026-06-30 after the Strategic Operation foundation-loop code, scene, config, UI, expedition, settlement, and follow-up large-map fixes reached automated verification. Desktop Mono QA notes remain historical evidence for future reopen decisions, not an active queue item.

## Origin

- Requirement: STRAT-OPS-001
- Design Proposal: `design-proposals/archived/2026-06-20-strategic-operation-foundation/`
- Authority:
  - `gameplay-design/content-systems-long-term-design.md`
  - `gameplay-design/details/cities-and-locations/README.md`
  - `system-design/strategic-management-system-architecture.md`
  - `system-design/strategic-battle-bridge-architecture.md`
- Related Implementation Proposals:
  - `gameplay-alignment/implementation-proposals/2026-06-19-site-map-layout-first-city.md`

## Goal

Implement the first player-visible Strategic Operation foundation loop:

```text
city construction -> world-map resource and reserve recovery -> corps recruitment or replenishment
-> hero company formation -> expedition -> occupy or develop a new strategic location
```

This proposal implements the foundation loop only. Full local building battle support, in-battle support activation, beast-route content, final city-map art authoring, and the rebuilt building-effects/economy model are later slices.

## Reopen Note

After the first implementation, the accepted direction changed for building authoring and economy effects:

- Old `BuildingSlot` markers, `FacilitySlotDefinition`, `FacilityDefinition`, and `WorldActionResolver` build actions are no longer accepted scaffolding.
- Strategic city building placement remains `ConstructionRegion` marker -> placement preview -> `BuildCityBuilding`.
- Direct ad hoc per-building scalar fields are retired temporary logic. The current foundation loop may add a thin Strategic Management-owned runtime production capability for basic economy buildings so one launch-session can complete construction -> resource growth -> recruitment/expedition flow without introducing a full economy system.
- Accepted amendment `design-proposals/archived/2026-06-20-building-placement-region-freedom/` removes construction-region building-category legality. Construction regions now define where buildings may be placed; terrain/tile/resource context may later affect production or support efficiency through a focused economy/capability model.

## Current State

Strategic Management is the long-term strategic authority for the foundation loop. The reopened cleanup has retired the old temporary building scaffolding:

- legacy `BuildingSlot` markers, `FacilitySlotDefinition`, `FacilityDefinition`, `WorldFacilitySlot` presentation, and `WorldActionResolver` build actions have been removed from runtime code and authored scenes;
- city construction routes through `ConstructionRegion` markers, placement preview, Strategic Management rules, and `BuildCityBuilding`;
- city buildings no longer own retired direct reserve recovery or force-capacity scalar fields;
- basic economy city buildings may provide a small, definition-authored runtime production capability settled only by elapsed world-map time while the player is outside paused city management;
- controlled non-city resource sites still own passive `ProductionPerWorldTimePulse`;
- expedition and battle bridge behavior remains reused without adding local building battle support in this slice.

## Architecture Direction

The implementation should replace the old Strategic Management facility-slot model with the accepted city building model inside Strategic Management itself. Do not add a compatibility layer where facilities and buildings both own the same city-development facts.

Strategic Management remains the owner of definitions, durable state, rules, commands, view models, elapsed world-map settlement, and battle-result writeback. Presentation consumes view models and submits commands. Combat and the Strategic Battle Bridge are not changed for full local building support in this slice.

The implementation may temporarily break compilation during the refactor, but the proposal is accepted only when the final code compiles and the automated/manual verification below has evidence.

## Scope

### 1. Foundation Strategic Content

Replace first-foundation content with the accepted foundation resources and buildings:

- resources: `Money`, `Food`, `Wood`, `Ore`;
- city buildings: `Farm`, `Market`, `Lumber Camp`, `Mine`, `Training Ground`, `Tavern`, `Arrow Tower`, `Medical Shrine`;
- construction regions for the first managed city, with map-aligned grid bounds but without building-category restrictions;
- foundation common corps creation and replenishment costs that consume resources and reserve soldiers;
- first hostile/developable target using existing world-map and battle routing where practical, but with generic foundation rewards instead of beast-route unlock language.

For the current foundation-loop QA, Bonefield is an enemy-held managed stronghold/city target, not a generic non-city ruin. It may reuse the current authored site/battle scene assets, but Strategic Management definitions and state must treat it as an implemented city whose management surface is locked only by ownership/control. Victory applies the battle-result command to change ownership/control; it must not fabricate a new city model inside the post-battle UI path.

The first implementation may keep existing battle map and large-map visual assets, including current demo site layouts, but the player-facing strategic reward should validate the foundation loop rather than the beast route.

### 2. Strategic State Model

Update Strategic Management state so the first city stores:

- building instances with stable ids, definition ids, construction region ids, grid positions, level/construction state, and optional future `BattleAnchorId`;
- authored construction-region state or definition references;
- `CityForceCapacity`;
- `ReserveForces`;
- active forces derived from corps, hero companies, and garrison/expedition assignments rather than maintained as an independent mutable pool.

Old Strategic Management facility-slot state must not remain the new authority. Legacy world/domain facility types outside Strategic Management can remain only if they are unrelated compatibility code and do not receive new Strategic Operation behavior.

### 3. Rules And Commands

Add or replace Strategic Management rules and commands for:

- validating building placement by buildable region membership, footprint, bounds, overlap, construction state, resources, and explicit building eligibility;
- building the foundation city buildings through commands;
- settling accepted elapsed world-map effects, including first-slice runtime production from controlled resource sites and constructed basic economy buildings;
- manually conscripting city-local reserve soldiers by spending `Money 15 + Food 20 -> ReserveForces +10`;
- storing a city-local automatic conscription intensity, settled only by elapsed world-map time after resource production in the same settlement command;
- deriving active forces and remaining reserve capacity;
- creating corps from resources plus reserve soldiers;
- replenishing damaged corps from resources plus reserve soldiers;
- assigning corps to heroes and forming dispatchable hero companies through existing hero/corps authority;
- creating expeditions from the city through the existing expedition command path;
- applying battle victory to occupy or develop the target strategic location through existing battle-result command boundaries where practical.

Commands must return explicit success, failure reason, changed facts, and low-noise strategic events. Failed commands must not partially mutate resources, reserve soldiers, building state, corps state, or expedition state.

First-version automatic conscription intensities are:

| Intensity | Rule |
| --- | --- |
| Off | No resource cost and no reserve soldier gain. This is the default to avoid hidden resource drain. |
| Low | Each elapsed world-map pulse may spend `Money 2 + Food 3` to add `ReserveForces +2`. No building requirement. |
| Standard | Each elapsed world-map pulse may spend `Money 5 + Food 8` to add `ReserveForces +6`. Requires a constructed Training Ground. |
| High | Each elapsed world-map pulse may spend `Money 12 + Food 18` to add `ReserveForces +10`. Requires a constructed Training Ground. |

Manual and automatic conscription are full-batch actions: if the city lacks resources or lacks enough remaining force capacity for the full batch, no resources are consumed and no reserve soldiers are added. Automatic conscription skips that city for that settlement pulse rather than partially applying.

### 4. Presentation Slice

Update the Strategic Management presentation so the player can exercise the loop from the current desktop Mono build:

- inspect city resources, reserve soldiers, and force capacity;
- see available construction regions and existing buildings;
- choose a building from a Chinese-labeled construction panel;
- preview a building footprint on the region grid and reject illegal placements with a clear disabled reason;
- place a valid building through Strategic Management commands;
- show city-building runtime resource production after valid economy buildings are constructed and world-map time resumes;
- split city military operations into `征兵`, `招募`, and `编制` pages:
  - `征兵`: reserve pool, automatic conscription intensity, and emergency manual conscription;
  - `招募`: opens a full-screen military workbench instead of using the left city sidebar. The workbench first selects the hero to adjust, then shows RTS-style corps option cards for that hero. Creating a corps consumes reserve soldiers and resources, creates a persistent corps instance, and binds it to the selected hero. If the hero already has a corps, the first version may replace it by returning the previous corps to the city as an unassigned garrison corps;
  - `编制`: existing corps instance management, replenishment, and hero assignment;
- form/select hero companies and dispatch an expedition using the existing expedition path;
- see the occupied/developed target reflected in Strategic Management state and dashboard after battle result application.

The first presentation can be functional and clear rather than final UI quality. It should not hardcode durable gameplay facts in Godot nodes.

### 5. Battle Boundary

Do not implement full local building battle support in this slice.

The implementation may preserve data fields needed for later support snapshots, such as optional `BattleAnchorId`, but combat must not query city building state. The Strategic Battle Bridge support-snapshot path is the next implementation slice.

## Non-Goals

- No in-battle manual building support activation.
- No pre-battle building support selection UI beyond keeping the bridge direction compatible.
- No worker units, road networks, logistics distance, gathering range, or placement-based production efficiency.
- No population, public order, civilian demographic model, or population-driven recruitment.
- No cross-city resource transport loss.
- No faction technology tree.
- No beast-route implementation, beast pen, beast source permission, or beast corps validation in this foundation slice.
- No final detailed city TileMap art authoring; `2026-06-19-site-map-layout-first-city.md` remains deferred until this loop proves what the map needs.
- No new battle Runtime behavior unless required to keep the existing expedition-to-battle path working.

## Touched Systems

- Strategic Management definitions under `src/Definitions/StrategicManagement/`.
- Strategic Management durable state under `src/Domain/StrategicManagement/`.
- Strategic Management rules, command service, runtime, and view models under `src/Application/StrategicManagement/`.
- First strategic content/state factories for the foundation slice.
- Strategic world presentation and site-management dashboard under `src/Presentation/World/` and `src/Presentation/World/Sites/`.
- Strategic Management regression tests under `tests/StrategicManagementRegression/`.
- Static architecture/resource tests under `tests/WorldSiteDeploymentCacheRegression/` only if presentation resources or scene contracts change.

## GodotPrompter Skills

Use these implementation skills before code/resource work:

- `csharp-godot`
- `godot-ui`
- `responsive-ui`
- `input-handling`
- `resource-pattern`
- `scene-organization`
- `godot-testing`
- `godot-debugging`

## Implementation Plan

### Phase 1: Replace Facility Authority With Building Authority

- Add foundation resource ids for `Wood` and `Ore`, and remove `BuildingMaterials`/`BeastMaterials` from foundation tests and first-loop content.
- Replace Strategic Management facility definitions with building definitions, construction-region definitions, and building-instance state.
- Rename or replace `BuildFacility` command paths with building placement commands.
- Update existing facility-focused regression tests into building-focused RED tests before implementation.

### Phase 2: Add City Reserve Soldiers And Capacity

- Add `CityForceCapacity` and `ReserveForces` to city state.
- Add deterministic active-force derivation from corps/hero/garrison/expedition state.
- Add reserve recovery during elapsed world-map settlement.
- Update corps creation to consume reserve soldiers and resources.
- Add corps replenishment command and tests for damaged corps.

### Phase 3: Foundation Content And Economy

- Define first city construction regions and the first building batch.
- Keep retired direct reserve-recovery and capacity scalar effects removed.
- Add only a thin runtime production capability for first-slice economy buildings: farm -> food, market -> money, lumber camp -> wood, mine -> ore. This is settled by Strategic Management elapsed world-map time and is not a save/load or worker/pathing system.
- Seed the first city with enough resources and reserve capacity for a short playable loop.
- Convert the current first hostile/developable target away from beast-route reward language for this slice.

### Phase 4: City Management Presentation

- Replace old facility-slot dashboard affordances with construction-region and building-view models.
- Add building palette, placement preview, legality feedback, and command submission.
- Show resource income, reserve soldiers, force capacity, recruit/replenish affordances, and dispatchable hero companies.
- Keep player-visible text Chinese.

### Phase 5: End-To-End Foundation Loop

- Validate city entry pauses world-map time.
- Validate returning to the world map resumes elapsed-time settlement.
- Validate construction-region placement, recruitment/replenishment from existing state, company formation, expedition, battle/occupy/develop target.
- Keep local building battle support out of completion criteria.

## Tests

Add or update Strategic Management regression coverage for:

- foundation resource definitions contain `Money`, `Food`, `Wood`, and `Ore`, and first-loop tests do not depend on `BuildingMaterials` or beast materials;
- first city exposes authored construction regions and legal building categories;
- valid building placement consumes resources and creates a building instance with region/grid position;
- invalid placement outside a buildable region, overlapping another building, or lacking resources fails without mutation;
- cross-category placement inside any buildable construction region succeeds when footprint, overlap, resources, and explicit eligibility pass;
- ad hoc direct city-building scalar-effect fields are absent;
- constructed basic economy buildings produce foundation resources during elapsed world-map settlement;
- old direct city-building reserve recovery and force-capacity bonus fields are absent;
- default city automatic conscription intensity is `Off`;
- manual conscription consumes `Money 15 + Food 20`, adds `ReserveForces +10`, and never exceeds `CityForceCapacity - ActiveForces`;
- automatic low conscription settles during elapsed world-map time by consuming money/food and adding reserve soldiers;
- standard and high automatic conscription require a constructed Training Ground before the city can select them;
- resource shortage and full or insufficient capacity skip conscription without resource mutation;
- city-management view models expose conscription and recruitment as separate pages instead of merging reserve production and corps creation into one list;
- corps creation consumes resources and reserve soldiers;
- corps replenishment consumes resources and reserve soldiers and restores damaged corps strength;
- dispatching a hero company changes active/available force derivation without corrupting reserve soldiers;
- victory at the first target updates the target location as occupied or developed through Strategic Management command boundaries;
- obsolete beast-route-first tests are removed or rewritten so they cannot reassert beast content as foundation scope.

Recommended verification commands:

```powershell
dotnet run --project tests\StrategicManagementRegression\StrategicManagementRegression.csproj -v:minimal
dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal
dotnet build rpg.csproj -maxcpucount:2 -v:minimal
git diff --check
```

## Diagnostics

Add low-noise logs for important state transitions:

- city building placement accepted/rejected;
- elapsed settlement summaries only for accepted non-building effects;
- corps creation/replenishment cost and result;
- expedition dispatch source city and participant summary;
- target occupation/development result after battle writeback.

Do not add per-frame or cursor-move placement logs. Placement preview diagnostics should be testable through rule results and UI disabled reasons.

## Manual QA

Desktop Mono QA should confirm:

1. Entering the city pauses world-map time.
2. The city management screen shows resources, reserve soldiers, force capacity, construction regions, and building options in Chinese.
3. Placing an illegal building preview is rejected with clear feedback.
4. Placing a legal foundation building consumes resources and appears in the city state/dashboard.
5. Returning to the world map resumes time and applies controlled resource-site plus constructed economy-building production, while city management itself remains paused.
6. Recruiting or replenishing a corps consumes resources and reserve soldiers.
7. A hero company can be selected and dispatched from the city.
8. The expedition can reach the target and trigger the existing battle path.
9. Victory occupies or develops the target and the city/target dashboard reflects the result.
10. No local building battle support UI or in-battle support buttons are required for this slice.

## Acceptance

This implementation proposal is accepted when:

- Strategic Management no longer treats facility slots as the foundation city-development authority.
- The first foundation loop can be completed through the desktop Mono playable path after the economy/capability rebuild.
- The first loop uses `Money`, `Food`, `Wood`, and `Ore`.
- City buildings are placed inside bounded construction regions through command-validated rules.
- Reserve soldiers and city force capacity constrain recruitment and replenishment through accepted Strategic Management state, not through retired per-building scalar effects.
- Existing battle entry/result boundaries still work for the expedition target.
- Automated verification commands pass.
- Manual QA evidence is recorded here after execution.

## Verification Evidence

Previous automated verification, invalidated by the reopen:

- RED evidence: `dotnet run --project tests\StrategicManagementRegression\StrategicManagementRegression.csproj -v:minimal` initially failed because Strategic Management view models still referenced deleted facility-slot APIs, including `StrategicCityState.FacilitySlotCount`, `StrategicManagementDefinitionSet.Facilities`, and `StrategicManagementCommandService.BuildFacility`.
- GREEN evidence: `dotnet run --project tests\StrategicManagementRegression\StrategicManagementRegression.csproj -v:minimal` passed after the building, construction-region, reserve, replenishment, expedition, and battle-result feedback implementation.
- Presentation anti-rot evidence: `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal` passed after updating the strategic dashboard binder and command routing to `BuildCityBuilding`, building placement defaults, and `ReplenishCorps`.
- Build evidence: `dotnet build rpg.csproj -maxcpucount:2 -v:minimal` completed with 0 warnings and 0 errors.
- Diff hygiene evidence: `git diff --check` completed with no whitespace errors.
- Legacy Strategic Management reference scan: this evidence is superseded. The reopened scope removes the remaining old `BuildingSlot`/Facility-slot compatibility and direct building production/recovery/capacity effects.

Reopened cleanup automated evidence:

- `dotnet run --project tests\StrategicManagementRegression\StrategicManagementRegression.csproj -v:minimal` passed after removing direct city-building production/recovery/capacity effects and adding absence guards.
- `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal` passed after removing `BuildingSlot`/Facility-slot markers, scenes, presentation entry points, and stale UI node names.
- `dotnet run --project tests\BattleHitFeedbackRegression\BattleHitFeedbackRegression.csproj -v:minimal` passed after removing legacy facility result/writeback references from world text and result-feedback tests.
- `dotnet build rpg.csproj -maxcpucount:2 -v:minimal` completed with 0 warnings and 0 errors.
- `git diff --check` completed with no whitespace errors; it reported only existing CRLF-to-LF notices for two `BattleHitFeedbackRegression` test files.
- Legacy runtime scan found no old `BuildingSlot`, `BuildingSlotMapMarker`, `FacilitySlot`, `FacilityDefinition`, `WorldFacilitySlot`, old facility build action, old facility state, or old direct building scalar-effect references in `src`, `scenes`, or `config`.

Building-region freedom amendment evidence:

- `dotnet run --project tests\StrategicManagementRegression\StrategicManagementRegression.csproj -v:minimal` passed after aligning first-city construction-region definitions with the `demo_site.tscn` marker coordinates and removing category restrictions from region definitions, view models, and placement rules.
- `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal` passed after removing construction-region `AllowedCategoryIds` marker data and asserting that `demo_site.tscn` uses marker-backed regions without category bans.
- `dotnet run --project tests\BattleHitFeedbackRegression\BattleHitFeedbackRegression.csproj -v:minimal` passed after the construction-region cleanup, confirming the broader battle feedback path still compiles and runs.
- `dotnet build rpg.csproj -maxcpucount:2 -v:minimal` completed with 0 warnings and 0 errors.
- `git diff --check` completed with no whitespace errors; it reported only existing CRLF-to-LF notices for two `BattleHitFeedbackRegression` test files.
- Legacy term scan found no old building-slot, facility-slot, or construction-region category-restriction references in `src`, `scenes`, or `config`; remaining matches are historical notes, authority guardrails, implementation evidence, and anti-rot tests.

Building icon and placement-preview correction evidence:

- `dotnet run --project tests\StrategicManagementRegression\StrategicManagementRegression.csproj -v:minimal` passed after replacing whole-sheet building icon paths with focused single-building `AtlasTexture` resources.
- `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal` passed after the mouse-follow placement preview drew only the selected building texture scaled to the current footprint bounds and after the remaining retired `BuildingSlotMapMarker.tscn` shell was removed.
- `dotnet build rpg.csproj -maxcpucount:2 -v:minimal` completed with 0 warnings and 0 errors.
- `git diff --check` completed with no whitespace errors; it reported only existing CRLF-to-LF notices for two `BattleHitFeedbackRegression` test files.

Runtime foundation economy evidence:

- RED evidence: `dotnet run --project tests\StrategicManagementRegression\StrategicManagementRegression.csproj -v:minimal` failed after tests required first-slice building `ProvidedCapabilities` and constructed farm food production during elapsed world-map settlement.
- GREEN evidence: `dotnet run --project tests\StrategicManagementRegression\StrategicManagementRegression.csproj -v:minimal` passed after adding thin building-provided resource production capabilities, configuring farm/market/lumber camp/mine production, and settling constructed economy-building production through Strategic Management elapsed world-map time.
- Presentation/resource contract evidence: `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal` passed after the runtime economy slice; output retained existing Source Generator and nullable warnings in test files.
- Build evidence: `dotnet build rpg.csproj -maxcpucount:2 -v:minimal` completed with 0 warnings and 0 errors.
- Diff hygiene evidence: `git diff --check` completed with no whitespace errors.
- Scope note: this slice is launch-session runtime economy only. It does not add startup save loading, production queues, workers, road/path efficiency, reserve recovery, force-capacity bonuses, or battle Runtime changes.

Conscription and hero-first recruitment split evidence:

- `dotnet run --project tests\StrategicManagementRegression\StrategicManagementRegression.csproj -v:minimal` passed after adding manual reserve conscription, city-local automatic conscription intensity, hero-directed `RecruitCorpsForHero`, and the rule that replacing a hero's corps returns the previous corps to the city as an unassigned garrison corps.
- `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal` passed after keeping `征兵` as a normal city-panel tab and moving `招兵` into a modal military workbench that first selects a hero, then shows RTS-style `WorldMusterOptionCard` corps cards for that hero.
- `dotnet build rpg.csproj -maxcpucount:2 -v:minimal` completed with 0 warnings and 0 errors.
- `git diff --check` completed with no whitespace errors.

Battle-result return routing correction evidence:

- RED evidence: `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal` failed because post-battle routing still bound site UI for non-managed targets, presentation cleanup did not receive a `StrategicBattleResultSummary`, and the city panel gate still depended on legacy `WorldSite` ownership.
- GREEN evidence: `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal` passed after routing non-victory and non-managed-location victories directly back to the strategic world map, clearing stale legacy defender placements only after Strategic Management victory writeback, and gating city management UI through Strategic Management city ownership.
- Strategic writeback evidence: `dotnet run --project tests\StrategicManagementRegression\StrategicManagementRegression.csproj -v:minimal` passed after the routing correction, confirming victory/defeat consequence application remains owned by Strategic Management commands.

Strategic battle stale-handoff cleanup evidence:

- RED evidence: `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal` failed because strategic battle transition left a stale legacy `BattleSessionHandoff`, strategic result consumption did not clear that legacy handoff, and `WorldSiteRoot` still directly fell back to the legacy handoff for strategic requests without an active strategic context.
- GREEN evidence: `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal` passed after strategic transitions cleared any stale legacy handoff before destination scene entry, strategic result consumption cleared legacy handoff after active-context consumption, and `WorldSiteRoot` rejected stale legacy strategic requests instead of reopening battle preparation.
- Strategic writeback evidence: `dotnet run --project tests\StrategicManagementRegression\StrategicManagementRegression.csproj -v:minimal` passed after the stale-handoff cleanup, confirming Strategic Management battle-result application remains intact.
- Build evidence: `dotnet build rpg.csproj -maxcpucount:2 -v:minimal` completed with 0 warnings and 0 errors.
- Diff hygiene evidence: `git diff --check` completed with no whitespace errors.

Post-battle settlement modal evidence:

- RED evidence: `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal` failed because strategic battle result writeback did not persist Strategic Management state before feedback, battle completion still routed directly instead of showing a settled modal, and `PostBattleSettlementDialog.tscn` did not exist.
- GREEN evidence: `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal` passed after adding the resource-backed post-battle settlement modal, routing battle completion through player acknowledgement actions, and saving Strategic Management state after successful battle-result writeback.
- Strategic writeback evidence: `dotnet run --project tests\StrategicManagementRegression\StrategicManagementRegression.csproj -v:minimal` passed after the modal routing change, confirming Strategic Management battle-result application remains intact.
- Build evidence: `dotnet build rpg.csproj -maxcpucount:2 -v:minimal` completed with 0 warnings and 0 errors.
- Diff hygiene evidence: `git diff --check` completed with no whitespace errors.

Captured managed-target correction evidence:

- RED evidence: `dotnet run --project tests\StrategicManagementRegression\StrategicManagementRegression.csproj -v:minimal` failed because Bonefield still resolved as a non-city ruin and victory did not expose managed city state for post-battle management.
- RED evidence: `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal` failed because post-battle settlement layout still depended on hidden site HUD workspace reservation.
- GREEN evidence: `dotnet run --project tests\StrategicManagementRegression\StrategicManagementRegression.csproj -v:minimal` passed after treating Bonefield as an enemy-held managed city/stronghold with isolated city state and ownership-gated management view models.
- Presentation evidence: `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal` passed after adding explicit post-battle settlement dialog layout state so the result modal does not reserve the hidden left management workspace.
- Build evidence: `dotnet build rpg.csproj -maxcpucount:2 -v:minimal` completed with 0 warnings and 0 errors.
- Diff hygiene evidence: `git diff --check` completed with no whitespace errors.

Captured managed-city large-map action evidence:

- RED evidence: `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal` failed because selected managed-city actions still lacked the Strategic Management dashboard entry gate and could fall back to legacy world actions.
- GREEN evidence: `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal` passed after large-map `查看详情` / `出征` entry visibility and validation read Strategic Management location dashboards instead of legacy `WorldSiteState` ownership.
- Strategic writeback evidence: `dotnet run --project tests\StrategicManagementRegression\StrategicManagementRegression.csproj -v:minimal` passed after the action-gate correction, confirming battle-result ownership and production state remain Strategic Management-owned.
- Build evidence: `dotnet build rpg.csproj -maxcpucount:2 -v:minimal` completed with 0 warnings and 0 errors.
- Diff hygiene evidence: `git diff --check` completed with no whitespace errors.

Large-map resource feedback evidence:

- RED evidence: `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal` failed after the anti-rot tests required a rolling strategic resource ticker and settlement-event-driven large-map production float text.
- GREEN evidence: `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal` passed after replacing the top resource label with `WorldResourceTicker`, adding authored `WorldResourceFloatText`, and routing Strategic Management production settlement events through a focused strategic-world feedback partial.
- Strategic production evidence: `dotnet run --project tests\StrategicManagementRegression\StrategicManagementRegression.csproj -v:minimal` passed after the presentation feedback slice, confirming resource production settlement remains owned by Strategic Management commands.
- Build evidence: `dotnet build rpg.csproj -maxcpucount:2 -v:minimal` completed with 0 warnings and 0 errors.
- Diff hygiene evidence: `git diff --check` completed with no whitespace errors.

Strategic content config and sandbox naming evidence:

- RED evidence: `dotnet run --project tests\StrategicManagementRegression\StrategicManagementRegression.csproj -v:minimal` failed after tests required module-based Strategic Management config paths and rejected the retired root `first_slice_buildings.json` content entry.
- RED evidence: `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal` failed after tests required the default strategic world to use sandbox naming instead of `chapter_01`.
- GREEN evidence: `dotnet run --project tests\StrategicManagementRegression\StrategicManagementRegression.csproj -v:minimal` passed after splitting Strategic Management content into `economy/resources.json`, `economy/conscription_policies.json`, `cities/buildings_foundation.json`, and `military/corps_common.json`, then loading resources, conscription policies, foundation buildings, and common corps through the Strategic Management config boundary.
- Presentation naming evidence: `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal` passed after the default strategic world id became `default_sandbox_strategic_v1`, the title became `沙盒大世界`, and active player-facing world text stopped using chapter or discarded demo-target terminology.
- Legacy feedback fallback evidence: `dotnet run --project tests\BattleHitFeedbackRegression\BattleHitFeedbackRegression.csproj -v:minimal` passed after updating the old world battle-result fallback label away from the discarded demo target name.
- Build evidence: `dotnet build rpg.csproj -maxcpucount:2 -v:minimal` completed with 0 warnings and 0 errors.
- Diff hygiene evidence: `git diff --check` completed with no whitespace errors.
- Scope note: this slice does not introduce content packs. The first version splits Strategic Management config by module/function only.

Post-battle company ownership correction evidence:

- RED evidence: `dotnet run --project tests\StrategicManagementRegression\StrategicManagementRegression.csproj -v:minimal` failed because surviving expedition corps remained stationed at the source city after a victorious assault instead of moving to the captured managed city.
- RED evidence: `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal` failed because managed-city peacetime map presentation still had no gate preventing legacy `WorldSite` unit placements/garrison regeneration from drawing stale enemy units.
- GREEN evidence: `dotnet run --project tests\StrategicManagementRegression\StrategicManagementRegression.csproj -v:minimal` passed after victorious assault settlement moved surviving participant corps to the captured managed city while leaving routed participants out of the target city.
- Runtime repair evidence: `dotnet run --project tests\StrategicManagementRegression\StrategicManagementRegression.csproj -v:minimal` passed after `LoadSavedState` repairs older resolved-victory state where surviving expedition corps were incorrectly saved at the source city.
- Presentation evidence: `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal` passed after managed-city peacetime presentation stopped regenerating or rendering legacy `WorldSite` unit placements while preserving legacy placement rendering for battle preparation/runtime modes.
- Build evidence: `dotnet build rpg.csproj -maxcpucount:2 -v:minimal` completed with 0 warnings and 0 errors.
- Diff hygiene evidence: `git diff --check` completed with no whitespace errors.

Follow-up presentation slice:

- `demo_site.tscn` is the first test map for Strategic Management construction-region authoring.
- Three `ConstructionRegionMapMarker` markers represent the first city economy, military, and civic regions.
- Building placement preview follows the mouse, resolves the active marker-backed construction region, and displays real-time legality from Strategic Management rules before the click submits `BuildCityBuilding`.
- Building picker icons and mouse-follow previews use focused single-building `AtlasTexture` resources. They must not point directly at whole multi-building sprite sheets; those sheets are only source atlases.
- Building preview textures use 16x16 source-cell scale as the base and are drawn to the selected footprint's grid-space bounds. The preview must not draw the old n*m footprint grid; invalid placement feedback uses the preview tint and command failure notice instead.
- Building placement hover corner frames must be derived from the selected building's full footprint bounds, so 2x2 and 3x2 buildings frame the whole placement area instead of the mouse cell.
- Building placement mode suppresses the generic map 1x1 hover indicator while a building remains selected; the selected building footprint frame is the only placement hover cue.
- Confirmed city building instances must render as map entities rebuilt from Strategic Management `city.Buildings`; the map presentation is a read model and must not own construction state.
- 招兵 does not use the left sidebar as its main workspace because corps selection is unrelated to city-map placement. The left tab opens a modal military workbench that uses the whole management viewport: step one selects a hero, step two adjusts that hero's corps through RTS-style corps cards. 征兵 remains a normal city panel because it only manages the city's reserve-soldier pool.
- Strategic Management state persistence uses versioned JSON under `user://` through a Strategic Management save service/runtime boundary. Strategic commands mutate memory only; successful player-facing mutations such as building placement may ask the runtime to save after the command succeeds.
- The site peacetime management panel is a player-city workspace only. Battle preparation/runtime, non-city strategic locations, and non-player-held sites must not render the build/recruit/corps/overview tabs.
- Battle-result return routing is player-acknowledged after settlement. Runtime completion first applies Strategic Management writeback for location ownership, expedition resolution, and corps damage, then saves Strategic Management state. Only after that settled data exists may Presentation show the post-battle result modal. Defeat, withdrawal, and disaster expose a `返回` action. Victory exposes `返回` plus `管理城池` when the resolved target is now an implemented player-held managed city; non-managed victory targets still show the settled result modal but do not open the city-management workspace.
- Strategic battle victory may clean stale legacy `WorldSite` defender placement visuals after Strategic Management writeback, but this is only presentation-cache cleanup. Strategic Management location state remains the ownership/control authority.
- Construction-region marker coordinates and Strategic Management region definitions must stay aligned so marker-backed preview cells and command validation evaluate the same region bounds.
- Region labels such as economy, military, and civic are layout/readability labels only. They must not reject building categories.
- Large-map resource production feedback is presentation-only. World-clock settlement consumes `StrategicCommandResult` events emitted by Strategic Management and may show short-lived resource float text above the producing map location, but it must not recalculate or mutate resources in Presentation.
- The large-map top resource bar may animate visible number changes with a rolling text component. The component remains a HUD presentation detail and must read the same Strategic Management dashboard resources as the existing resource label.

Post-battle company ownership rule:

- Expedition lifecycle is Strategic Management runtime state, not a UI-only selection. Creating an expedition locks the selected hero companies by setting hero/corps expedition ids and corps expedition status, and clears the corps' current stationed city. `SourceLocationId` records the expedition's departure point only; it is not an ownership or station relationship after dispatch. The source city must not expose, count, replenish, or otherwise manage corps that are currently on expedition.
- A victorious assault moves each surviving participant corps to the captured managed city before unlocking it. A defeated or routed participant must not move into the target city. A reinforce expedition that arrives at an owned managed city resolves through Strategic Management and stations surviving participant corps at the target city. Presentation may display this state, but cannot maintain a separate city-to-company ownership list.
- Strategic expedition world-army carriers are large-map movement adapters only. Owned-target Strategic Management expeditions must not be blocked by legacy `WorldSite` garrison capacity and must not mutate legacy site garrison rows on arrival.

Expedition station ownership correction evidence:

- RED evidence: `dotnet run --project tests\StrategicManagementRegression\StrategicManagementRegression.csproj -v:minimal` failed after tests required dispatch to clear source-city station ownership and required owned-city reinforce arrival to settle through Strategic Management.
- RED evidence: `dotnet run --project tests\StrategicManagementRegression\StrategicManagementRegression.csproj -v:minimal` failed because moving expeditions still treated their `SourceLocationId` departure record as an invalid reinforce target and rejected return-to-departure-city retargeting with `same_location_target`.
- RED evidence: `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal` failed because Strategic Management reinforce carriers were still rejected by legacy `WorldSite` garrison capacity and target hover/selection still used legacy garrison-capacity blocking.
- RED evidence: `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal` failed after the selected-world-army command path still checked legacy garrison capacity before syncing Strategic Management owned-city reinforce retargeting.
- GREEN evidence: `dotnet run --project tests\StrategicManagementRegression\StrategicManagementRegression.csproj -v:minimal` passed after dispatch cleared moving corps station ownership, city dashboards excluded expedition corps from source-city lists and active forces, routed corps stopped auto-returning to source city, and reinforce arrival stationed surviving corps at the owned target city.
- GREEN evidence: `dotnet run --project tests\StrategicManagementRegression\StrategicManagementRegression.csproj -v:minimal` passed after retarget rules stopped treating `SourceLocationId` as an ownership/station blocker for moving expeditions, while creation-time same-location target rejection remains intact.
- Presentation/world evidence: `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal` passed after Strategic Management expedition carriers bypassed legacy garrison capacity and did not mutate legacy site garrison rows on owned-target arrival.
- Presentation/world evidence: `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal` passed after selected Strategic Management expedition carriers retargeted owned-city reinforce commands before legacy capacity checks, while non-strategic world armies kept the old capacity gate.
- Build evidence: `dotnet build rpg.csproj -maxcpucount:2 -v:minimal` completed with 0 warnings and 0 errors.
- Diff hygiene note: `git diff --check` was intentionally not rerun in this pass because the desktop Codex app was writing a large `C:\Users\qs\.codex\logs_2.sqlite` database and the user requested avoiding further disk pressure.

Large-map hover summary correction evidence:

- RED evidence: `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal` failed after the anti-rot test required large-map hover summaries to read Strategic Management location dashboards instead of legacy `WorldSiteState` resources and garrison facts.
- GREEN evidence: `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal` passed after `RefreshSiteHoverSummary` built a Strategic Management dashboard for the hovered map site and `WorldSiteHoverSummaryPresenter` rendered faction resources, city reserve capacity, and current-city hero company count from that dashboard.
- Scope note: the hover card is a Presentation read model. It must not show retired foundation fields such as population, legacy economy, legacy garrison army count, or placeholder ellipsis.

Manual QA:

- Archived with desktop Mono manual QA deferred by user request on 2026-06-30. The Manual QA list above remains historical reference if this proposal is reopened.

## Reopen Gate

If implementation needs to add population, logistics loss, beast-route rules, full local battle support, or combat Runtime changes beyond keeping the existing battle path working, stop and create an amendment design proposal before continuing.
