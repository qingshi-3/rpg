# Combat Command Detail

## Parent Authority

Global rules live in `../../content-systems-long-term-design.md`, especially the combat model and battle report sections.

## Boundary

This detail area defines player-facing battle command rules:

- selecting a battle group;
- separating hero command, corps command, and combined command;
- hero hold/attack/retreat/skill behavior;
- corps advance/guard/hold/attack/retreat behavior;
- command cadence for medium-frequency light RTS;
- command feedback and battle-report implications.

## To Refine

- Post-v0.1 command list beyond battle start.
- Command UI expectations after the first playable slice.
- What commands are allowed while the hero or corps is routed, casting, stunned, retreating, or separated.
- How command state is explained in the battle report.

## V0.1 Playable Slice

The first playable slice proves entry into hero-led battle without requiring the full light RTS command UI.

Required player flow:

1. The player clicks `出征` on the world map.
2. The player selects a hero in a Chinese expedition panel.
3. The selected hero brings a default corps.
4. The player right-clicks an enemy strategic location.
5. The battle group travels on the world map toward the enemy strategic location.
6. After arrival, the player chooses to enter the assault battle.
7. The game enters pre-battle deployment.
8. The player confirms or places the battle group in a valid deployment zone.
9. The player clicks `开战`.
10. Real-time battle starts.

V0.1 does not require post-start hero/corps command controls. Those controls remain the next combat-command iteration after this playable path is usable.

## Target Deployment And Beacon Command Flow

Battle preparation should remain a short deployment and initial-intent step. It creates valid starting placement, formation, and initial destination-beacon facts for each participating player battle group, but it no longer requires objective-region or engagement-rule selection before the battle clock starts.

The player operation is:

1. Click a battle group in the compact battle-preparation roster.
2. The selected battle group enters placement-follow mode: its default current-battle formation follows the mouse over the battlefield, including the hero and corps formation footprint.
3. Move the preview over the friendly deployment area; valid placements render normally, while invalid placements render the whole preview red and keep placement-follow mode active.
4. Click a legal placement to commit the battle group's initial formation.
5. After placement, immediately enter destination-targeting mode for the selected group.
6. During destination targeting, hide persistent UI and the system cursor, keep grid hover visible, and draw a curved arrow guide from the placed battle group toward the pointer.
7. Left-click a reachable destination cell during battle preparation to create or move the selected battle group destination beacon.
8. Repeat the flow for every participating battle group, or multi-select deployed groups and assign them one shared destination beacon.
9. Confirm deployed groups with initial destination beacons through the lower-right start-battle button.

The preparation roster is a compact index and placement source, not a long information panel. It should show only battle-group identity and deployment status, such as deployed, needs destination, reserve, or invalid. Detailed deployment facts belong to the selected battle-group feedback, map overlays, or launch validation feedback.

During placement-follow mode and preparation destination-targeting mode, persistent UI should avoid covering the battlefield so the player can read the map. The roster, top status, deployment controls, and start-battle button leave the battlefield view and ignore mouse input, then return after the operation is committed or cancelled. Deployment legality feedback remains visible through the formation preview and deployment-area highlight. Destination-targeting feedback remains visible through a curved arrow guide from the placed battle group to the pointer, plus the normal grid hover used for cell localization.

The default battle-group posture is attack. The player expresses movement intent through destination beacons in both preparation and live battle, but the preparation input state is intentionally focused:

1. Select one or more deployed battle groups during preparation, live battle, or tactical pause.
2. In battle preparation destination-targeting mode, left-click a valid destination cell; during live battle or tactical pause, use the accepted runtime destination-beacon input gesture for that command UI.
3. If every selected group can reach the destination under its required static topology and footprint profile, place one shared destination beacon.
4. Assign that beacon to the selected battle groups as a player-sourced combined command.
5. Leave all non-selected battle groups on their current command, local-combat, fallback, or autonomous state.

The first version treats a multi-select beacon command as atomic: if any selected battle group has no valid static route to the destination, the command is rejected with player-facing feedback and no selected group changes target. A later accepted command design may add explicit partial acceptance.

Destination beacons are not pure UI pings. They are runtime command target objects or preparation-seeded command facts with an owner command, location, version, validity state, and reportable command source. Moving a beacon or issuing a new beacon for the same selected groups supersedes their previous player destination at the next valid command boundary. A preparation-seeded beacon becomes the battle group's initial active beacon when Runtime starts.

Objective areas remain authored tactical regions for scenarios, enemy intent, route hints, reports, tutorials, and possible future optional planning views. Engagement rules remain a future command/posture vocabulary. They are not mandatory battle-preparation choices in the accepted first playable flow.


## Intent-Directed Movement And Local Combat

