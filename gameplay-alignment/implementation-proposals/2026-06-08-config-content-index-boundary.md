# Config Content Index Boundary Implementation Proposal

Status: Implemented - Automated Verification Passed

## Authority

- Implements `system-design/battle-content-progression-architecture.md`, especially the configuration index boundary.
- Originating design proposal: `design-proposals/archived/2026-06-08-config-content-index-boundary/`.

## Scope

- Add `config/` as the plain configuration index directory.
- Move first-slice strategic initial rosters from `assets/definitions/world/strategic_world_v1_initial_state.tres` into `config/world/strategic_world_v1_initial_state.json`.
- Move first-slice hero-company and Bonefield roster mappings from hardcoded C# lists into `config/battle/first_slice_hero_companies.json`.
- Add a first-slice battle-unit resource path index under `config/battle/`.
- Keep unit `.tres`, visuals, audio, SpriteFrames, scenes, and other authored resources in `assets/`.
- Replace old resource loading and hardcoded content lists with config-backed typed query wrappers.

## Non-Goals

- No broad rebalance of unit stats, skills, counts, or placement rules.
- No migration of actual unit resources out of `assets/battle/units/`.
- No full indexing of every imported unit resource beyond the current first-slice configured set.
- No Godot editor resource reimport work.

## Touched Systems

- Content/progression architecture docs and proposal records.
- First-slice config files under `config/`.
- Strategic world initial definition assembly.
- First-slice hero-company query wrapper.
- Battle unit resource lookup fast path.
- World-site deployment regression tests.

## Tests

- Add or update regression coverage requiring first-slice config files under `config/`.
- Assert the old `assets/definitions/world/strategic_world_v1_initial_state.tres` resource is gone.
- Assert `StrategicWorldV1DefinitionFactory` reads the JSON config path instead of `GD.Load` on the old resource.
- Assert `FirstSliceHeroCompanyIds` uses the config-backed catalog and no longer hardcodes `new[]` company content.
- Assert the unit resource index maps the configured first-slice unit ids to existing `unit.tres` resources.

## Diagnostics And QA

- Config loaders should throw explicit errors for missing files, ids, paths, empty companies, or non-positive counts.
- Manual QA after automated checks: open the strategic world, start an expedition, verify three hero companies, send one to Bonefield, and verify the selected company plus Bonefield roster appears in battle preparation.

## Acceptance Evidence

- `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal` passed on 2026-06-08.
- `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal` passed on 2026-06-08.
- `dotnet run --project tests\BattleHitFeedbackRegression\BattleHitFeedbackRegression.csproj -v:minimal` passed on 2026-06-08.
- The runs still report existing Godot source generator / nullable warnings in test projects; they did not block compilation or execution.
- Manual Godot UI QA was not run in this session.
