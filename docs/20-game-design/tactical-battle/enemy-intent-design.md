# Enemy Intent Design

This document defines the player-facing design of enemy intents.

`../../30-technical-design/battle/intent-system.md` defines the runtime contract. This document defines what the player should see, infer, and counterplay.

## Design Contract

Enemy intent is a readable tactical signal, not a locked exact action.

The player should be able to answer:

- What is this enemy trying to do?
- How soon does it become dangerous?
- What target, area, or direction does it care about?
- What can I do to alter or break the result?

Main intent icons:

`attack`, `advance`, `defense`, `support`, `control`, `mobility`, `summon`, `charge`, `retreat`, `unknown`.

Details such as poison, armor break, lifesteal, area targeting, and multi-target behavior are secondary details. They should appear in hover details or future secondary tags, not as the primary overhead intent icon.

## Attack / 攻击

### Player Meaning

`attack` means near-term threat.

The enemy already has a credible damage action and is expected to hurt a target on its next action if the player does not respond.

Player-facing interpretation:

```text
它要打人了。
如果我不处理，目标大概率会马上受到伤害。
```

`attack` is not a generic hostile mood. It should only be shown when the enemy is close to executing damage.

`attack` and `advance` must stay distinct:

- `attack`: the threat is already formed.
- `advance`: the threat is still forming.

### Possible Targets

Attack usually points to a unit.

Common target policies:

- Nearest reachable unit.
- Lowest health unit.
- Highest threat unit.
- Backline unit.
- Marked unit.
- Random unit among valid targets.

Attack may also represent area damage, but area damage is a detail of `attack`, not a separate main intent. The head marker remains `attack`; hover details and map overlay communicate the area.

### Map Presentation

When the player hovers an enemy with `attack`, the map should show an intent overlay:

- A red sniper-scope style frame around the predicted target unit.
- A thin red/orange lock-on line from the enemy to the target.
- A danger emphasis on the target cell, such as a red pulse or danger border.
- If the attack is area-based, show the affected area around the target point.

The overlay answers:

```text
谁会被打？
伤害从哪里来？
我能否通过移动、防御、控制或击杀改变结果？
```

### Lock-On Line Shape

The attack line is a threat line, not a movement path.

Recommended visual traits:

- Thin direct line from attacker to target.
- Red/orange color family.
- Semi-transparent with a subtle pulse.
- Small arrow head, hit spark, or reticle at the target end.
- No route bends unless the attack itself explicitly arcs or chains.

The attack line should not look like the `advance` arrow chain:

- `advance`: thick, segmented, strategic route.
- `attack`: thin, direct, tactical lock-on.

### Target Unit Marker

Use a sniper-scope style frame for the predicted target unit.

Rules:

- The scope surrounds the unit, not only the grid cell.
- It should be visually stronger than the attack line because the target is the most important information.
- It means "this unit is expected to be attacked soon".

If the target can be changed by player movement, hover details should make that clear through the target policy.

Example:

```text
目标规则：最近单位
```

This tells the player that moving a tougher unit closer may redirect the attack.

### Damage Information

Attack should show expected damage whenever possible.

Clear:

```text
预计伤害：2
```

Fuzzy:

```text
预计伤害：2-4
```

Unknown:

```text
预计伤害：?
```

Damage information is essential because the player cannot judge whether to block, tank, heal, or ignore the threat without it.

### Area Attack Detail

If the attack affects an area, keep the main intent as `attack` and add area detail.

Map overlay:

- Draw the target area with red translucent cells or a red danger circle.
- Draw a lock-on line from enemy to the area center or primary target.
- Keep the target unit frame only if a primary target exists.

Hover text:

```text
意图：攻击
方式：范围
目标：骑士附近
预计伤害：2
影响：3格范围
```

The `attack` area marker is different from the `advance` area marker:

- `advance` area means "the enemy wants to move or pressure there later".
- `attack` area means "this area is about to be hit".

### Information Precision

Attack can have three clarity levels.

Clear:

```text
Show exact target unit.
Show lock-on line.
Show expected damage.
```

Fuzzy:

```text
Show possible target group or area.
Show damage range.
Hover text says the target is not fully certain.
```

Unknown:

