# UnitSystem

## Hero Structure

Hero is composed from:

- Attributes.
- Ability set.
- Command capability.
- Passives.

## Runtime Battle Entity Composition

Runtime battle objects use composition instead of a deep unit inheritance tree.

`BattleEntity` represents any object that can participate in battle-space interaction. A player unit, enemy, summon, destructible barrel, blocking pillar, or trap can all be a `BattleEntity`.

Capabilities come from components:

- `GridOccupantComponent`: position on the battle grid and blocking flags.
- `FactionComponent`: player, enemy, or neutral ownership.
- `HealthComponent`: HP and death state.
- `DamageReactionComponent`: unit-local reaction to HP damage events; it plays
  damage presentation through `UnitAnimationComponent` and fails loudly if its
  required components are missing.
- `ActionPointComponent`: AP for controllable actors.
- `MovementComponent`: movement range for movement preview and pathfinding queries.
- `SelectableComponent`: whether the entity can be selected by player input.
- `TargetableComponent`: static target categories such as unit, object, obstacle, or destructible.

Ally and enemy relationships should be derived from `FactionComponent` relative to the acting entity, not stored as fixed target tags.

## Hero Role Examples

- Warrior: control and frontline durability.
- Ranger: marking and focus fire.
- Mage: area control.
- Priest: healing and protection.
- Rogue: mobility and disruption.

## Race

Race is a tag system, not a cosmetic skin.

Supported Race tags:

- Human.
- Beast.
- Undead.
- Construct.
- Spirit.

Race is used for:

- Ability checks.
- Status immunity.
- Counter relationships.
- Emotion baseline lookup through `CharacterRaceDefinition.Id`.

Race definition belongs to the Character / Unit definition layer. Emotion can read race ids and race baseline attributes, but Emotion must not define races itself.

## Faction

Faction is used for:

- Enemy composition.
- Behavior style.

## DamageType

Supported DamageType values:

- Physical.
- Fire.
- Shock.
- Holy.
- Shadow.

DamageType uses local counter rules. It should not use cyclic counters.

## Status Categories

- Control: Stun.
- Behavior: Mark, Guard.
- Duration: Burn, Poison.
- Command: Focused, Retreat.
