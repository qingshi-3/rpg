# World Design Index

This directory stores current world-layer design. The active implementation direction is Strategic World V1.

Strategic World V1 is the foundation for the current product direction: a fantasy world-journey RPG where the big map runs a Sanguo Qunying-style ecology, the player travels as a character / party, and important `WorldSite` entries are persistent operable local maps.

## Current V1 Routes

- V1 overview and boundaries: `strategic-world-v1.md`
- WorldSite / 场域 naming boundary: `world-site-concept.md`
- V1 data model: `strategic-world-v1-data-model.md`
- V1 actions and WorldTick: `strategic-world-v1-actions-and-tick.md`
- V1 battle request/result contract: `strategic-world-v1-battle-contract.md`
- V1 UI and interaction flow: `strategic-world-v1-ui-flow.md`
- V1 implementation plan: `strategic-world-v1-implementation.md`
- V1 open questions: `strategic-world-v1-open-questions.md`
- RTS world navigation and armies: `strategic-world-rts-navigation-and-armies.md`

## Superseded Directions

The old static campaign map, walkable overmap, free exploration location flow, persistent-location sketch, strategic-map sketch, and fixed-continent randomization notes have been consolidated into Strategic World V1. Do not use those older routes as implementation entry points.

## Rule

Keep overview, data model, interaction flow, implementation steps, and test cases separate. Add focused child documents when a world area or feature becomes dense.