```text
Show only the attack icon.
Hover text says the enemy is preparing an attack, but target or damage is unclear.
```

Normal enemies should usually be clear. Elite enemies and bosses may be fuzzy, but the system should not feel like it is lying.

### Hover Text

Hover details should be compact and decision-oriented.

Examples:

```text
意图：攻击
目标：骑士
预计伤害：2
可干预：离开射程 / 防御 / 击杀 / 控制
```

```text
意图：攻击
目标：血量最低单位
预计伤害：2-4
可干预：治疗 / 保护 / 改变站位
```

```text
意图：攻击
目标：未知
预计伤害：?
可干预：侦察 / 拉开距离 / 控制
```

### Counterplay

Attack should create immediate tactical decisions:

- Move the target out of range.
- Redirect the target policy toward a tougher unit.
- Defend or gain armor.
- Heal before damage lands.
- Control the attacker.
- Kill the attacker.
- Create blockers if the rules support blocked attacks.

The most interesting attack intents are not only "take damage or kill enemy". They should allow the player to manipulate who gets hit and how hard.

### Conversion Rules

Typical conversions:

- `attack -> advance`: the target leaves range or becomes unreachable.
- `attack -> defense`: the enemy loses attack opportunity and is exposed.
- `attack -> retreat`: the enemy is low health and cannot safely attack.
- `attack -> unknown`: a special enemy hides its target or damage profile.

The player should feel that movement and control can transform immediate damage into future pressure or a weaker fallback.

### Implementation Notes

Do not store the final attack request directly in `BattleIntent`.

The intent should store high-level target policy and preferred ability. The overlay can ask the resolver or tactical query layer for a current predicted target, damage estimate, and affected area.

Suggested data shape for preview later:

```text
intent type: attack
target kind: unit / area / group / unknown
target entity or target area
clarity: clear / fuzzy / unknown
damage estimate: exact / range / unknown
affected area, optional
target policy text
```

The first playable version can render:

```text
enemy position -> target unit lock-on line
target unit scope
target cell danger pulse
short hover text with expected damage
```

## Advance / 推进

### Player Meaning

`advance` means future threat.

The enemy is not primarily announcing immediate damage. It is moving toward a tactical goal that will create pressure in the next 1-2 rounds if the player ignores it.

Player-facing interpretation:

```text
它正在推进。
现在未必马上打到我，但它正在靠近某个危险位置或目标。
```

`attack` and `advance` must stay distinct:

- `attack`: near-term threat; the enemy can likely hurt a target this round.
- `advance`: future threat; the enemy is trying to become dangerous soon.

### Possible Targets

Advance may point to:

- A player unit.
- A map area.
- A high ground or ranged firing position.
- A choke point or blocking position.
- A summon point, objective point, or scripted tactical region.
- A support position near an ally.

The target does not have to be a unit. This is important because enemies should sometimes fight for terrain, not only chase the nearest character.

### Map Presentation

When the player hovers an enemy with `advance`, the map should show an intent overlay:

- A chain of marching/war-plan arrows from the enemy toward its intended target.
- If the target is an area, draw a translucent red circle around the target area.
- If the target is a unit, draw a sniper-scope style target frame around that unit.
- The arrow chain should scale dynamically with distance, so long advances show more strategic segments and short advances remain compact.

The overlay should feel like a battlefield command map, similar to a commander drawing an attack route.

Do not use one huge arrow for long paths. One large arrow only communicates direction, which is not enough for counterplay. Players need to understand the rough route so they can decide where to move, block, delay, or prepare future traps.

### Arrow Chain Shape

The arrow chain is a strategic intent route, not a normal movement path line.

Recommended visual traits:

- Each segment has a thick tapered body.
- Each segment has a broad arrow head.
- Slight curve when useful for readability.
- Red-orange color family.
- Semi-transparent fill, stronger edge glow.
- No rectangular UI frame around the arrow.
- Segment spacing should leave the map readable.

The arrow chain should not look like the blue player movement path. It represents enemy intent, not player path preview.

Avoid drawing one arrow per tile. That turns the overlay into exact movement preview noise. The target is a rough command-route visualization.

### Route Simplification

Advance should use the enemy's predicted path as input, then simplify it into a small number of strategic arrow segments.

