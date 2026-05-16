# Game Design Index

This directory stores player-facing gameplay design. It explains what the player does, what decisions matter, and how each gameplay pillar creates value.

## Gameplay Pillars

```text
三国群英传式大地图战略
+ 三国志 / 三国立志传式人物社交经营
+ 城池 / 战略地点经营与英雄带兵轻 RTS 战斗
```

The project may use 异世界召唤 as a setting and content expression, but gameplay design should still be written around map strategy, people management, concrete city/strategic-location operation, build development, deployment, command, and reportable battle consequences.

## Routes

- Core gameplay direction: `gameplay-direction.md`
- Core loop: `core-loop.md`
- Strategic map gameplay: `strategic-map/README.md`
- Officer social gameplay: `officer-social/README.md`
- Battle gameplay: `tactical-battle/README.md`
- Current gameplay authority outside legacy docs: `../../gameplay-design/content-systems-long-term-design.md`
- Historical auto battle migration guardrails: `../50-production/technical-changes/2026-05-16-auto-tactics-migration.md`

## Boundaries

- Gameplay documents define player-facing verbs, choices, systems, and feedback.
- Technical contracts, data models, and engine implementation belong in `../30-technical-design/`.
- Authored scenario, tutorial, character, and world content belongs in `../40-content/`.
- Roadmap state and implementation sequencing belong in `../50-production/`.
