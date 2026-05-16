# Gameplay Design Exploration

This directory stores the clean gameplay exploration created after the major gameplay direction reset.

It intentionally lives outside `docs/` so it does not inherit older document routes, migration notes, or implementation-era assumptions.

## Routes

- Long-term content systems design: `content-systems-long-term-design.md`
- Detail design index: `details/README.md`

## Boundary

These files are clean design references. Do not use this directory for implementation progress, temporary task tracking, or historical migration logs.

The long-term design document is the global authority. Detail documents refine specific subsystems and must not silently contradict the global document. If detail work changes a global rule, use the proposal flow in `../design-proposals/`.