A battle group has one owner for tactical movement intent: the battle group itself. Its current destination beacon, target region, temporary target region, local combat region, and engagement state are not global battle facts, even when Runtime caches snapshots for query or diagnostics.

Out of combat, movement is intent-directed through stable target objects or destination beacons. A group should advance, hold, defend, withdraw, harass, or reposition toward a target object selected by command, beacon, future plan, or AI intent rather than chase a moving unit. In combat, the group may lock concrete actor targets, attack slots, and support slots, but only inside its current command scope and local combat region.

Enemy-controlled groups use configurable AI intent plans to choose target objects and replan regions. Player-controlled groups use player commands, including destination beacon commands, to choose or replan movement goals. Player groups may reuse local combat solvers, but their movement intent is not changed by enemy-style automatic policy.

A group exits engaged state only after the whole group has no perceived enemy for the configured disengage period and no recent damage or attack event keeps the local combat active.

## Square-Grid Realtime Battle Contract

The live battle space uses square-grid anchored realtime combat. The player still deploys units onto valid square cells, and the battle map keeps its existing square grid. After the battle starts, units move and fight automatically inside the current command or default posture.

Battle movement is realtime presentation over grid authority:

- Each unit has an anchored square cell and may reserve a next square cell while moving.
- Movement is displayed as smooth travel between cell centers.
- A unit may path toward targets in any valid square-grid direction. The first expected implementation uses 8-neighbor movement unless map data forbids diagonal transitions.
- Runtime builds the static navigation graph once for the battle handoff. Static graph legality owns terrain, walkable surfaces, height-specific surfaces, and authored height connections.
- Runtime may evaluate a beacon flow field several cells ahead, but it commits only the next neighbor cell. Replanning happens at valid runtime decision boundaries from current actor, beacon or target, occupancy, reservations, local combat state, and command posture.
- Without explicit AI override, ordinary assault follows the selected battle group's active destination beacon or default attack posture first. Target acquisition chooses enemies inside the command scope, retains valid targets, and scores attack slots only when acquisition or attack-slot approach is actually needed.
- When the target is already engaged, nearby support should reinforce the local fight if a valid attack slot is reachable. Support positioning is a fallback when direct attack slots are unavailable or tactically constrained.
- Living-unit occupancy is layered: the immediate next footprint is a hard blocker, while future occupied cells are soft route cost. This keeps a moving actor from treating another unit as a permanent wall, while still making comparable open routes more attractive.
- Same-tick reservations are hard blockers for the next committed footprint and reject opposite-edge swaps.
- A unit cannot perform a basic attack while in transit between cells.
- A unit may attack once it is anchored and the target actor is within the ability's grid range.

This model is not a side-scrolling lane battle and does not restrict units to attacking only forward enemies. It is also not freeform physics RTS movement. The square cell remains the readable tactical position and command surface.

### Destination Beacon Flow Fields

Destination beacon pathing is a navigation-intent layer, not a presentation layer.

Each active destination beacon may own or reference one flow-field cache per required footprint/passability profile. The flow field is built from immutable battle topology plus static footprint legality. It is rebuilt when the beacon changes, the topology version changes, the passability profile changes, or the command is invalidated. It is not rebuilt every frame or every time a unit moves.

The flow field must not contain living-unit occupancy, same-tick reservations, attack-slot assignments, local combat targets, damage state, or presentation facts. Those remain Runtime validation and tactical-state concerns. A unit following a beacon flow field still commits only legal neighboring anchors, still respects occupancy and reservation rejection, and may degrade into queue, support, local combat, hold, or failure states when the next step is blocked.

Presentation displays the beacon and animates movement events emitted by Runtime. It does not sample the flow field, move actors visually along uncommitted paths, or create alternate movement truth. This keeps pathfinding algorithm changes from directly changing movement interpolation or combat animation.

## Ability Targeting Direction

Ability content should support multiple targeting styles without changing the battle identity:

- Unit target: lock onto one battle actor.
- Cell target: lock onto a square cell.
- Direction target: cast toward a resolved direction.
- Self-centered: resolve from the caster.

Direction handling is part of the ability definition. Common direction modes are free angle, 8-way square-grid snap, 4-way cardinal snap, and forward arc. Area handling is also definition-driven, with shapes such as single actor, single cell, line, cone, circle radius, and grid radius.

The first implementation only needs basic actor-target attacks and the data-contract extension points for future abilities. Full projectile and area-skill behavior can be added later through these target and direction contracts.

## Skill Input, Availability, And Presentation Traits

Battle skill UI should consume command traits from the compiled skill snapshot instead of branching on concrete skill ids.

Skill snapshots should expose the player-facing command facts needed for input and feedback:

- command channel and caster eligibility;
- targeted, cell, direction, mark-selection, or self-centered input flow;
- range, area, direction-snap, preview, and two-stage selection traits;
- cost, cooldown, charge, limited-use, and disabled-reason display facts;
- presentation profile such as icon, cast preview, targeting cursor, range overlay, impact preview, and report label.

