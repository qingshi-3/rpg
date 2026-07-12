# Project Agent Rules

This file contains only stable project-level rules and routing for AI-assisted work on this Godot RPG. Detailed design, architecture, roadmap, workstream, and implementation information belongs in focused documents.

## Authority And Session Bootstrap

Follow `gameplay-alignment/authority-map.md` when sources conflict:

- `gameplay-design/`: accepted player-facing gameplay and content-system rules.
- `system-design/`: accepted implementation architecture, ownership, data flow, and contracts.
- `work-items/active/`: confirmed task contracts, progress, resume state, and verification handoff; never gameplay or architecture authority.
- `gameplay-alignment/`: gap tracking, durable workstream and acceptance records, and repair work; never gameplay or architecture authority.
- `history/`: the only route to legacy proposal and implementation records; never active authority or a default bootstrap input.

At the start of a repository session:

1. Read this file and `gameplay-alignment/authority-map.md`.
2. Read `gameplay-design/README.md` and the gameplay authority relevant to the task.
3. Read `system-design/README.md` only when implementation architecture is involved.
4. Before creating, executing, resuming, or verifying any state-changing task, read `work-items/README.md` and the exact task under `work-items/active/` when it exists.
5. Read related current gap, workstream, or acceptance records only when the task continues tracked work.
6. Do not enter `history/` unless the user explicitly requests history, resumes the exact subject, or a historical investigation requires a named record.

## Discussion And Execution Gate

The global two-stage workflow applies to every state-changing task: discuss first, then execute only after the user confirms the direction. Discussion may inspect files, logs, runtime behavior, and external references but must not modify code, resources, persistent documents, or external state.

After confirmation, create or update one self-contained task document under `work-items/active/` before execution. It must carry the confirmed result, authority impact, scope, non-goals, constraints, acceptance, current progress, resume state, and verification handoff. When the conclusion changes product or gameplay rules, system architecture, persistent state, runtime ownership, cross-system contracts, scene/resource taxonomy, or future-agent behavior, update the relevant `gameplay-design/` or `system-design/` authority document at the start of execution before changing implementation.

- Do not create design or implementation proposals as execution gates. Use the confirmed active task plus the appropriate authority, module, workstream, test-case, or acceptance document for durable memory.
- Execution Agents maintain the task's progress and evidence without changing confirmed direction. Direction or authority conflicts set the same task to `Needs Discussion` and stop state-changing work.
- When scoped execution is complete, an execution Agent must hand off at `Awaiting Verification` and must not set `Completed`. A different Agent or independent context verifies against the task acceptance criteria before setting `Completed` and archiving; failed verification returns the task to `In Progress` for scoped defects or `Needs Discussion` for direction or authority issues.
- If accepted documents, the confirmed active task, code, resources, or older documents disagree, set the task to `Needs Discussion`, stop execution, and return to discussion. Do not use historical records to reinterpret accepted design.

## Documentation And Comments

- Treat project documents as long-term AI working memory. Record intent, ownership, boundaries, contracts, and acceptance criteria; keep temporary notes, inventories, progress logs, and ordinary todos out of authority documents.
- Prefer correcting or deleting stale documents. Archive only material with lasting historical value, and keep this file limited to stable rules and routes.
- Before edits, classify impact using the user-level Small/Medium/Large rules and update authority, focused documents, routes, and nearby comments at the corresponding scope.
- Non-trivial code changes must leave concise nearby comments that preserve intent, state transitions, failure semantics, tuning rationale, or authority ownership without restating mechanics. Treat stale comments as bugs and update them with related behavior, constants, exported tunables, or authority boundaries.

## Current Gameplay Direction And Terminology

The accepted direction is hero-led light RTS with strategic-city and content-system management. `gameplay-design/content-systems-long-term-design.md` is the player-facing content-system authority.

Do not revive manual tactical chess, pure post-deployment autobattler playback, or AP/TurnSystem growth as the future battle identity unless a confirmed discussion updates the accepted authority.

- Use **strategic location** as the umbrella design term.
- Use **city / stronghold** for core managed locations; use **resource site**, **gate/pass**, **ruin**, **dungeon**, and **opportunity** for lighter or specialized locations.
- `WorldSite` may remain a technical abstraction in code and legacy documents; do not force concrete design language back to “场域”.
- Preserve Godot scene terminology such as `.tscn`, `SiteScenePath`, `ReturnScenePath`, and `ChangeSceneToFile`.

## Implementation Authority

- Keep one authoritative implementation for each runtime responsibility.
- Do not hide broken core logic behind layered fallbacks. Fail explicitly, log the reason, and fix the authoritative path.
- Use the accepted long-term architecture and persistent-state model. Do not add throwaway models, one-off bypasses, hidden compatibility branches, or temporary core gameplay, runtime, persistence, settlement, or content logic.
- If the required architecture is missing or unclear, return to discussion and update design authority before coding.
- Migration adapters are allowed only as explicit boundaries from a legacy owner into the accepted architecture; they must not own new business facts, gameplay rules, or runtime authority.

## Godot Practice And References

- Prefer authored `.tscn`, `.tres`, `.gdshader`, `Theme`, `StyleBoxTexture`, and reusable `PackedScene` resources. Do not construct UI, themes, shaders, or scene structures with `new` unless no practical resource-based alternative exists.
- Runtime code should load resources, bind nodes, and refresh state. Repeated dynamic UI should instantiate authored templates instead of constructing complete control trees in business logic.
- For Godot API, editor, scene/resource, and C# integration questions, use official Godot 4.7 documentation. Prefer `.codex/external/godot-docs-4.7/` when available; otherwise use the official 4.7 online documentation, never a mismatched older-version clone.
- GodotPrompter skills are implementation aids, never project authority. After the discussion gate passes and relevant authority is current, load the matching installed skill for Godot code, scenes, UI, resources, debugging, testing, or review.
- Route installed skills by subsystem: C# and signals (`csharp-godot`, `csharp-signals`); structure (`scene-organization`, `resource-pattern`, `component-system`, `state-machine`, `event-bus`); UI (`godot-ui`, `responsive-ui`, `hud-system`); runtime support (`input-handling`, `camera-system`, `ai-navigation`, `ability-system`); persistence, assets, diagnostics, tests, and review (`save-load`, `assets-pipeline`, `godot-debugging`, `godot-testing`, `godot-code-review`).
- Active work items, durable work records, or final verification summaries must name the GodotPrompter skills used, or explicitly state that no installed skill applies.

## Content And Runtime Conventions

- Author story, campaign, dialogue, reward, relationship, encounter, unit, and city content through definitions or configuration. Avoid hardcoded one-off content unless a confirmed discussion and the relevant authority explicitly accept that cost.
- Add low-noise runtime diagnostics for important state transitions, failures, and user-facing actions; never log per-frame or other high-frequency noise. For runtime or presentation debugging, check the newest `C:\Users\qs\AppData\Roaming\Godot\app_userdata\rpg\logs\rpg-YYYYMMDD.log` first, then `godot.log` for engine output.
- Player-visible in-game text defaults to Chinese unless the task explicitly requires another language.

## Repository Operations

- If direct GitHub HTTPS access times out and `127.0.0.1:7890` is reachable, use a command-scoped proxy such as `git -c http.proxy=http://127.0.0.1:7890 -c https.proxy=http://127.0.0.1:7890 push origin HEAD:main`. Do not set global Git proxy configuration unless the user asks.
- Treat `C:\Users\qs\asset` as read-only: never rename, move, delete, rewrite, or otherwise modify files there. Assets may be copied into this project; cleanup, renaming, deletion, and import-side changes must occur only inside the project directory.
