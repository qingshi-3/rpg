# Audio Asset Migration Backlog

Status: pending. Do not continue bulk migration until the starter slice is reviewed in-game and the mapping rule below is enforced by tooling or checklist.

## Background

Duelyst audio is usable as a resource source, but this project must not map sounds from temporary local unit ids, Chinese display names, or placeholder gameplay labels. Those names can be wrong while the actual animation pixels point to a different Duelyst unit.

The authoritative mapping chain is:

```text
BattleUnitDefinition.Visual
-> BattleUnitVisualDefinition.SpriteFrames
-> frames.tres source png/plist
-> Duelyst RSX animation alias in app/data/resources.js
-> Duelyst card factory setBaseSoundResource block
-> source sfx file
-> Godot unit audio profile
```

## Pending Migration Rules

- Map audio by actual sprite/animation source, not by local unit id or display name.
- Record the source visual key, RSX animation alias, card factory path/line, source sfx file, and target Godot path for every migrated cue.
- Keep unit-specific audio under `assets/battle/units/<unit_id>/audio/` for maintainability.
- Keep common UI/system audio under `assets/audio/sfx/common/`.
- Do not bulk-copy all Duelyst sounds; migrate only sounds referenced by accepted units, UI events, or battle feedback needs.
- Treat first-pass audio as presentation-only. It must not change AP, turn order, hit timing authority, health, death state, or action resolution.

## Starter Slice Finding

The initial four local unit definitions are not four distinct Duelyst units:

| Local Unit Id | Actual Sprite Source | Duelyst Mapping Source |
| --- | --- | --- |
| `player_knight` | `f1_shieldforger.png` | `RSX.f1Surgeforger*` / `app/sdk/cards/factory/wartech/faction1.coffee` |
| `militia` | `f1_shieldforger.png` | `RSX.f1Surgeforger*` / `app/sdk/cards/factory/wartech/faction1.coffee` |
| `skeleton_warrior` | `f1_shieldforger.png` | `RSX.f1Surgeforger*` / `app/sdk/cards/factory/wartech/faction1.coffee` |
| `skeleton_archer` | `f1_scintilla.png` | `RSX.f1Scintilla*` / `app/sdk/cards/factory/bloodstorm/faction1.coffee` |

This means the current local names are not reliable audio identities. Future content should either rename/re-author these units or keep audio tied to the actual sprite source until the unit identity is finalized.

## Proposed Tooling

Before full migration, add a small offline script that:

1. Reads each `assets/battle/units/**/unit.tres` or legacy unit definition.
2. Resolves `Visual -> SpriteFrames -> source png/plist`.
3. Looks up the corresponding RSX animation alias in Duelyst `app/data/resources.js`.
4. Finds the matching card factory block and `setBaseSoundResource` entries.
5. Converts referenced `.m4a` to Godot-friendly `.ogg`.
6. Writes or updates `audio.tres` and a JSON audit report.

## Acceptance Criteria

- A migrated unit's audio report can be traced from Godot unit definition to original Duelyst visual and sound block.
- In-game review confirms the sound is not misleading for the current visible sprite.
- The migration report identifies any local units that share the same source sprite but have different temporary gameplay names.
- Bulk migration is blocked if a source sprite cannot be resolved or if multiple Duelyst card identities appear to share the same visual without a chosen authority.

## Deferred Questions

- Should local unit directories be renamed around stable source visual ids, gameplay ids, or final authored character/unit ids?
- Should temporary units that share `f1_shieldforger.png` remain separate definitions, or should they be consolidated until unique visuals exist?
- Should movement audio be enabled globally, per unit, or disabled until it is less repetitive?
