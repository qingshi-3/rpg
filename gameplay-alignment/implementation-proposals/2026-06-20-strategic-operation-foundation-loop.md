# Strategic Operation Foundation Loop

Status: Implemented - Automated Verification Passed; Manual QA Pending

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

This proposal implements the foundation loop only. Full local building battle support, in-battle support activation, beast-route content, and final city-map art authoring are later slices.

## Current State

Strategic Management already exists as the long-term strategic authority, but the current playable operation is still close to the old shell:

- city development still uses facility-slot language and `BuildFacility`;
- first content still emphasizes `BuildingMaterials`, beast source, and beast pen validation;
- city construction has no bounded construction-region placement model;
- city manpower has no reserve-soldier model;
- corps creation consumes resources but not reserve soldiers or city capacity;
- the dashboard summarizes existing city facts, but does not support the new construction/recruitment/resource loop;
- expedition and battle bridge behavior exists and should be reused without changing battle Runtime logic.

## Architecture Direction

The implementation should replace the old Strategic Management facility-slot model with the accepted city building model inside Strategic Management itself. Do not add a compatibility layer where facilities and buildings both own the same city-development facts.

Strategic Management remains the owner of definitions, durable state, rules, commands, view models, elapsed world-map settlement, and battle-result writeback. Presentation consumes view models and submits commands. Combat and the Strategic Battle Bridge are not changed for full local building support in this slice.

The implementation may temporarily break compilation during the refactor, but the proposal is accepted only when the final code compiles and the automated/manual verification below has evidence.

## Scope

### 1. Foundation Strategic Content

Replace first-foundation content with the accepted foundation resources and buildings:

- resources: `Money`, `Food`, `Wood`, `Ore`;
- city buildings: `Farm`, `Market`, `Lumber Camp`, `Mine`, `Training Ground`, `Tavern`, `Arrow Tower`, `Medical Shrine`;
- construction regions for the first managed city, with category compatibility and grid bounds;
- foundation common corps creation and replenishment costs that consume resources and reserve soldiers;
- first hostile/developable target using existing world-map and battle routing where practical, but with generic foundation rewards instead of beast-route unlock language.

The first implementation may keep existing battle map and large-map visual assets, including `DemoSite`/`Bonefield` style content, but the player-facing strategic reward should validate the foundation loop rather than the beast route.

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

- validating building placement by region type, footprint, bounds, overlap, construction state, and resources;
- building the foundation city buildings through commands;
- settling elapsed world-map time into foundation resource income and reserve recovery;
- deriving active forces and remaining reserve capacity;
- creating corps from resources plus reserve soldiers;
- replenishing damaged corps from resources plus reserve soldiers;
- assigning corps to heroes and forming dispatchable hero companies through existing hero/corps authority;
- creating expeditions from the city through the existing expedition command path;
- applying battle victory to occupy or develop the target strategic location through existing battle-result command boundaries where practical.

Commands must return explicit success, failure reason, changed facts, and low-noise strategic events. Failed commands must not partially mutate resources, reserve soldiers, building state, corps state, or expedition state.

### 4. Presentation Slice

Update the Strategic Management presentation so the player can exercise the loop from the current desktop Mono build:

- inspect city resources, reserve soldiers, and force capacity;
- see available construction regions and existing buildings;
- choose a building from a Chinese-labeled construction panel;
- preview a building footprint on the region grid and reject illegal placements with a clear disabled reason;
- place a valid building through Strategic Management commands;
- see elapsed world-map time produce resources and reserve recovery after leaving city management;
- recruit or replenish corps from the city;
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
- Define building income and capacity effects.
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
- Validate build -> income/reserve -> recruit/replenish -> form company -> expedition -> battle/occupy/develop target.
- Keep local building battle support out of completion criteria.

## Tests

Add or update Strategic Management regression coverage for:

- foundation resource definitions contain `Money`, `Food`, `Wood`, and `Ore`, and first-loop tests do not depend on `BuildingMaterials` or beast materials;
- first city exposes authored construction regions and legal building categories;
- valid building placement consumes resources and creates a building instance with region/grid position;
- invalid placement outside region, overlapping another building, wrong category, or insufficient resources fails without mutation;
- elapsed world-map settlement grants resource income from controlled city buildings;
- elapsed world-map settlement recovers reserve soldiers up to `CityForceCapacity - ActiveForces`;
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
- elapsed settlement resource and reserve recovery summary;
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
5. Returning to the world map resumes time; resources and reserve soldiers increase through elapsed settlement.
6. Recruiting or replenishing a corps consumes resources and reserve soldiers.
7. A hero company can be selected and dispatched from the city.
8. The expedition can reach the target and trigger the existing battle path.
9. Victory occupies or develops the target and the city/target dashboard reflects the result.
10. No local building battle support UI or in-battle support buttons are required for this slice.

## Acceptance

This implementation proposal is accepted when:

- Strategic Management no longer treats facility slots as the foundation city-development authority.
- The first foundation loop can be completed through the desktop Mono playable path.
- The first loop uses `Money`, `Food`, `Wood`, and `Ore`.
- City buildings are placed inside bounded construction regions through command-validated rules.
- Reserve soldiers and city force capacity constrain recruitment and replenishment.
- Existing battle entry/result boundaries still work for the expedition target.
- Automated verification commands pass.
- Manual QA evidence is recorded here after execution.

## Verification Evidence

Automated verification:

- RED evidence: `dotnet run --project tests\StrategicManagementRegression\StrategicManagementRegression.csproj -v:minimal` initially failed because Strategic Management view models still referenced deleted facility-slot APIs, including `StrategicCityState.FacilitySlotCount`, `StrategicManagementDefinitionSet.Facilities`, and `StrategicManagementCommandService.BuildFacility`.
- GREEN evidence: `dotnet run --project tests\StrategicManagementRegression\StrategicManagementRegression.csproj -v:minimal` passed after the building, construction-region, reserve, replenishment, expedition, and battle-result feedback implementation.
- Presentation anti-rot evidence: `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal` passed after updating the strategic dashboard binder and command routing to `BuildCityBuilding`, building placement defaults, and `ReplenishCorps`.
- Build evidence: `dotnet build rpg.csproj -maxcpucount:2 -v:minimal` completed with 0 warnings and 0 errors.
- Diff hygiene evidence: `git diff --check` completed with no whitespace errors.
- Legacy Strategic Management reference scan: old facility-slot, beast-route, and obsolete first-loop resource references are absent from Strategic Management definitions, state, rules, commands, config, and the strategic dashboard presentation path except for explicit negative regression assertions. Legacy WorldSite facility-slot presentation compatibility remains outside Strategic Management authority.

Follow-up presentation slice:

- `demo_site.tscn` is the first test map for Strategic Management construction-region authoring.
- Three `ConstructionRegionMapMarker` markers represent the first city economy, military, and civic regions.
- Building placement preview follows the mouse, resolves the active marker-backed construction region, and displays real-time legality from Strategic Management rules before the click submits `BuildCityBuilding`.

Manual QA:

- Pending desktop Mono playthrough. Required checks remain the Manual QA list above.

## Reopen Gate

If implementation needs to add population, logistics loss, beast-route rules, full local battle support, or combat Runtime changes beyond keeping the existing battle path working, stop and create an amendment design proposal before continuing.
