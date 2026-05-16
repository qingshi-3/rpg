# Hero-Led Light RTS System Architecture Proposal

Status: Draft

## Background

The accepted gameplay direction has moved to hero-led light RTS with strategic city and location management. The product design is now broad enough to drive system decomposition, but `system-design/` does not yet contain a dedicated architecture document for this target.

This proposal records the current architecture gap and the expected target architecture before implementation work starts.

## Scope

This proposal covers implementation-facing architecture only:

- horizontal business system boundaries;
- vertical technical layers;
- battle group ownership and contracts;
- command, runtime, settlement, and report boundaries;
- resource and progression flow boundaries;
- ability/effect resourceization boundaries;
- migration principles for old battle and garrison concepts.

## Out Of Scope

This proposal does not implement code, change scenes, set balance values, write final UI layouts, or directly edit accepted authority documents.

It also does not change the accepted gameplay rules in `gameplay-design/content-systems-long-term-design.md`.

## Affected Authority Paths

Current copies:

- `current/system-design/README.md`
- `current/system-design/hero-led-light-rts-system-architecture.md`

Expected copies:

- `expected/system-design/README.md`
- `expected/system-design/hero-led-light-rts-system-architecture.md`

If accepted and later merged, the expected files map to:

- `system-design/README.md`
- `system-design/hero-led-light-rts-system-architecture.md`

## Review Status

This draft incorporates two review passes:

- system-design alignment review: Pass;
- mature game-system design review using MDA, The Door Problem, Machinations, Game Programming Patterns, Game AI Pro, Godot Resource-style data authoring, and GAS-style ability/effect separation: Pass after adding cross-system contract sections.

## Acceptance Request

User review should focus on whether the expected architecture is the right target before any architecture refactor or implementation begins.

Review entry points:

- architecture text: `expected/system-design/hero-led-light-rts-system-architecture.md`
- local visual blueprint: `visuals/architecture-overview.html`
