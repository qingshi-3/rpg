# Character Definition

Character definitions provide shared identity data for world, emotion, recruitment, settlement, and battle handoff.

This is only the definition layer. It does not implement character progression, equipment, AI, or battle behavior.

## Core Data

```text
CharacterRaceDefinition
  Id
  DisplayName
  Description
  RaceTags
  BaselineAttributes
  DefaultEmotionModifierIds

CharacterDefinition
  Id
  DisplayName
  OriginProfileId
  Race
  CultureId
  FactionId
  ProfessionId
  AttributeModifiers
  EmotionModifierIds
  SocialProfileIds
  IsSpecial
```

## Dependency Direction

```text
Character / Unit definitions
-> Emotion
-> Story / World / Settlement queries
```

Race is owned by Character / Unit definitions. Emotion profiles reference `CharacterRaceDefinition.Id`; they do not create independent race concepts.

`OriginProfileId` is display and content grouping context only. Runtime gameplay should not branch on origin such as 三国、水浒 or 幕末. If origin needs gameplay meaning, express it through abstract social traits, relationship edges, bond definitions, faction/culture modifiers, rank, talent, or duties.

## Emotion Inputs

Character definitions are the owner of race and identity context. Emotion reads them as input only.

Emotion generation uses:

- `CharacterDefinition.Race.Id` to find `RaceEmotionProfileDefinition.RaceId`.
- `CharacterRaceDefinition.BaselineAttributes` plus `CharacterDefinition.AttributeModifiers` to derive small trait adjustments.
- `CharacterRaceDefinition.DefaultEmotionModifierIds` as race-level default modifiers.
- `CharacterDefinition.CultureId`, `FactionId`, and `ProfessionId` as optional modifier lookup keys.
- `CharacterDefinition.EmotionModifierIds` as authored personal or story modifiers.
- `CharacterDefinition.IsSpecial` to skip ordinary individual variance when a hand-authored character should stay stable.

Modifier ids are resolved against `EmotionProfileModifierDefinition`. A culture, faction, or profession modifier can use either its raw id or a prefixed id such as `culture:village`, `faction:player_allies`, or `profession:healer`.

## Current Scope

The first pass only defines:

- Character attributes.
- Race tags.
- Race definitions.
- Character definitions.

Out of scope for this pass:

- Character progression.
- Equipment.
- Recruitment rules.
- Battle unit spawning.
- Save/load.

Detailed summoned-character relationship layering lives in `summoned-social-relationship-model.md`.
Talent, rank, direct-control slots, and promotion boundaries live in `unit-talent-rank-control.md`.
