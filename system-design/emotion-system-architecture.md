# Emotion System Architecture

Status: Accepted Architecture

## Gameplay Authority

This document supports the long-term people-and-heroes pillar in `gameplay-design/content-systems-long-term-design.md`, especially relationship-based content and character decision support.

## Responsibility

The emotion system owns character emotional traits, relationships, memories, event reactions, disposition evaluation, recruitment fit, task assignment fit, loyalty risk, battle support willingness, and relationship-gated checks.

## Does Not Own

The emotion system does not own strategic world state, battle runtime state, city resources, garrison mutation, manual persistence UI, or final gameplay action authority. It evaluates social state and produces results for callers to accept or reject.

## Persistent State

`EmotionWorldState` owns actors. Each actor owns:

- stable actor id, display name, race id, and special flag;
- trait values by `EmotionAxis`;
- relationship metrics by target id;
- memories with source event id, description, weight, and tags.

Definitions under `Definitions/Emotion` provide race profiles, modifiers, event definitions, conditions, effects, traits, and relationship metrics.

## Runtime State

`EmotionSystem` keeps an in-memory `EmotionWorldState` plus definition database references. Query result objects, score factors, and condition contexts are runtime-only explanations.

## Inputs

Inputs are character definitions, race profiles, profile modifiers, emotion events, event definitions, condition contexts, and query objects for recruitment, task assignment, loyalty, battle support, relationship gates, and event reaction previews.

## Outputs

Outputs are operation results, actor snapshots, event results, condition results, score factors, disposition results, loyalty risk levels, recruitment decisions, task assignment scores, battle support decisions, and relationship gate decisions.

## Contracts

All public APIs return `EmotionOperationResult<T>` or explicit boolean helpers. Missing actors, missing definitions, or failed conditions return explicit failures instead of mutating unrelated state.

Event application mutates only emotion actors through trait, relationship, and memory deltas. Evaluation APIs are read-only and expose score factors so callers can explain decisions.

World task fit supports assigned work categories that exist in the current strategic-management model. Raid and exploration task kinds are not part of the accepted first-phase task set.

## Failure Rules

Missing actor ids, invalid event definitions, missing required context, and failed relationship/trait/disposition conditions must produce failed operation results or negative condition results. The system must not create world resources, armies, battles, or facilities as side effects.

## Acceptance

This architecture is acceptable when:

- emotion state can be exported, replaced, and queried independently of world scene nodes;
- event application changes only traits, relationships, and memories;
- social decisions expose score factors;
- first-phase task evaluation contains no raid or exploration assignment path.
