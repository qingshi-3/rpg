# Deployment Cache Extraction

## Purpose

This is the first implementation slice after docs are accepted. It extracts deployment candidate construction from `WorldSiteRoot` so management placement, exploration entry, assault/defense setup, and future auto battle can share one owner.

Implementation plan: `09-deployment-cache-extraction-implementation-plan.md`.

## Current Context

Current implementation facts:

- `src/Presentation/World/Sites/WorldSiteRoot.cs` contains a private `WorldSiteRuntimeDeploymentCache`.
- `RebuildSiteDeploymentRuntimeCache` derives candidates from `_activeGridMap.Surfaces`.
- Candidate surfaces must be top surfaces, walkable, and have `MoveCost > 0`.
- Direction ordering currently exists in `OrderDeploymentSurfaceCandidates`.
- Water tagging currently uses terrain tags through `BattleRuleQueries.IsWater`.
- `WorldSiteDeploymentService` writes authoritative rows to `WorldSiteState.UnitPlacements`.

## Target Owner

Create a focused `WorldSiteRuntimeDeploymentCacheBuilder` and a cache value object.

Preferred ownership:

- place pure cache construction in the world/application side if it only depends on `Rpg.Domain.Battle.Grid`;
- do not introduce an `Application -> Presentation` dependency;
- if water detection needs extraction, create a pure terrain-tag query under the battle-grid domain instead of depending on `Rpg.Presentation.Battle.Rules.BattleRuleQueries`.

The builder may return `WorldSiteDeploymentCell` because that type already represents a world-side deployment candidate.

## Inputs

- `siteId`
- active `BattleGridMap`
- supported `WorldSiteAttackDirection` values

## Output

A cache value with:

- `SiteId`
- `CandidatesByDirection`
- `GetCandidates(WorldSiteAttackDirection direction)` with fallback to `Any`

## Required Candidate Rules

- Include only top surfaces.
- Include only walkable surfaces.
- Exclude surfaces with `MoveCost <= 0`.
- Preserve terrain tag.
- Preserve `IsWater`.
- Preserve side-specific ordering:
  - `North`: lowest Y first;
  - `South`: highest Y first;
  - `West`: lowest X first;
  - `East`: highest X first;
  - `Any`: closest to map center first.

## Root Changes After Extraction

`WorldSiteRoot` should:

- call the builder;
- store the returned cache;
- log cache summary;
- keep using the cache through existing placement and relocation paths.

It should stop owning:

- candidate filtering;
- direction ordering;
- water tagging for deployment candidates;
- cache value construction.

## Tests And Checks

Automated or isolated checks should cover:

- north/south/east/west/any ordering;
- water candidate preservation;
- non-top surfaces excluded;
- non-walkable surfaces excluded;
- zero-cost surfaces excluded;
- null or missing grid returns an empty cache, not a hidden fallback placement.

Manual smoke checks should use `docs/60-qa/testcases/auto-tactics-migration.md`.

## Non-Goals

- Do not redesign deployment UX in this slice.
- Do not build auto battle runtime in this slice.
- Do not rewrite `WorldSiteDeploymentService`.
- This slice did not remove legacy battle flow; final cleanup is tracked separately.

## Acceptance

- Deployment cache construction is testable without instantiating `WorldSiteRoot`.
- Existing battle entry still prepares placements from `WorldSiteState.UnitPlacements`.
- Existing site placement drag behavior still respects walkability, occupancy, and water restrictions.
- Logs still identify the site and candidate counts.
