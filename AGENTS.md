# Project Agent Rules

This file defines stable project-level rules for AI-assisted work on this Godot RPG.

## Documentation Routing

- Start documentation lookup from `docs/README.md` unless a more specific route is already known.
- When information density is high, use progressive disclosure like Codex skills: keep top-level docs concise and route readers to focused documents.
- Do not copy detailed plans, inventories, progress, or implementation notes into `AGENTS.md`.
- Prefer a short principle plus a document path over exhaustive explanation.
- Keep `docs/design/` organized by stable document role and domain:
  - `core/`: product direction and core player loop.
  - `architecture/`: cross-system architecture, boundaries, and content-authoring rules.
  - `world/`: world-layer and strategic-world specs.
  - `battle/`: battle runtime, combat interaction, intent, cards, units, and battle demos.
  - `character/`: character definition, emotion, and relationship systems.
- Do not recreate broad folders such as `systems/`, `product/`, or `gameplay/` under `docs/design/`; update `docs/design/README.md` if a new long-term design category is genuinely needed.

## Design Collaboration

- Treat design discussion through six dimensions: Gameplay, System, Technical, Content, UX, and Risk.
- Archive design decisions under the taxonomy defined in `docs/collaboration/ai-collaboration.md`.
- Do not change core battle architecture ad hoc.
- Do not add systems that break existing system boundaries.
- Do not solve design problems by adding avoidable complexity.

## Multi-Agent Workflow

- For implementation, architecture, scene, UI, resource, and substantial documentation tasks, follow the persistent workflow in `docs/collaboration/multi-agent-workflow.md`.
- The main agent acts as workflow orchestrator: it owns state transitions, handoffs, conflict handling, and final user communication, but should not replace specialist agents for requirement decomposition, implementation, review, acceptance, or documentation consolidation.
- Role specifications are stored independently under `docs/collaboration/agents/`.
- Simple status checks, direct questions, command outputs, and small non-behavioral edits may bypass the full workflow unless the user explicitly requests it.

## Reference Architecture Guardrail

- When a world, settlement, expedition, battle-entry, campaign-loop, or cross-system feature is underspecified, default to the 三国群英传-style strategic campaign skeleton defined in `docs/design/architecture/sanguo-qunying-reference-architecture.md`.
- The minimum acceptable direction is: persistent `WorldSite` state and actions -> party/expedition/threat movement or decision -> `BattleStartRequest` -> `BattleResult` -> world, character, resource, facility, or threat writeback.
- Use 三国群英传 as a product-architecture reference only; do not copy its IP, content, assets, old Unity singleton style, hardcoded arrays, or one-off skill scripts.

## Game Text Language

- All player-visible in-game text defaults to Chinese unless a task explicitly requires another language.

## Content Authoring

- Story, campaign, dialogue, reward, relationship, and encounter content should be data-driven: use small generic runtime code plus large authored definitions/configuration, not hardcoded one-off plot logic. See `docs/design/architecture/content-authoring-architecture.md`.

## World Terminology

- Persistent operable world locations are `WorldSite` / 场域. Do not call them battle scenes, cities, or generic scenes in design docs or code. See `docs/design/world/world-site-concept.md`.
- Keep Godot scene naming for engine concepts such as `.tscn`, `SceneFilePath`, `SiteScenePath`, `ReturnScenePath`, and `ChangeSceneToFile`.

## Runtime Diagnostics

- Runtime logic changes should add low-noise persistent logs for key state transitions, failures, and user-facing actions; avoid per-frame or high-frequency logging.

## Implementation Authority

- Do not keep multiple authoritative implementations for the same runtime responsibility; choose one clear owner and remove obsolete parallel logic.
- Do not hide broken core logic behind layered fallbacks. Fail explicitly, log the reason, and fix the authoritative path.

## Godot Resource Authoring

- Do not hardcode UI, themes, shaders, or scene structures with `new` unless there is no practical resource-based alternative.
- Prefer authored `.tscn`, `.tres`, `.gdshader`, `Theme`, `StyleBoxTexture`, and reusable packed scenes; runtime code should load resources, bind nodes, and update state.
- For dynamic repeated UI, prefer instancing reusable row/button/item scenes over constructing controls directly in gameplay code.
- For UI creation or refactor with Codex, follow `docs/collaboration/codex-godot-ui-guidance.md` and consult the local GodotPrompter UI skills before changing scene trees, themes, HUDs, or responsive behavior.

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
