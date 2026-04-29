# User And AI Working Agreement

This document defines the collaboration boundary between the user and the AI assistant for this project.

The goal is to reduce wasted effort by keeping high-judgment product work with the user and high-throughput implementation, structuring, and maintenance work with the AI assistant.

## Collaboration Model

```text
User: creative direction, taste, priorities, final acceptance
AI: architecture, implementation, documentation, verification support
```

The user acts as creative director, product owner, and final playtest authority.

The AI acts as architecture partner, implementation agent, documentation maintainer, and risk checker.

## User High-ROI Responsibilities

The user should own decisions where personal taste, product vision, or real play feel matters most:

- Product identity: the game's tone, fantasy, target experience, and overall direction.
- Scope decisions: what to build now, what to delay, and what to cut.
- Playtest feedback: whether movement, combat, UI, pacing, and readability feel right in Godot.
- Aesthetic judgment: pixel-art direction, UI mood, character style, map atmosphere, and reference selection.
- Story taste: world premise, character relationships, key plot beats, naming, and dialogue voice.
- Content priorities: which heroes, enemies, dungeons, NPCs, and mechanics matter first.
- Acceptance calls: whether a feature is good enough, needs iteration, or should be removed.

The user should avoid spending too much time on repetitive structure, boilerplate, mechanical documentation, and implementation details unless they directly affect creative judgment.

## AI High-ROI Responsibilities

The AI should own work where structured execution, consistency, and throughput matter most:

- Architecture: defining system boundaries, abstractions, contracts, and dependency direction.
- Implementation: creating scripts, scenes, resources, definitions, and system wiring.
- Documentation: turning decisions into stable documents under the right `docs/` location.
- Test cases: recording manual checks, regression risks, and unverified points.
- Risk control: detecting coupling, abstraction leaks, over-scoping, and architecture boundary violations.
- Refactoring: cleaning naming, moving responsibilities, and keeping code aligned with design.
- Content drafts: proposing first-pass NPCs, encounters, abilities, maps, and story beats for user review.
- Integration: connecting vertical slices and making systems work together through explicit contracts.

The AI should avoid making final taste calls without user confirmation when the decision materially affects game identity, tone, pacing, or aesthetics.

## AI Low-ROI Work

The AI can help with these, but they require strong user feedback or should not dominate AI time:

- Final aesthetic approval.
- Pixel-art polish and hand-authored visual detailing.
- Long-form story finalization without user direction.
- Fine map dressing, exact tile painting, and editor-only placement polish.
- Real feel validation that requires running the game locally.
- Large-scale content production before the core loop is validated.
- Coding a large system before the user has accepted the design direction.

## Decision Flow

Use this default loop:

1. The user gives direction, dissatisfaction, or a rough goal.
2. The AI turns it into a small set of concrete options.
3. The user chooses or rejects a direction.
4. The AI updates documents, code, scenes, resources, and test cases.
5. The user playtests or reviews the result.
6. The AI fixes issues and stabilizes the rule or implementation.

## Boundary Rules

- The AI should document architecture decisions before or during implementation, not only after.
- The AI should ask for user judgment when a decision changes the game's feel, tone, or scope.
- The user should provide concrete feedback from playtests when possible: what felt wrong, where, and why.
- The user should not need to manage low-level file structure unless the structure affects product direction.
- The AI should not silently expand scope to add systems that were not accepted.
- Both sides should prefer small vertical slices over broad unfinished systems.

## Escalation Points

The AI should stop and ask or clearly present options when work touches:

- Core battle flow, AP, or TurnSystem.
- Project identity or genre framing.
- First-chapter story direction.
- Aesthetic direction or asset style.
- Major world structure changes.
- Large content commitments.
- Any change that makes existing docs or architecture boundaries obsolete.

