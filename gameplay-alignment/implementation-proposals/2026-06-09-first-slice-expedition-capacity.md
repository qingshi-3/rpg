# First Slice Multi-Company Expedition Implementation Proposal

Status: Implemented - Automated Verification Passed

## Authority

- Implements `gameplay-design/vertical-slices/first-playable-slice.md`, especially VS-04 and VS-05.
- Implements `gameplay-design/vertical-slices/first-playable-slice.md`, especially VS-07 and VS-08 reserve deployment rules.
- Follows `system-design/strategic-world-runtime-architecture.md` for `WorldArmyState` as strategic army state authority.
- Follows `system-design/world-battle-entry-architecture.md` for battle-entry grouping, preparation, Runtime handoff, and reserve exclusion.

## Scope

- Let one first-slice expedition draft select one to three available hero companies.
- Create one strategic expedition army carrying every selected hero company.
- Attach each selected hero company's default corps to that same strategic army.
- Preserve a first-slice hardcoded active expedition capacity until faction-level command capacity exists.
- Build one battle request from the arriving strategic army.
- Present carried hero companies as separate battle-preparation command groups.
- Allow battle launch when at least one carried company is deployed and planned.
- Exclude undeployed carried companies from the Runtime battle request so they do not spawn, fight, require plans, or take battle casualties in this slice.

## Non-Goals

- No faction progression, faction level, command building, or persistent faction capability model.
- No mid-battle reinforcement from undeployed reserve companies.
- No persistent long-term reserve workflow beyond the first-slice battle-entry boundary.
- No broad battle-preparation UI redesign beyond supporting carried-company grouping and reserve launch gating.

## Touched Systems

- Strategic expedition creation service.
- Strategic expedition start gating in the world presentation.
- Strategic expedition draft selection and default-corps summary.
- Strategic-to-battle request building.
- Battle force command-group identity.
- Battle-preparation launch validation.
- Battle Runtime handoff request filtering.
- Battle result writeback for deployed forces versus undeployed reserves.
- World action failure text.
- Focused world regression coverage.

## Tests

- Keep the service-level regression for the first-slice hardcoded active expedition capacity.
- Assert selecting one hero company in the draft does not clear other selected hero companies.
- Assert a multi-company draft creates one strategic army instead of multiple one-company armies.
- Assert default corps attachment loops across every selected hero company.
- Assert a multi-company source army builds player forces for every carried company.
- Assert battle command grouping is company-scoped while settlement source ids remain army-scoped.
- Assert battle launch validates only deployed player companies and requires at least one deployed company.
- Assert Runtime handoff removes undeployed reserve company forces and plans.
- Assert undeployed reserves are not resolved as battle casualties.

## Diagnostics And QA

- Reuse existing expedition creation diagnostics for successful launches.
- Add a low-noise diagnostic when battle preparation excludes reserve groups from Runtime.
- Manual QA: from the player camp, select two or three hero companies, send one visible expedition army to Bonefield, deploy one subset in battle preparation, start battle, and confirm only deployed companies appear in Runtime.

## Acceptance Evidence

- `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal` passed on 2026-06-09 after the multi-company expedition and reserve pruning implementation.
- `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal` passed on 2026-06-09 after adding company-scoped command-group ids.
- Both runs still report the existing Godot source generator warning that `GodotProjectDir` is null or empty in test projects; it did not block compilation or execution.
