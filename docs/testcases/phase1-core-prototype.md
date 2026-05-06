# Phase 1 Core Prototype Test Cases

Applies to:

- `docs/roadmap/development-priority.md`
- `docs/content/tutorial/tutorial-battle.md`
- `docs/design/battle/technical-architecture.md`

## Focus Checks

- [ ] Grid readability: the tutorial battle terrain, height, water, and blocked cells are readable during play.
- [ ] Cell occupancy: player units, enemies, obstacles, and empty cells are visually distinguishable.
- [ ] Turn order: the battle advances through player and enemy phases without ambiguous ownership.
- [ ] AP visibility: the player can see current AP before choosing an action.
- [ ] AP cost clarity: action costs are visible before confirmation.
- [ ] Intent readability: each enemy shows a high-level Intent marker before it resolves.
- [ ] Intent hover preview: hovering an enemy in neutral state shows predicted target, path, or affected cells without debug-only wording.
- [ ] Intent counterplay: the player can move or attack to change at least one enemy predicted outcome within the same high-level Intent.
- [ ] Basic ability resolution: move and attack produce visible results.
- [ ] Unit animation hooks: configured units play move, idle, attack, hit, and defeated cues without changing AP, turn, or damage logic.
- [ ] Terrain access: ordinary units cannot move through water unless explicitly allowed.

## Regression Checks

- [ ] Stored high-level Intent does not change during normal flow unless modified by an Effect.
- [ ] Intent preview does not recompute or write logs every frame while hovering the same enemy and unchanged battlefield state.
- [ ] AP remains a shared team resource, not a per-unit resource.
- [ ] Repeated action cost increases remain understandable and visible.
- [ ] Enemy actions still resolve from the previously displayed high-level Intent.
- [ ] Enemy AI uses `IEnemyIntentPlanner` plus `BattleIntentResolver`; no legacy concrete action planner path remains.
- [ ] Unit animation clips are optional presentation config; missing config does not block battle logic, while missing configured clips produce low-noise warnings.

## Unverified Checks

- [ ] Whether the final UI treatment makes Intent understandable without reading debug text.
- [ ] Whether AP decisions feel meaningful after real playtest pacing is available.
- [ ] Whether future minion rule execution follows priority order after RuleSystem implementation.
- [ ] Final authored unit animation clips and sprite tracks are not configured in this framework pass.
