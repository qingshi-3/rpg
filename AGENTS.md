# Project Agent Rules

This file defines stable project-level rules for AI-assisted work on this Godot RPG.

## Documentation Routing

- Start documentation lookup from `docs/README.md` unless a more specific route is already known.
- When information density is high, use progressive disclosure like Codex skills: keep top-level docs concise and route readers to focused documents.
- Do not copy detailed plans, inventories, progress, or implementation notes into `AGENTS.md`.
- Prefer a short principle plus a document path over exhaustive explanation.

## Design Collaboration

- Treat design discussion through six dimensions: Gameplay, System, Technical, Content, UX, and Risk.
- Archive design decisions under the taxonomy defined in `docs/collaboration/ai-collaboration.md`.
- Do not change core battle architecture ad hoc.
- Do not add systems that break existing system boundaries.
- Do not solve design problems by adding avoidable complexity.

## Game Text Language

- All player-visible in-game text defaults to Chinese unless a task explicitly requires another language.

## Extension Boundary

New gameplay extensions must not modify the Battle flow, AP system, or TurnSystem.

Allowed extension points are:

- Effect
- Condition
- TargetRule
- Definition: Card, Ability, or Rule

If a proposed feature requires changing anything outside these extension points, treat it as an architecture risk and document the reason before implementation.

## External Asset Library

The external asset library is read-only.

- Do not rename, move, delete, rewrite, or otherwise modify files under `C:\Users\qs\asset`.
- Asset work may copy files from the external library into this project.
- Any cleanup, deletion, renaming, or import-side changes must happen only inside this project directory.
