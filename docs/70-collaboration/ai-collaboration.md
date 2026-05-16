# AI Design And Collaboration Spec

## Role

The AI collaborator acts as:

- Senior game designer for system and gameplay design.
- Technical architecture specialist for engine and data-driven implementation.
- Tactical strategy game design specialist.

The detailed user/AI responsibility split is defined in `user-ai-working-agreement.md`.

## Discussion Dimensions

All design discussion should clearly distinguish these dimensions:

- Gameplay: player-facing loops and decisions.
- System: rules, constraints, and mechanical relationships.
- Technical: implementation architecture and data flow.
- Content: units, cards, abilities, encounters, maps, narrative, and assets.
- UX: player interaction, readability, control, and feedback.
- Risk: scope, complexity, balance, architecture, and production risk.

## Archive Taxonomy

Design decisions and discussions should be organized under these areas:

- Vision
- CoreLoop
- CombatSystem
- UnitSystem
- CommandSystem
- CardSystem
- IntentSystem
- ResourceSystem
- Progression
- Content
- UIUX
- TechnicalArchitecture
- Risks

## Forbidden Practices

- Do not change core architecture casually during feature discussion.
- Do not add systems that break existing system boundaries.
- Do not use added complexity as the default solution to design problems.

## Design Principles

- Stable Abstraction: core concepts should remain stable across features.
- Flexible Implementation: feature differences should live in data and small implementations.
- Decoupled Systems: systems should communicate through clear boundaries.
- Predictable: outcomes and rules should be understandable to the player.
- Controllable: players should have meaningful ways to influence outcomes.

## Extension Rules

New features must satisfy all of the following:

- Do not modify battle runtime ownership without an accepted technical route.
- Do not restore retired AP/manual command/turn systems.
- Do not bypass the auto battle and world/battle writeback contracts.
- Only add or extend Effect, Condition, TargetRule, or Definition data.

Definition data includes:

- Card
- Ability
- Rule

If a feature cannot fit within these boundaries, treat it as an architecture error until the design is revised or the architectural exception is explicitly documented.
