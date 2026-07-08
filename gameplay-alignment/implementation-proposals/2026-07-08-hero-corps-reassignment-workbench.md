# Hero Corps Reassignment Workbench Implementation Proposal

Status: Implemented - Pending Manual QA, Presentation Suite Blocked By Unrelated Taxonomy Guard

## Origin

- Requirement: SM-HERO-CORPS-001
- Design Proposal: `design-proposals/archived/2026-07-08-hero-corps-reassignment-workbench/`
- Authority:
  - `gameplay-design/content-systems-long-term-design.md`
  - `gameplay-design/details/heroes-and-corps/README.md`
  - `gameplay-design/details/cities-and-locations/README.md`
  - `system-design/strategic-management-system-architecture.md`
  - `system-design/presentation-ui-layout-architecture.md`
- Parent Implementation Proposal: None
- Supersedes: None
- Superseded By: None
- Amends: None
- Amended By: `design-proposals/archived/2026-07-08-muster-card-resource-chip-display/`
- Blocking Issues: None known.

## Requirement

Implement the accepted city recruitment workbench flow where selecting a troop option replaces the selected hero's main corps. Replacement fully refunds the old corps' current-strength reserve soldiers and resource value with no extra replacement loss, creates and assigns the new corps, and lets recruitment cards show the selected corps reserve-soldier and resource requirements as compact attributes. Remove the separate first-version corps tab because it has no distinct accepted workflow.

## Scope

- Add Strategic Management rules/view-model support for hero main-corps replacement projections:
  - selected troop consume cost;
  - old-corps refund based on current remaining strength;
  - final net reserve/resource delta.
- Change the hero-directed replacement command so it does not park the previous corps as a hidden city inventory item.
- Preserve command validation through Strategic Management rules and explicit failure reasons.
- Update military workbench binding so troop option cards show visible reserve-soldier and resource requirement indicators below the unit display.
- Remove the site-management Corps tab button and its old section from the first-version city panel flow.
- Keep replenishment support for existing corps instances at the command/rule level, but do not expose it through the removed first-version tab.
- Add focused regression coverage for replacement settlement, view-model projections, UI scene structure, and tab removal.

## Non-Goals

- Do not add multiple main corps per hero.
- Do not add a broad corps inventory, drag assignment, or garrison-management workflow.
- Do not change battle Runtime, battle preparation, expedition travel, or battle-result settlement.
- Do not create individual soldier records.
- Do not restyle the recruitment UI beyond replacing verbose settlement rows with compact requirement indicators.
- Do not delete lower-level replenishment command support in this slice.

## Touched Systems

- `src/Application/StrategicManagement/`
- `src/Domain/StrategicManagement/`
- `src/Presentation/World/Sites/`
- `scenes/world/ui/WorldSitePeacetimeHud.tscn`
- `scenes/world/ui/WorldMusterOptionCard.tscn`
- `tests/StrategicManagementRegression`
- `tests/WorldSiteDeploymentCacheRegression`

## GodotPrompter Skills

- `csharp-godot`
- `godot-ui`
- `godot-testing`

## Tests

- Add Strategic Management regression coverage that replacing a hero's corps:
  - removes the previous corps instance from player-managed corps state;
  - refunds current-strength reserve soldiers and resource value;
  - charges the new corps creation cost;
  - assigns the new corps to the hero;
  - reports consume/refund/net facts.
- Add regression coverage for a damaged old corps so refund follows current strength and does not restore battle losses.
- Add view-model coverage that each muster option can expose consume/refund/net values for the currently selected hero.
- Add Presentation anti-rot coverage that:
  - `WorldSitePeacetimeHud.tscn` no longer contains `CorpsTabButton` or `SiteCorpsSection`;
  - `WorldMusterOptionCard.tscn` has visible compact requirement indicators instead of relying only on tooltip text;
  - the workbench binder binds selected-corps reserve and resource requirements from Strategic Management view models without printing refund/net rows on cards.
- Run `dotnet run --project tests\StrategicManagementRegression\StrategicManagementRegression.csproj -v:minimal`.
- Run `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg`; accept only known unrelated guard failures if they remain unrelated.
- Run `dotnet build rpg.csproj -maxcpucount:2 -v:minimal`.
- Run `git diff --check`.

## Diagnostics

- Add low-noise Strategic Management command events for replacement settlement, including hero id, new corps id, old corps id, reserve consumed, reserve refunded, and net reserve delta.
- Do not add per-frame or high-frequency UI logs.
- Missing authored UI nodes should fail through existing node-resolution warnings or focused regression guards, not silent fallback controls.

## Manual QA

- Open city management and confirm there is no separate `编制` tab.
- Open `招兵`, select each hero, and confirm the selected hero's current corps is shown.
- Confirm each troop option directly displays required reserve soldiers and resource costs as compact icon/amount attributes.
- For a hero with an existing corps, confirm each option does not print separate refund or final net-impact rows.
- Replace a hero's corps and confirm the hero shows the new corps.
- Confirm the old corps does not appear as a hidden extra row in the city UI.
- Confirm resources and reserve soldiers change by the displayed net amount.

## Acceptance

- The design proposal is archived and authority documents are current.
- The implementation proposal names the originating design proposal and authority documents.
- Strategic Management owns replacement validation, refund calculation, mutation, and event reporting.
- Presentation displays selected-corps reserve/resource requirements from view models and submits the replacement command.
- The separate first-version corps tab is removed from the player-facing site-management tab bar.
- Focused Strategic Management and Presentation regression guards pass, with only documented unrelated blockers accepted.

## Verification Evidence

- 2026-07-08: `dotnet run --project tests\StrategicManagementRegression\StrategicManagementRegression.csproj -v:minimal` passed. The replacement settlement and dashboard projection cases pass, including full current-strength refund, old-corps removal, new-corps assignment, and consume/refund/net reporting. The run still prints the existing Godot source-generator warning when `GodotProjectDir` is not supplied to this regression project.
- 2026-07-08: UI amendment accepted through `design-proposals/archived/2026-07-08-muster-card-resource-chip-display/`; recruitment cards now target compact reserve/resource requirement indicators instead of visible consume/refund/net rows.
- 2026-07-08: stale-reference scan found no production references to the removed corps-tab/row paths; remaining hits are anti-rot assertions in tests.
- 2026-07-08: `dotnet build rpg.csproj -maxcpucount:2 -v:minimal` passed with 0 warnings and 0 errors.
- 2026-07-08: `git diff --check` exited 0; Git reported a line-ending normalization warning for `tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegressionCases.PresentationAntiRot.cs`.
- 2026-07-08: After the resource-chip amendment, the Presentation regression guard was verified red before implementation, then `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal /p:GodotProjectDir=D:\godot\rpg` passed the task-specific `world site recruitment uses hero first military workbench` guard. The suite still exits nonzero on the unrelated resource taxonomy guard: `TileSets expected=0 actual=2`.
- 2026-07-08: After the resource-chip amendment, `dotnet run --project tests\StrategicManagementRegression\StrategicManagementRegression.csproj -v:minimal` passed, `dotnet build rpg.csproj -maxcpucount:2 -v:minimal` passed with 0 warnings and 0 errors, and `git diff --check` exited 0 with the existing line-ending normalization warning for `tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegressionCases.PresentationAntiRot.cs`.
