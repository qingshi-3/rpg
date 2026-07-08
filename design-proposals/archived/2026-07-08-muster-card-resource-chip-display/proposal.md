# Muster Card Resource Chip Display

Status: Archived

## Requirement Id

SM-HERO-CORPS-001-UI-A

## Parent Proposal

`design-proposals/archived/2026-07-08-hero-corps-reassignment-workbench/`

## Supersedes

None.

## Superseded By

None.

## Amends

`design-proposals/archived/2026-07-08-hero-corps-reassignment-workbench/`

## Amended By

None.

## Affected Authority Documents

- `gameplay-design/content-systems-long-term-design.md`
- `system-design/presentation-ui-layout-architecture.md`
- `system-design/strategic-management-system-architecture.md`

## Related Implementation Proposals

- `gameplay-alignment/implementation-proposals/2026-07-08-hero-corps-reassignment-workbench.md`

## Current

The hero-corps reassignment workbench requires troop option cards to print consume, refund, and net cost rows directly on each candidate card. This exposes the Strategic Management replacement settlement process as card text and makes each option read like an accounting breakdown.

## Expected

Troop option cards should display only the resources required to choose that corps as compact reserve-soldier and resource indicators. Buying a corps consumes the shown requirements; replacing an existing corps still refunds the old corps through the Strategic Management command, but the card should not print separate consume, refund, or net-change rows. Disabled states may still explain insufficient resources or unavailable templates.

Strategic Management may continue calculating and reporting consume, refund, and net facts for validation, diagnostics, command results, tests, and future confirmation surfaces. Those facts are not the normal recruitment-card presentation.

## Acceptance

- Authority documents describe recruitment cards as resource-requirement displays, not settlement breakdowns.
- Presentation remains display-only and does not calculate or apply refund settlement.
- Existing replacement command semantics remain unchanged.
