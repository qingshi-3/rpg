# Battle Gameplay Index

This directory is retained for legacy path stability.

Current player-facing battle authority lives outside `docs/`:

- `../../../gameplay-design/content-systems-long-term-design.md`
- `../../../gameplay-design/details/combat-command/README.md`

The accepted direction is hero-led light RTS on authored battle maps: the player selects hero companies and can issue separate hero, corps, and combined commands. Pure post-deployment auto battle playback is not the current product target.

## Routes

- Current gameplay authority: `../../../gameplay-design/content-systems-long-term-design.md`
- Combat command detail: `../../../gameplay-design/details/combat-command/README.md`
- Migration cleanup tracking: `../../../gameplay-alignment/gap-register.md`
- Historical auto battle migration record: `../../50-production/technical-changes/2026-05-16-auto-tactics-migration.md`

## Rule

Do not recreate AP, player phases, turn controllers, or manual action menus.

Do not route new player-facing battle work through "auto tactics" as the product identity. If a new battle design document is needed, create or update it through the accepted proposal flow and align it with hero-led light RTS.
