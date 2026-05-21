# Battle Open Attack Flow Field Cache Implementation Proposal

Status: Implemented - pending manual QA
Created: 2026-05-21

Originating Design Proposal: None
Related Implementation Proposals:
- `gameplay-alignment/implementation-proposals/2026-05-21-battle-runtime-spike-diagnostics.md`
- `gameplay-alignment/implementation-proposals/2026-05-21-battle-movement-performance.md`

Authority Documents:
- `system-design/battle-runtime-architecture.md`
- `system-design/battle-navigation-topology-architecture.md`

## Goal

Reduce Runtime movement spikes caused by repeated open attack-slot flow-field builds while preserving the current battle movement, target choice, attack-slot, footprint, and reservation semantics.

## Boundary

- Runtime remains authoritative for movement, target choice, reservations, damage, and action timing.
- This slice does not introduce navigation LOD, combat islands, approximate pathfinding, or Presentation-side truth.
- Same-tick released footprint cells remain blocked.
- Open attack-slot flow fields may be reused only inside one Runtime advance, where topology, tick-start occupancy, and actor decision facts are already fixed.

## Scope

1. Cache the flow field rebuilt from dynamically open attack slots.
2. Key the cache by actor footprint, attack range, and the exact ordered open attack-slot anchors.
3. Route target scoring and movement planning through the same cache owner instead of calling `BattleFlowFieldBuilder.PreferOpenAttackSlots` directly.
4. Add counters and regression coverage proving duplicate open attack-slot field builds are reused.

## Non-Goals

- No target selection rule changes.
- No attack-slot legality changes.
- No same-tick occupancy or reservation changes.
- No cross-tick cache in this slice.
- No static/dynamic field separation in this slice.
- No branch-and-bound target scoring in this slice.

## Acceptance

- Existing target choice, multi-unit navigation, same-tick blocking, and presentation timing regressions still pass.
- A deterministic many-vs-many scenario records open attack-slot cache hits.
- Open attack-slot cost-field build count is lower than open attack-slot requests when duplicate open-slot facts appear in one Runtime advance.
- `BattleRuntimeSpike` remains available to verify whether `flowFieldBuildMs` drops in manual QA.

## Verification Evidence

- Red: `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal` failed before implementation because `OpenAttackFlowFieldBuildCount` was missing.
- Red: `dotnet run --project tests\BattleHitFeedbackRegression\BattleHitFeedbackRegression.csproj -v:minimal` failed after adding monitor assertions because the open attack-slot cache monitors were not registered.
- Green: `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal` passed and printed `flowBuilds=219`, `flowHits=278`, `flowMisses=219` in the deterministic performance scenario.
- Green: `dotnet run --project tests\BattleHitFeedbackRegression\BattleHitFeedbackRegression.csproj -v:minimal` passed after registering `Battle/OpenAttackFlowFieldRequests`, `Battle/OpenAttackFlowFieldCacheHits`, and `Battle/OpenAttackFlowFieldBuilds`.
- Green: `BattleRuntimeSpike` summaries include `openAttackFlowFieldRequests`, `openAttackFlowFieldCacheHits`, and `openAttackFlowFieldBuilds` for manual runs.
- Green: `dotnet build rpg.csproj -maxcpucount:2 -v:minimal` passed with 0 warnings and 0 errors.