Concrete skill ids identify content for lookup, unlocks, reports, and migration. They must not be the UI's main behavior switch. A new skill that uses existing targeting, availability, effect, and presentation traits should not require a new UI branch.

## Unit Footprints

Larger units may use grid footprints while keeping the square-grid realtime model. A unit footprint is a rectangular set of occupied cells such as `1x1`, `1x2`, `2x1`, `2x2`, or `3x3`.

The anchor cell is always the footprint's top-left cell. The anchor is a compact state identifier, not the terrain legality rule by itself. Movement, deployment, attack range, area overlap, occupancy, and reservation resolve by expanding the anchor into the full covered footprint.

Static navigation legality is footprint-aware. A candidate anchor is legal only when every covered cell required by that footprint exists in the compiled battle topology. Same-level movement between anchors is legal only when the actor footprint can move from source anchor to target anchor without cutting blocked corners or passing through missing covered terrain. Runtime may still path over an anchor graph or destination-beacon flow field, but that graph or field must already represent valid footprint placement for the actor size.

Unit body collision is stricter than terrain navigation. Other units cannot occupy or reserve any cell inside the committed candidate footprint. During a live movement tick, each actor proposes one next anchor cell, reserves the full candidate footprint, and cannot directly swap edges with another same-tick mover. Tick-start occupied cells remain blocked for immediate committed movement throughout that tick, even when the occupying actor also moves during the same tick. A released footprint may be entered only after Runtime advances to a later decision boundary with updated occupancy. Future projected cells may include occupied cells as soft cost, not hard legality, because those units may move before the actor reaches that part of the route.

During pre-battle deployment, the existing hover selection frame should resize around every cell covered by the dragged unit's footprint. The dragged sprite should stay centered on that footprint and only move to the next anchor after the pointer crosses the half-cell threshold around the current footprint center. Drop validation uses that same covered-cell set.

Attacks and area effects should read the footprint instead of pretending the unit only occupies its anchor:

- Basic attack range uses shortest square-grid distance between attacker footprint and target footprint.
- Actor-target attacks still lock onto an actor.
- A valid basic-attack position is any legal attacker anchor whose full footprint is within range of the target footprint and does not overlap it.
- Larger targets naturally expose more valid attack positions. Multiple smaller units may surround and attack a larger unit when terrain, placement legality, occupancy, and reservations allow it.
- Area effects hit when at least one covered cell overlaps the target footprint.

This makes large units readable as multi-cell bodies without forcing the runtime into freeform physics movement. Navigation remains anchored, but every anchor represents a full footprint state.

Visual size supports this readability, but it is not the occupancy rule. Larger units should scale their sprite uniformly from a tuned footprint size signal instead of stretching differently on X and Y.

## Spatial Tactical Mechanics Direction

Combat is a low-frequency spatial realtime tactical battle. The player should make commander-level choices about local fronts, terrain, reserves, and skill timing. The player should not control individual soldiers or win through high-frequency input.

Maps are the first combat asset. The same unit roster should play differently on bridges, mountain passes, forests, city gates, alleys, high ground, and multi-entrance maps. If terrain does not change tactical value, the combat design is failing its core direction.

The first validation scene should be one strategy scenario before expanding content breadth. A bridge assault or bridge defense is the preferred slice because it can test chokepoint value, delay, reserve timing, ranged pressure, enemy telegraphing, retreat, and spatial skills in one readable setup.

## Corps Tactical Roles

Corps should stay low-complexity and readable through battlefield responsibility:

- spear: chokepoint and anti-charge pressure;
- shield: holding, protection, and frontline stability;
- archer: ranged pressure and area denial;
- cavalry: flank pressure, pursuit, and interruption;
- mage: battlefield area changes and timing pressure.

Individual corps types may gain special rules later, but a corps should not need several active skills to communicate its role.

## Skill And Time Rules

Hero and corps skills should change space, time, or battle state instead of acting as plain damage buttons.

Useful skill categories:

- terrain control such as ice wall, earth wall, fire field, and temporary blockers;
- area suppression such as arrow rain, artillery, poison, and denial zones;
- formation disruption such as charge, knockback, shock, and break-line effects;
- tempo control such as slow, freeze, root, and delayed cast pressure;
- frontline stabilization such as shields, war drums, guard stance, and recovery windows.

Time is a tactical resource. Delaying should matter when it enables reinforcements, cooldown recovery, setup completion, flanking arrival, or a breakthrough elsewhere.

### High-Tier Thunder Mark Skill Family

The first flagship high-tier demo skill family is a lightning-mark coordinate-control kit. It should prove that hero skills can become strong without reducing battle to a single damage button.

