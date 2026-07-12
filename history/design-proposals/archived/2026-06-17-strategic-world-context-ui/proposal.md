# Strategic World Context UI Proposal

Status: Archived

## Relationship Metadata

- Requirement Id: UI-CTX-001
- Parent Proposal: None
- Supersedes: None
- Superseded By: None
- Amends: None
- Amended By: None
- Affected Authority Documents:
  - `system-design/presentation-ui-layout-architecture.md`
- Related Implementation Proposals: None; later accepted UI slices already realize this principle.

## Accepted Architecture

Player-facing UI follows a context-first principle: expose only the state, choices, and feedback relevant to the current context, and avoid redundant panels or parallel controls for the same capability. Unrelated details and capabilities recede as the context changes so the active surface stays concise and immersive.

## Scope

This proposal records only that durable principle. It creates no other design claim or implementation workstream.

## Acceptance Criteria

- `system-design/presentation-ui-layout-architecture.md` states the context-first UI principle.
- The authority rejects broad information-board defaults and redundant capability surfaces.
- No additional design or implementation obligation is created by this proposal.
