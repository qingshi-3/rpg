# Hero Corps Reassignment Workbench Proposal

Status: Archived

## Relationship Metadata

- Requirement Id: SM-HERO-CORPS-001
- Parent Proposal: None
- Supersedes: None
- Superseded By: None
- Amends: None
- Amended By: None
- Affected Authority Documents:
  - `gameplay-design/content-systems-long-term-design.md`
  - `gameplay-design/details/heroes-and-corps/README.md`
  - `gameplay-design/details/cities-and-locations/README.md`
  - `system-design/strategic-management-system-architecture.md`
  - `system-design/presentation-ui-layout-architecture.md`
- Related Implementation Proposals:
  - Pending: `gameplay-alignment/implementation-proposals/2026-07-08-hero-corps-reassignment-workbench.md`

## Current Design

The accepted strategic-management direction already defines persistent corps instances, city muster templates, aggregate reserve soldiers, hero-corps assignment, and Presentation/UI command boundaries.

The current authority does not define the player-facing replacement flow when a hero already has a main corps and the player selects another corps from the recruitment surface. The implementation currently treats that as hero-directed recruitment: it creates a new corps, binds it to the hero, and parks the previous corps back in the city as a separate garrisoned corps instance.

That behavior creates two problems for the intended city-management workflow:

- the recruitment tab is effectively also the hero main-corps reassignment surface, but the accepted docs do not say so;
- the old separate corps tab can become a hidden inventory-management surface instead of a clear player decision point.

The current UI also relies too much on hover detail for corps option cost. The player needs to see the resource and reserve-soldier consumption directly while choosing a troop option.

## Expected Design

The city recruitment surface is also the first-version hero main-corps reassignment workbench.

The player flow is:

```text
open recruitment workbench
-> select hero
-> inspect available troop options
-> read visible cost/refund/net resource impact below each option
-> choose a troop option
-> Strategic Management replaces the hero's main corps
```

When a hero already has a main corps, replacing it is a single Strategic Management command. The previous corps is settled back into the city with full refund and no extra replacement loss. Refunds are based on the previous corps' current remaining strength: the command returns the recoverable reserve soldiers and resource value represented by that remaining strength. Strength already lost in battle is not restored by reassignment.

The previous corps must not be silently parked as another long-term city corps by this replacement flow. It is dissolved through the full-refund settlement, and the newly mustered corps becomes the hero's main corps. Separate persistent city corps and future corps inventory management remain possible only through explicitly accepted flows with their own UI purpose.

The recruitment workbench should display troop-option economics directly on the option card:

```text
consume: reserve soldiers and resources for the selected corps
refund: previous corps recoverable reserve soldiers and resources, when a hero already has a corps
net: final reserve soldiers and resource delta applied by the command
```

If a selected hero has no assigned corps, the option displays only direct creation cost and net cost. If a replacement produces a resource gain for a cheaper corps, the UI may show that as a return instead of hiding it.

The separate first-version corps tab should be removed when it has no distinct player decision beyond the recruitment workbench. Replenishment, existing-corps reassignment, and corps inventory management require a later accepted design if they become player-facing workflows.

## Non-Goals

- No multiple main corps per hero.
- No individual soldier records.
- No random reassignment loss.
- No hidden replacement penalty.
- No broad corps inventory UI in this requirement.
- No changes to battle Runtime, battle preparation, or settlement rules.

## Acceptance Criteria

- Authority documents define recruitment as the first-version hero main-corps reassignment workbench.
- Authority documents define full-refund, no-loss replacement semantics based on current remaining corps strength.
- Strategic Management remains the only owner of replacement mutation, refund calculation, and validation.
- Presentation displays consume/refund/net resource impact directly under troop options and submits commands only through Strategic Management.
- The first-version separate corps tab is no longer required unless a future accepted design gives it a distinct workflow.

## Merge Record

Merged into the affected authority documents on 2026-07-08 and archived as historical design evidence.
