# Battle Technical Index

This directory is retained for legacy implementation references while battle architecture is realigned.

Current accepted architecture should be created under `system-design/` through the proposal flow. Until that exists, use the current gameplay authority and `gameplay-alignment/` gap tracking before changing battle runtime ownership.

## Start Here

- Current gameplay authority: `../../../gameplay-design/content-systems-long-term-design.md`
- Current system-design route: `../../../system-design/README.md`
- Battle cleanup and gaps: `../../../gameplay-alignment/gap-register.md`
- World battle contract: `../world/strategic-world-v1-battle-contract.md`
- Historical auto battle migration: `../../50-production/technical-changes/2026-05-16-auto-tactics-migration.md`

## Reused Presentation References

- Battle scene and map runtime: `battle-scene-architecture.md`
- Unit system: `unit-system.md`
- Unit authoring: `unit-authoring.md`
- Unit animation system: `unit-animation-system.md`

## Rule

Do not recreate AP, turn controllers, manual command routers, manual action menus, or player-phase battle HUDs.

Do not treat the auto battle migration route as the future battle product identity. New battle runtime work should either be part of an accepted hero-led light RTS architecture proposal or stay isolated as backend auto-resolve/report infrastructure.
