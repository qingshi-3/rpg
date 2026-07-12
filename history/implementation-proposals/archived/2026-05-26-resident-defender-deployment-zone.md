# Resident Defender Deployment Zone Implementation Proposal

Status: Archived - implemented resident defender deployment-zone alignment record
Created: 2026-05-26

Authority Documents:
- `system-design/world-battle-entry-architecture.md`
- `system-design/world-site-management-architecture.md`
- `system-design/semantic-map-marker-architecture.md`

## Goal

When entering battle preparation for a hostile site, resident defender forces should start inside the authored enemy deployment zone instead of inheriting stale or default garrison coordinates outside that zone.

## Scope

1. Keep Application deployment preparation as the owner of request placement generation.
2. Preserve side-aware semantic deployment markers as the battle-start constraint.
3. Move resident site placement rows to an authored side deployment zone before preferred placements are exported to `BattleStartRequest`.
4. Keep non-resident attacker/defender placement creation behavior unchanged.

## Non-Goals

- No Runtime movement, AI, or combat placement override.
- No new deployment UI flow.
- No scene marker authoring changes.

## Verification

Run after implementation:

```powershell
dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal
dotnet build rpg.csproj -maxcpucount:2 -v:minimal
dotnet build-server shutdown
```

## Implementation Evidence

- Added resident-force deployment-zone alignment in `WorldSiteBattleDeploymentPreparer`.
- Resident site forces still keep their existing site-local placement identity, but battle preparation moves that placement into the authored side deployment zone before exporting preferred placements to the battle request.
- Non-resident force placement creation remains on the existing `EnsureBattlePlacementsForForce` path.
- Added a regression covering resident defenders that previously inherited a default garrison cell outside the enemy deployment marker.

## Verification Evidence

- Passed: `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal`
- Passed: `dotnet build rpg.csproj -maxcpucount:2 -v:minimal`
- Passed: `dotnet build-server shutdown`
