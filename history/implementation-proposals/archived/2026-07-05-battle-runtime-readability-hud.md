# Battle Runtime Readability HUD Implementation Proposal

Status: Archived By User Request - Implemented

## Requirement

Improve battle readability by removing always-on unit health bars and adding a battle-runtime hero summary panel.

## Authority

- `gameplay-design/content-systems-long-term-design.md`: combat is hero-led light RTS; the player reads and commands battle groups rather than individual soldiers.
- `system-design/presentation-ui-layout-architecture.md`: battle runtime HUD belongs in `BottomCommandHost`; UI reads runtime/display facts and does not own combat truth.

## Scope

- Change world-space unit health bars to show from attention states: hover, selected command group, target preview, and action preview.
- Add a compact battle-runtime summary area to the bottom HUD.
- Show each player hero/battle group with hero HP, soldier count `n/m`, and aggregate corps HP.
- Use runtime corps actor HP for troop strength and existing presentation `HealthComponent` for current hero HP because Runtime hero HP is still a placeholder.

## Non-Goals

- No new combat rules, damage calculation, settlement logic, or persistence.
- No left-side battle-runtime management panel.
- No per-soldier permanent HUD.

## Touched Systems

- Presentation/Battle unit health-bar attention state.
- Presentation/World/Sites battle-runtime HUD binding and authored scene layout.
- Regression tests under `tests/BattleHitFeedbackRegression` and `tests/WorldSiteDeploymentCacheRegression`.

## Acceptance

- Unit head health bars do not stay visible merely because a unit is damaged or low HP.
- Hovering a battlefield unit can request temporary health-bar attention.
- Runtime bottom HUD has a player battle-group summary container.
- Summary rows display hero HP, troop count, and aggregate corps HP derived from runtime/presentation state without mutating Runtime.

## Verification Evidence

- `dotnet run --project tests\BattleHitFeedbackRegression\BattleHitFeedbackRegression.csproj -v:minimal`
- `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal`
- `git diff --check`
- `dotnet build rpg.csproj -maxcpucount:2 -v:minimal`
