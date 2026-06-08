# First Slice Hero And Skill Content Expansion Implementation Proposal

Status: Implemented - Automated Verification Passed

## Authority

- Implements `gameplay-design/vertical-slices/first-playable-slice.md`, especially VS-04 and VS-11 plus the minimum content counts.
- Implements `gameplay-design/details/heroes-and-corps/README.md`.
- Follows `gameplay-design/details/combat-command/README.md` for active-skill command shape.
- Follows `system-design/battle-content-progression-architecture.md` and `system-design/battle-command-architecture.md`.
- Uses `D:\godot\story\docs\production\first-slice-units-and-skills-guidance.md` as the production content handoff.

## Scope

- Replace the V0 one-hero slice constants with first-slice hero-company content for shield, archer, and assault lines.
- Add all three player heroes to the initial player camp garrison.
- Let expedition selection choose exactly one available hero company and automatically attach that hero's default corps.
- Populate Bonefield with one named leader plus two regular enemy unit roles.
- Define one active skill per player hero with original Chinese display names.
- Carry skill ownership from definition to snapshot so UI and Runtime expose only the selected hero company's skill.
- Rename selected unit resources to original Chinese player-visible names.

## Non-Goals

- No new shield, healing, control, area, or charge effect primitive.
- No full preparation three-choice flow.
- No equipment reward implementation.
- No multi-company player deployment in one battle.
- No new external asset import.

## Touched Systems

- Gameplay detail docs and this implementation proposal.
- Strategic world initial content and expedition drafting.
- World battle request construction and company grouping.
- Battle skill definitions, snapshots, UI filtering, and Runtime command validation.
- Focused regression tests for world/hero-company content and skill binding.

## Tests

- Update world-site deployment cache regressions so the first slice requires three selectable hero companies, one selected company per expedition, and Bonefield leader plus two regular enemy roles.
- Update hero-skill regressions so three skill definitions exist, each carries a caster unit binding, snapshots preserve bindings, the runtime HUD filters skills by selected company, and Runtime rejects skills not bound to the caster group.
- Run existing battle hit feedback and target battle architecture regression suites after focused tests.

## Diagnostics And QA

- Runtime rejection for an unbound skill must emit a low-noise reason code.
- Manual QA should launch from the main strategic world, open the expedition panel, confirm three named hero companies are visible, send one company to Bonefield, and verify only the selected hero's skill appears in battle.

## Acceptance Evidence

- `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal` passed on 2026-06-07.
- `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal` passed on 2026-06-07.
- `dotnet run --project tests\BattleHitFeedbackRegression\BattleHitFeedbackRegression.csproj -v:minimal` passed on 2026-06-07.
- The runs still report the existing Godot source generator warning that `GodotProjectDir` is null or empty in test projects; it did not block compilation or execution.
- Manual Godot UI QA was not run in this session. Suggested follow-up remains: launch the strategic world, confirm the three hero companies in expedition selection, send one company to Bonefield, and verify only that hero company's skill appears in battle.
