# Lightweight Reserve Recovery

Status: Archived

## Relationship Metadata

- Requirement Id: `SM-RESERVE-RECOVERY-001`
- Parent Proposal: `design-proposals/archived/2026-06-20-strategic-operation-foundation/`
- Supersedes: None
- Superseded By: None
- Amends: `design-proposals/archived/2026-06-20-strategic-operation-foundation/`
- Amended By: None
- Affected Authority Documents:
  - `gameplay-design/content-systems-long-term-design.md`
  - `gameplay-design/details/cities-and-locations/README.md`
  - `system-design/strategic-management-system-architecture.md`
  - `system-design/presentation-ui-layout-architecture.md`
- Related Implementation Proposals:
  - `gameplay-alignment/implementation-proposals/2026-07-10-lightweight-reserve-recovery.md`

## Current Design

Accepted gameplay describes reserve soldiers as a city-local aggregate that recovers over world-map time up to the remaining city force capacity. It explicitly excludes a first-version conscription-policy system.

The current implementation diverges from that design. It has no passive reserve recovery. Instead, it adds a manual resource-spending command and persistent automatic conscription intensities that consume Money and Food during elapsed-time settlement. Presentation exposes those mechanics through a dedicated conscription tab and policy panel.

This creates more player-facing policy, persistence, UI, balancing, and maintenance work than the first strategic slice needs.

## Expected Design

The first version uses one lightweight reserve-recovery rule:

```text
recovered reserve soldiers
= min(2 * elapsed world-map pulses, remaining city force capacity)
```

The rule applies to each player-controlled city when Strategic Management accepts elapsed world-map time. Recovery is free, requires no building, and is aggregated when multiple pulses settle together. Strategic Management emits one low-noise recovery fact per affected city rather than one event per pulse.

The recovery rate is an economy definition with a first-version value of `2` reserve soldiers per elapsed pulse. Runtime code consumes that definition and must not duplicate the value as a hidden constant.

The first version has no manual conscription command, automatic conscription intensity, conscription-policy persistence, resource cost, training-ground requirement, or dedicated conscription panel. City overview or military-management presentation may show current reserve soldiers, total force capacity, and the passive recovery rate as read-only facts.

## Non-Goals

- population or civilian demographic simulation;
- public order, morale, or recruitment-pool simulation;
- training queues or player-selected recruitment intensity;
- building, hero, technology, or city-role modifiers to reserve recovery;
- changing corps creation or replenishment costs;
- changing world-map time ownership or settlement cadence.

## Acceptance

- Authority documents define passive recovery at `2` reserve soldiers per elapsed world-map pulse.
- Recovery is capped by `ActiveForces + ReserveForces <= CityForceCapacity`.
- Recovery has no first-version resource, building, or policy requirement.
- Authority documents do not expose manual conscription, automatic intensity, or a dedicated conscription UI as accepted first-version behavior.
- Presentation remains read-only for reserve recovery and does not calculate or apply it.
- Implementation work begins only from a focused implementation proposal after this design is merged and archived.