Recommended first-pass rule:

```text
raw_path = predicted path from enemy to target
turn_points = start, corners, important choke/high ground points, target
segment_count = clamp(ceil(raw_path_length / 12), 2, 8)
display_points = simplify raw_path into segment_count route points
draw arrow segment between each display point pair
```

Examples:

- A 6-cell push may show 2 small arrows.
- A 24-cell flank may show 3-4 arrows.
- A 100-cell long route may show about 8 arrows.

Long routes should show enough bends that the player understands the route, but not every tile.

### Dynamic Arrow Sizing

Arrow chain density follows enemy-to-target distance.

Recommended first-pass rule:

```text
segment_count = clamp(ceil(path_cell_count / 12), 2, 8)
shaft_width = clamp(7 + path_cell_count * 0.025, 9, 16)
head_size = clamp(16 + path_cell_count * 0.05, 20, 32)
alpha = 0.55 to 0.75
```

`path_cell_count` uses the predicted route length, not direct distance. This lets a route that bends around walls still communicate that the enemy is going around, not simply moving straight toward the target.

If the target is very close, keep a minimum of two compact arrow segments so the player still reads it as an intent marker.

### Target Area Marker

Use a red circular region when the advance target is an area.

Rules:

- The circle indicates the enemy's desired destination zone, not guaranteed exact destination.
- The circle may cover several cells.
- If the AI knows a precise destination, the circle can be small.
- If the AI only has a tactical region, the circle should be larger and softer.

This supports targets such as high ground, choke points, summoning circles, and defensive zones.

### Target Unit Marker

Use a sniper-scope style frame when the advance target is a unit.

Rules:

- The scope frame surrounds the target unit, not the target cell.
- It should be red/orange and semi-transparent.
- It means "this enemy is advancing because of this unit", not "this unit will definitely be attacked this turn".

This distinction matters. `advance` points to future threat; `attack` means near-term threat.

### Information Precision

Advance can have three clarity levels.

Clear:

```text
Show arrow chain following the approximate predicted route.
Show target marker.
Hover text names the tactical goal.
```

Fuzzy:

```text
Show fewer, broader arrow segments toward a region.
Show a larger target circle.
Hover text says the enemy is pushing toward a direction or area.
```

Unknown:

```text
Show only a short direction arrow or no map target.
Hover text says the enemy is advancing, but the goal is unclear.
```

First implementation may start with the clear version for normal enemies.

### Hover Text

Hover details should be compact and action-oriented.

Examples:

```text
意图：推进
目标：靠近骑士
威胁：下回合可能进入攻击范围
可干预：阻挡路径 / 后撤 / 控制
```

```text
意图：推进
目标：北侧高地
威胁：占据射击位置后压制后排
可干预：抢占高地 / 阻挡通道
```

```text
意图：推进
目标：未知区域
威胁：正在形成未来威胁
可干预：侦察 / 拉开距离 / 控制
```

### Counterplay

Advance should create meaningful responses:

- Block the path.
- Move the threatened unit.
- Occupy the target area first.
- Pull the enemy toward a worse route.
- Control or slow the enemy.
- Kill the enemy before it reaches the tactical point.

If the player cannot respond meaningfully, `advance` becomes decorative and should be reconsidered.

### Conversion Rules

Typical conversions:

- `advance -> attack`: the enemy reaches a position where it can threaten a target this round.
- `advance -> defense`: the target area becomes blocked or the enemy must hold position.
- `advance -> retreat`: the enemy is heavily damaged or its push becomes unsafe.
- `advance -> unknown`: a special enemy hides its exact objective.

The player should feel that good positioning can transform enemy future threat into a weaker action.

### Implementation Notes

Do not store the final movement destination directly in `BattleIntent`.

The intent should store high-level policy and target semantics. The overlay can ask the resolver or a tactical query service for a current predicted target and display vector.

Suggested data shape for preview later:

```text
intent type: advance
target kind: unit / area / direction / unknown
target position or area
clarity: clear / fuzzy / unknown
predicted route, optional
threat horizon: 1 / 2 / unknown rounds
```

The first playable version can render:

```text
enemy position -> simplified predicted route arrow chain
target unit scope OR target area circle
short hover text
```
