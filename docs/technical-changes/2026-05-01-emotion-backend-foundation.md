# Emotion Backend Foundation

Date: 2026-05-01

## Change

Expanded the backend-only foundation for character identity, emotion state, event application, condition checks, and definition-driven emotion events.

New definition layer:

- `src/Definitions/Characters/`
- `src/Definitions/Emotion/`

New pure runtime / application layer:

- `src/Domain/Emotion/`
- `src/Application/Emotion/`

## Architecture

Race ownership belongs to Character definitions:

```text
CharacterRaceDefinition
CharacterDefinition
```

Emotion references race ids through:

```text
RaceEmotionProfileDefinition.RaceId
```

Emotion does not define races itself.

## Contracts

`EmotionSystem` supports:

- Generating actor emotion state from `CharacterDefinition`.
- Generating actor emotion state from an explicit `EmotionNpcGenerationRequest`.
- Generating random NPC individual variation from seed / variation key / emotion inputs rather than actor identity.
- Applying structured `EmotionEvent` input.
- Applying definition-driven `EmotionEventDefinition` input by id or resource.
- Applying event batches.
- Querying actor snapshots.
- Querying trait values.
- Querying relationships.
- Evaluating disposition toward a target.
- Checking trait and relationship thresholds.
- Checking configurable conditions for traits, relationships, disposition, actor existence, and memory tags.
- Checking disposition thresholds and memory tags directly.
- Exporting cloned `EmotionWorldState`.
- Replacing runtime state from an existing `EmotionWorldState` clone.

## Definition Additions

Emotion definitions now include:

- `EmotionEventKind`
- `EmotionEventDefinition`
- `EmotionConditionDefinition`
- `EmotionConditionKind`
- `EmotionComparisonOperator`
- `EmotionEffectDefinition`
- `EmotionEffectKind`

These are still backend-only contracts. They do not implement story, world, recruitment, settlement, battle flow, AP, or turn logic.

## Runtime Additions

Domain state now supports:

- Cloning actor, relationship, memory, and world state.
- Applying multiple memory deltas per event.
- Querying memory tags.
- Keeping event effects as structured trait, relationship, and memory changes.

## Dependency Notes

Character and race definitions remain the source of race identity. Emotion reads race ids, attributes, modifier ids, and profile definitions; it does not define races itself.

`CharacterDefinition.CultureId`, `FactionId`, and `ProfessionId` are treated as optional modifier lookup inputs. Matching `EmotionProfileModifierDefinition` ids may be raw ids or prefixed ids such as `culture:village`, `faction:player_allies`, and `profession:healer`.

## Gameplay Query Contracts

Added gameplay-facing emotion query contracts without implementing those gameplay systems:

- Recruitment emotion gate and chance modifier.
- World task assignment efficiency and loyalty risk delta.
- Loyalty risk score and risk level.
- Battle support chance and morale modifier.
- Relationship gate checks for recruitment, friendship, romance, bond, personal quest, sensitive command, and story choices.
- Event reaction tone and relationship delta preview.

These contracts are exposed through `IEmotionSystem` and only read emotion state. Long-term state changes still go through `EmotionEvent` or `EmotionEventDefinition`.

Added `WorldTaskKind` as a lightweight world definition enum so emotion can evaluate task fit without depending on a future world task implementation.

## Out Of Scope

This change does not implement:

- UI.
- World interaction wiring.
- Recruitment logic.
- Character progression.
- Save/load.
- Battle integration.

Those systems should consume the backend through requests, events, and query results later.

## Random NPC Rule

`ActorId` is a runtime lookup key only. It should not decide personality.

Random NPC differences should come from:

- Race profile.
- Emotion profile modifiers.
- Mounted emotion trait inputs.
- Initial relationship inputs.
- World generation seed.
- Variation key.

This lets the world generator create individualized NPCs after generating a world without requiring authored unit-specific business logic.

## Verification

`dotnet build` passes with 0 warnings and 0 errors.
