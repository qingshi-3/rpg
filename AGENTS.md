# Project Agent Rules

This file stores stable project-level rules for AI-assisted work on this Godot RPG. Keep it short: route to focused documents instead of copying design, architecture, roadmap, or implementation details here.

## Authority Routing

- `gameplay-design/`: accepted player-facing gameplay and content-system rules.
- `system-design/`: accepted implementation architecture, system ownership, data flow, and contracts.
- `design-proposals/`: local proposal copies used before changing accepted design or architecture.
- `gameplay-alignment/`: gap tracking and steady repair work against the new direction.
- `docs/`: existing project documentation and historical implementation material; use it as reference, but do not let it override `gameplay-design/` or `system-design/`.

When documents conflict, follow `gameplay-alignment/authority-map.md`.

## Session Bootstrap

When a session starts in this repository:

1. Read `AGENTS.md`.
2. Read `gameplay-alignment/authority-map.md`.
3. Read `gameplay-design/README.md` and the relevant gameplay authority document for the task.
4. Read `system-design/README.md` only when the task touches implementation architecture.
5. Check `design-proposals/active/` for active proposals related to the task.
6. Treat `docs/` as legacy/reference material after current authority is clear.
7. Do not read archived proposal bodies unless the user explicitly requests them.

## Design Proposal Gate

For changes that affect product/gameplay rules, system architecture, persistent state, runtime ownership, cross-system contracts, resource/scene taxonomy, or future-agent behavior:

1. Read the current authority documents.
2. Present the current design or architecture to the user.
3. Present the expected design or architecture to the user.
4. Wait for user acceptance.
5. Create a `design-proposals/active/<date>-<slug>/` proposal with `current/` and `expected/` copies.
6. Implement against the accepted `expected/` copy.
7. If the expected design changes during implementation, pause and get acceptance again.
8. After implementation acceptance, merge `expected/` into the authority documents and archive the proposal.

Do not directly edit accepted design or architecture documents for proposal-scoped changes.

## Archive Rule

Archived proposals are historical records, not active authority.

- `design-proposals/archived/README.md` may be read for orientation.
- Do not read archived proposal bodies unless the user explicitly requests a specific archived proposal or archive investigation.
- Archived content must not override current `gameplay-design/` or `system-design/`.

## Documentation Governance

- Treat project documents as long-term AI working memory, not user-facing progress narration.
- Do not put temporary notes, inventories, implementation logs, or ordinary todos into authority documents.
- Prefer deleting or correcting stale material over adding layers of supersession notes.
- Keep `AGENTS.md` limited to stable rules and route entries.
- When code, resources, and docs disagree, identify the current authority first, then repair old docs and implementation through `gameplay-alignment/`.
- Before edits, classify impact as Small, Medium, or Large:
  - Small: local code/resource edit; update nearby comments when intent or tunables change.
  - Medium: behavior or cross-file flow change; update focused design or system docs through proposal flow when authority changes.
  - Large: new module/system, persistence model, architecture boundary, or documentation taxonomy change; use proposal flow and update acceptance routes.

## Implementation Comments

Comments are part of the implementation-level memory for future AI readers.

- Non-trivial code changes must leave concise comments near the relevant code explaining why the behavior exists, the design background, or the authority boundary.
- Comments should preserve intent, state transitions, failure semantics, tuning rationale, architecture ownership, or temporary workaround reasons.
- Do not add mechanical comments that only restate what the next line of code does.
- Treat stale comments as bugs. When behavior, constants, exported tunables, or authority boundaries change, update nearby comments in the same change.

## Current Gameplay Direction

The accepted direction is hero-led light RTS with strategic-city and content-system management. Use `gameplay-design/content-systems-long-term-design.md` as the player-facing content-system authority.

Do not revive old manual tactical chess, pure post-deployment autobattler playback, or AP/TurnSystem growth as the future battle identity unless a new accepted proposal changes the authority documents.

## Terminology

- Use **strategic location** as the umbrella design term for large-map locations.
- Use **city / stronghold** for core managed locations.
- Use **resource site**, **gate/pass**, **ruin**, **dungeon**, and **opportunity** for lighter or specialized location types.
- `WorldSite` may remain a technical abstraction in code and legacy docs. Do not force all design language back to "场域" when "city", "ruin", or another concrete term is more accurate.
- Keep Godot scene terminology for engine concepts such as `.tscn`, `SiteScenePath`, `ReturnScenePath`, and `ChangeSceneToFile`.

## Implementation Authority

- Do not keep multiple authoritative implementations for the same runtime responsibility.
- Do not hide broken core logic behind layered fallbacks. Fail explicitly, log the reason, and fix the authoritative path.
- Default to the accepted long-term architecture and persistent state model.
- If a temporary workaround is unavoidable, isolate it, comment the reason, and keep it out of the main implementation path.

## Godot Resource Authoring

- Do not hardcode UI, themes, shaders, or scene structures with `new` unless there is no practical resource-based alternative.
- Prefer authored `.tscn`, `.tres`, `.gdshader`, `Theme`, `StyleBoxTexture`, and reusable packed scenes.
- Runtime code should load resources, bind nodes, and refresh state.
- Dynamic repeated UI should instantiate reusable row/button/item scenes instead of constructing full control trees in gameplay code.

## Content Authoring

Story, campaign, dialogue, reward, relationship, encounter, unit, and city content should be data-driven through definitions/configuration. Avoid hardcoded one-off plot, unit, or location logic unless a proposal explicitly accepts that cost.

## Runtime Diagnostics

Runtime logic changes should add low-noise logs for important state transitions, failures, and user-facing actions. Avoid per-frame or high-frequency logging.

## Game Text Language

All player-visible in-game text defaults to Chinese unless a task explicitly requires another language.

## External Asset Library

The external asset library is read-only.

- Do not rename, move, delete, rewrite, or otherwise modify files under `C:\Users\qs\asset`.
- Asset work may copy files from the external library into this project.
- Cleanup, deletion, renaming, and import-side changes must happen only inside this project directory.
