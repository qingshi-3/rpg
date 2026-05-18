# Battle Audio Slice

This change adds the first battle-audio slice for the starter units only.

## Scope

- Two canonical source-visual unit definitions now reference local `BattleUnitAudioDefinition` resources.
- Unit-owned audio files live under each unit package at `assets/battle/units/<faction>/<package>/audio/`.
- Shared UI/common cues live under `assets/audio/sfx/common/`.
- Source and target conversion records live in `assets/audio/sfx/duelyst_audio_migration_a.json`.

## Runtime Boundary

- Audio is presentation-only and must not mutate AP, turn order, health, position, death state, or battle authority.
- `BattleUnitAudioComponent` is attached to the reusable unit scene and receives its profile from `BattleUnitDefinition.Audio` through `BattleUnitFactory`.
- Battle events currently play cues for move, attack start, attack impact, hit, and defeated presentation.

## Resource Rule

- Keep unit-specific audio next to the unit definition for maintainability.
- Map unit audio from the actual `SpriteFrames` source visual / Duelyst RSX animation identity, not from temporary local unit ids or display names.
- Do not bulk-import all Duelyst audio until the starter slice has been accepted in-game.

## Starter Slice Mapping

- `f1_shieldforger` owns the consolidated `Surgeforger` sound mapping for the removed placeholder units that shared its visuals.
- `f1_scintilla` owns the consolidated `Scintilla` sound mapping for the removed placeholder unit that shared its visuals.
