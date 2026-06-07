# Project Agent Rules

This file stores stable project-level rules for AI-assisted work on this Godot RPG. Keep it short: route to focused documents instead of copying design, architecture, roadmap, or implementation details here.

## Authority Routing

- `gameplay-design/`: accepted player-facing gameplay and content-system rules.
- `system-design/`: accepted implementation architecture, system ownership, data flow, and contracts.
- `design-proposals/`: local proposal copies used before changing accepted design or architecture.
- `gameplay-alignment/`: gap tracking, implementation proposals, acceptance records, and steady repair work against the new direction.

When documents conflict, follow `gameplay-alignment/authority-map.md`.

## Session Bootstrap

When a session starts in this repository:

1. Read `AGENTS.md`.
2. Read `gameplay-alignment/authority-map.md`.
3. Read `gameplay-design/README.md` and the relevant gameplay authority document for the task.
4. Read `system-design/README.md` only when the task touches implementation architecture.
5. Check `design-proposals/active/` for active proposals related to the task.
6. Check `gameplay-alignment/implementation-proposals/` for active implementation proposals related to the task.
7. Do not read archived proposal bodies unless the user explicitly requests them.

## Design-First Gate

Design and documentation are the first implementation step. Do not write or edit code for product/gameplay rules, system architecture, persistent state, runtime ownership, cross-system contracts, scene/resource taxonomy, or future-agent behavior until the accepted authority documents describe the intended rule.

If current code, active proposals, old docs, and accepted authority documents disagree, stop and repair the documentation chain first. Do not use local code or active proposals to reinterpret the design.

## Design Proposal Gate

For changes that affect product/gameplay rules, system architecture, persistent state, runtime ownership, cross-system contracts, resource/scene taxonomy, or future-agent behavior:

1. Read the current authority documents.
2. Present the current design or architecture to the user.
3. Present the expected design or architecture to the user.
4. Wait for user acceptance.
5. Create a `design-proposals/active/<date>-<slug>/` proposal with `current/` and `expected/` copies.
6. Merge the accepted `expected/` copy into the authority documents.
7. Archive the design proposal after the authority documents are updated.

Design proposals change documents, not code. Do not implement directly from an active design proposal. Do not directly edit accepted design or architecture documents for proposal-scoped changes before the proposal is accepted.

Work one requirement at a time. Each design proposal must expose its requirement id, parent/superseded/amended proposal links, affected authority documents, and follow-up implementation proposal links in the default AI-readable entry document. Archived proposals stay immutable except for minimal index metadata that records these relationships.

## Implementation Proposal Gate

After the design proposal is archived and the authority documents are current, create or update a focused implementation proposal under `gameplay-alignment/implementation-proposals/` before code changes begin.

The implementation proposal must reference the accepted authority documents and define scope, non-goals, touched systems, tests, diagnostics, manual QA, and acceptance evidence. Code work must follow that implementation proposal. If implementation reveals a design conflict, stop coding and return to the Design Proposal Gate.

Archive or mark the implementation proposal accepted only after tests, diagnostics, and requested manual QA pass.

Each implementation proposal must implement exactly one accepted requirement or a clearly named slice of one requirement, and its default AI-readable entry document must link the originating design proposal, authority documents, amendment proposals, blocking issues, and verification records.

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
- Comments should preserve intent, state transitions, failure semantics, tuning rationale, or architecture ownership.
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
- Reject temporary coding. Do not add throwaway models, one-off bypasses, hidden compatibility branches, or "just for now" logic for core gameplay, runtime, persistence, settlement, or content flow.
- All code changes must fit the accepted long-term architecture. If the architecture is missing or unclear, stop and repair the design/proposal first instead of inventing a local shortcut.
- Migration adapters are allowed only when they are explicit boundary adapters from a legacy owner into the accepted architecture. They must not own new business facts, gameplay rules, or runtime authority.

## Godot Resource Authoring

- Do not hardcode UI, themes, shaders, or scene structures with `new` unless there is no practical resource-based alternative.
- Prefer authored `.tscn`, `.tres`, `.gdshader`, `Theme`, `StyleBoxTexture`, and reusable packed scenes.
- Runtime code should load resources, bind nodes, and refresh state.
- Dynamic repeated UI should instantiate reusable row/button/item scenes instead of constructing full control trees in gameplay code.

## Godot Documentation Reference

For Godot engine API, editor behavior, scene/resource semantics, and C# integration questions, prefer the local official Godot 4.5 documentation clone at `.codex/external/godot-docs-4.5/` before web search.

## Content Authoring

Story, campaign, dialogue, reward, relationship, encounter, unit, and city content should be data-driven through definitions/configuration. Avoid hardcoded one-off plot, unit, or location logic unless a proposal explicitly accepts that cost.

## Runtime Diagnostics

Runtime logic changes should add low-noise logs for important state transitions, failures, and user-facing actions. Avoid per-frame or high-frequency logging.
Godot runtime logs for this project are under `C:\Users\qs\AppData\Roaming\Godot\app_userdata\rpg\logs\`. When debugging runtime or presentation behavior, check the newest `rpg-YYYYMMDD.log` first, then `godot.log` if engine-level output is needed.

## Game Text Language

All player-visible in-game text defaults to Chinese unless a task explicitly requires another language.

## External Asset Library

The external asset library is read-only.

- Do not rename, move, delete, rewrite, or otherwise modify files under `C:\Users\qs\asset`.
- Asset work may copy files from the external library into this project.
- Cleanup, deletion, renaming, and import-side changes must happen only inside this project directory.
