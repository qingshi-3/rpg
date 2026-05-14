# Battle Hit Feedback

## Scope

Battle hit visuals are owned by the battle presentation layer. Combat rules, AP
spending, targeting validity, and turn flow remain unchanged.

## Contract

- Target grid highlights are preview-only. Actual attack execution must not paint
  the impacted cells yellow.
- When an attack animation starts, every impacted unit receives a red unit
  outline. The outline is cleared after the attack animation duration.
- Positive damage events spawn red floating damage numbers above the impacted
  unit body, not on the grid cell. The number starts close to the target and
  drifts mostly upward with a slight right offset.
- Feedback consumes `BattleActionResult.DamageEvents` so future multi-target
  abilities can reuse the same presentation path.
- Hover preview supports both enemy and friendly units. Enemy hover keeps hostile
  intent colors; friendly hover uses green movement cells and yellow attack cells
  while sharing the same unit fronting and information hint path.
- Friendly and enemy hover previews both keep post-move attack projection, so
  movement and attack ranges are shown as a combined preview. Hover projection
  skips per-origin `BattleThreatSource` allocation because the overlay only needs
  movement, attack, and target cells.
- Hover overlay updates are batched so movement, attack, and target layers rebuild
  the grid highlight overlay once per hover target instead of once per layer.
- Grid-range highlights are rendered through runtime-created presentation-only
  `TileMapLayer` children. The highlight overlay generates its own transient
  `TileSet` atlas from the active map tile size and applies cell diffs with
  `SetCell` / `EraseCell`; callers still pass semantic `BattleGridHighlightKind`
  values and do not know about tile sources or atlas coordinates.
- Runtime highlight layers are not map authority. They contain no authored
  walkability, navigation, collision, or targeting data; hover frames, path
  arrows, and target pointers remain lightweight vector overlay elements because
  they are small dynamic presentation details rather than large range fills.
- Attack and skill target selection consumes target entities resolved by the
  ability or intent systems. The preview layer must not turn those targets into
  yellow target cells; it fronts the target units and applies the same red unit
  outline used by hit feedback. When execution starts, hit feedback can keep the
  red outline active until the attack animation completes.
- Movement path previews keep tile path cells but do not draw directional arrow
  segments by default. Unit sprite visuals are scaled through a battle-wide
  presentation multiplier of `0.8` so authored per-unit visual resources remain
  intact while the rendered animation size is reduced.

## Verification

- Automated coverage: `tests/BattleHitFeedbackRegression` validates multi-target
  feedback planning, damage number motion defaults, friendly hover highlight
  style/workload selection, tile highlight diff behavior, and attack target
  presentation avoiding target grid cells. It also locks the default movement
  path arrow visibility and unit sprite scale multiplier.
- Manual visual QA should confirm the timing in a real battle scene because
  Godot animation and shader rendering are presentation concerns.