The player-facing kit is:

| Skill | Role |
|---|---|
| Thunder Tag Throw | A projectile that deals damage while creating either a ground mark or an attached unit mark. |
| Thunder Mark Fold | Two-stage teleport: select one live mark, then select an empty legal landing anchor within the mark's landing radius. |
| Thunder Spiral Break | A channeled forward pressure field: the player chooses one of the four cardinal directions around the hero, previews a 3x3 area in that direction, then clicks again to submit. Teleporting during the channel may move the continuing damage window with the hero's selected forward offset but must not refresh its duration. |
| Thunder Mark Transfer | Later high-tier spatial transfer that can redirect a unit or skill event through marks. This is accepted as a future flagship rule, not a first implementation requirement. |

Marks are battlefield coordinates, not UI-only decorations. A mark has an owner, a source skill, a location or attached actor, a finite lifetime, and a limited set of skills that can consume or reference it. Ground marks support space planning; attached marks support pursuit, cut-in, or pressure on a moving unit.

Thunder Mark Fold requires explicit mark selection before destination selection. The player first clicks a live owned ground mark or an enemy actor carrying an attached mark. That click only chooses the fold reference; it does not submit the skill. The UI then renders candidate landing anchors around the selected mark. The first implementation uses a square-grid landing radius of 3 around the selected mark anchor and accepts only empty, topology-legal anchors for the caster footprint. The Runtime command is submitted only after the player clicks a legal landing anchor.

The kit must remain hero-led light RTS:

- mark throwing may be an offhand action that does not rewrite the current corps command;
- teleport placement must be selected at the hero/corps command granularity, not through individual soldier micro;
- channeled melee pressure is a hero high-impact window, while the corps still owns the main battle line;
- spatial transfer must use Runtime facts for units, skill casts, and impact areas, so reports can explain what was moved and why.

First implementation should stop at the first three skills unless confirmed discussion accepts the event-transfer system work and updates authority.

## Active Skill Command Timing

Active skills are player-cast tactical commands, not hidden passive stat changes. The default command vocabulary distinguishes:

- Targeted skill: the player selects a valid battle actor as the skill target.
- Non-targeted skill: the skill resolves from the caster, a cell, a direction, or another definition-owned target mode without a selected unit target.

Targeted skill range is checked when the command is accepted and the target is locked. Default active-skill range uses a footprint-aware Manhattan diamond on the square grid, separate from basic-attack slot rules. If that target later moves out of range before the skill action starts, the skill still resolves against the locked target. If the target dies or becomes invalid before execution, the skill fails and does not release.

Active skills can interrupt a basic attack before the attack's damage impact. After basic attack damage has already resolved, the remaining attack recovery cannot be canceled by default. Canceling recovery is a special mechanic that must come from an explicit hero, skill, equipment, relic, or other accepted trait.

A unit that is already casting or recovering from an active skill cannot start another active skill by default. Skill-to-skill interruption, fire-and-forget or offhand release, instant release, and recovery canceling are advanced mechanics that must be explicitly granted by definitions.

During tactical pause, the player may select targets and submit skill commands. Pause-time input changes command intent only; it does not advance battle state, damage, cooldown, cast time, or AI perception until Runtime resumes and reaches a valid release or interrupt boundary.

Tactical pause may let the player switch between multiple battle groups, but each skill submission is one selected hero's current skill intent. Runtime owns whether that intent replaces or waits: if the selected caster is not currently casting or recovering from an active skill, the newest accepted skill intent supersedes that caster's older unstarted pending skill intent; if the caster is already casting or recovering from an active skill, the accepted intent waits behind that active skill. Closing tactical pause hides hero and skill command controls while the battle continues.

## AI Telegraphing And Reports

Enemy AI should be stable and readable before it is clever. Strong enemy behavior must telegraph intent, such as longbow preparation, cavalry charge setup, or mage channeling.

The battle report should explain meaningful outcomes through terrain, timing, reserves, command, skill use, and local collapse. Reports should avoid source-less randomness when the real cause was a chokepoint failure, late retreat, unprotected ranged line, interrupted cast, or bad reserve timing.

## Semantic Marker Dependency

Spatial combat requires authored map semantics. Battle maps and strategic-location interiors should be able to define visible rectangular grid markers from an editor-placed anchor, extending right and down by `m*n` cells.

Marker types may include deployment zone, optional objective zone, chokepoint, lane, reserve point, flank route, ranged point, defend point, entrance, event spawn, and strategic construction region. The marker data model and Godot authoring workflow belong to the focused semantic-marker authority and its confirmed execution work. Destination beacons are runtime command objects and are not authored semantic markers.

## Non-Goals

- Individual soldier micro-control.
- Large-scale RTS box selection.
- AP/turn-based command flow as the future combat identity.
