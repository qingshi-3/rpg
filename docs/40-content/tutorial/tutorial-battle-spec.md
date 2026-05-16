# Tutorial Battle Spec

This legacy tutorial spec must not define the future combat identity. Current combat direction comes from `../../../gameplay-design/content-systems-long-term-design.md` and the combat-command detail docs.

The encounter should teach deployment, hero-led company selection, separate hero/corps/combined commands, readable automatic soldier behavior, and final report interpretation. It should not teach individual soldier micro, AP spending, player turns, or manual action menus.

## Battlefield

- Grid size: 6x6.
- Columns use `x = 0..5` from left to right.
- Rows use `y = 0..5` from the player entrance toward the enemy side.
- Coordinates are written as `(x, y)`.

Suggested layout:

```text
y5  .  .  T  .  .  .
y4  .  .  .  .  S  .
y3  .  .  X  X  .  .
y2  .  .  X  X  .  .
y1  .  W  .  A  .  .
y0  .  .  .  .  .  .
    x0 x1 x2 x3 x4 x5
```

Legend:

- `W`: frontline player unit.
- `A`: ranged player unit.
- `S`: melee enemy.
- `T`: ranged enemy.
- `X`: blocking obstacle.
- `.`: walkable cell.

## Teaching Goals

- Show that deployment cells and obstacles matter before battle starts.
- Show that the player commands hero companies rather than individual soldiers.
- Show that soldiers fight automatically after commands are issued.
- Show that attack, movement, defeat, command, and outcome events are readable.
- Show that the final report explains survivors, losses, contribution, command impact, and failure reason.

## Unit Behavior Targets

- Frontline player unit moves toward the nearest hostile and attacks in melee range.
- Ranged player unit prefers a target it can already reach; otherwise it repositions conservatively.
- Melee enemy closes distance and attacks adjacent targets.
- Ranged enemy attacks from range when line of sight and range allow it.

These are behavior targets for authored content and future combat architecture. Do not add AP, player turns, or the old action menu to force these outcomes.

## Required Checks

- Deployment validity is visible before battle start.
- Battle can run to victory or defeat with medium-frequency company commands and automatic soldier behavior.
- Important command, attack, movement, skill, defeat, and outcome events are present in playback or event feed.
- Final report includes survivor/loss counts, concise contribution facts, and command-relevant failure reasons.

Historical auto battle playback/report notes live in `docs/50-production/technical-changes/2026-05-16-auto-tactics-migration/05-playback-ui-and-report.md`, but they are reference material only.
