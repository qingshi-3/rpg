# Project Agent Rules

This file defines stable project-level rules for AI-assisted work on this Godot RPG.

## Documentation Routing

- Start documentation lookup from `docs/README.md` unless a more specific route is already known.
- Treat project documentation as long-term AI working memory. It is written to guide future agents' execution, not to narrate progress for the user.
- When information density is high, use progressive disclosure like Codex skills: keep top-level docs concise and route readers to focused documents.
- Do not copy detailed plans, inventories, progress, or implementation notes into `AGENTS.md`.
- Prefer a short principle plus a document path over exhaustive explanation.
- Keep documentation separated by responsibility, not by implementation convenience:
  - `docs/10-product/`: product positioning, audience, labels, and market-facing pillars.
  - `docs/20-game-design/`: player-facing gameplay design and core loops.
  - `docs/30-technical-design/`: implementation architecture, contracts, and data models.
  - `docs/40-content/`: authored content specs.
  - `docs/50-production/`: roadmap, priorities, technical change notes, and open questions.
  - `docs/60-qa/`: test cases and acceptance checks.
  - `docs/70-collaboration/`: AI workflow, quality gates, and review rules.
- Do not mix product, gameplay, expression, technical, production, and QA layers in one document; split dense documents and leave routed links.

## Documentation Governance

- Before any code, scene, resource, or documentation edit, explicitly classify the documentation/comment impact as Small, Medium, or Large, then update the affected comments or routed docs in the same change set.
- When code, scenes, and docs disagree, govern the docs first: identify the current authoritative contract, delete or archive misleading old material, then update code/comments against that contract.
- Prefer deleting obsolete documents over adding supersession notes. Use `docs/90-archive/` only when historical context still has clear future value.
- Keep top-level documents short and executable. Detailed architecture belongs in `docs/30-technical-design/`, change notes in `docs/50-production/technical-changes/`, and acceptance checks in `docs/60-qa/`.
- Project documents should capture intent, responsibility boundaries, architecture contracts, implementation approach, and acceptance criteria. Do not mirror concrete code implementation details that are better understood from the code itself.
- Before executing a change after discussion, classify its documentation impact:
  - Small: local code/resource edits only; update nearby comments for changed intent or tunables.
  - Medium: behavior or cross-file implementation changes; update the focused design/technical document with the approach and contract, then align code comments.
  - Large: new module, new system, or documentation taxonomy change; update docs routing/directory structure, authoritative documents, code comments, and QA routes together.
- After docs and code are aligned, add comments only where they preserve intent future agents need: ownership boundaries, state transitions, failure semantics, and tuning rationale.
- Tunable values must carry intent near the value or exported property when the reason is not obvious, especially temporary test tuning such as movement speed multipliers and visual emphasis values such as highlight alpha.
- Treat stale comments as bugs. When changing behavior or constants, update the related comment in the same edit.
- Do not document hidden fallbacks as acceptable core behavior. If a fallback is only non-authoritative presentation, say so explicitly; otherwise fail, log, and fix the authoritative path.

## Design Collaboration

- Treat design discussion through six dimensions: Gameplay, System, Technical, Content, UX, and Risk.
- Archive design decisions under the taxonomy defined in `docs/70-collaboration/ai-collaboration.md`.
- Do not change core battle architecture ad hoc.
- Do not add systems that break existing system boundaries.
- Do not solve design problems by adding avoidable complexity.

## Long-Term Implementation Rule

- Default to the target long-term architecture and persistent state model.
- Do not introduce short-term hacks, duplicated authorities, or scene/runtime workarounds that weaken the intended architecture just to make a feature land faster.
- If a temporary workaround is unavoidable, isolate it, document the reason, and keep it out of the main implementation path.

## Multi-Agent Workflow

- For implementation, architecture, scene, UI, resource, and substantial documentation tasks, follow the persistent workflow in `docs/70-collaboration/multi-agent-workflow.md`.
- The main agent acts as workflow orchestrator: it owns state transitions, handoffs, conflict handling, and final user communication, but should not replace specialist agents for requirement decomposition, implementation, review, acceptance, or documentation consolidation.
- Role specifications are stored independently under `docs/70-collaboration/agents/`.
- Simple status checks, direct questions, command outputs, and small non-behavioral edits may bypass the full workflow unless the user explicitly requests it.

## Reference Architecture Guardrail

- When a world, settlement, expedition, battle-entry, campaign-loop, or cross-system feature is underspecified, default to the 三国群英传-style strategic campaign skeleton defined in `docs/20-game-design/strategic-map/sanguo-qunying-reference-architecture.md`.
- The minimum acceptable direction is: persistent `WorldSite` state and actions -> party/expedition/threat movement or decision -> `BattleStartRequest` -> `BattleResult` -> world, character, resource, facility, or threat writeback.
- Use 三国群英传 as a gameplay and architecture reference for strategic-map structure; do not copy its IP, content, assets, old Unity singleton style, hardcoded arrays, or one-off skill scripts.

## Game Text Language

- All player-visible in-game text defaults to Chinese unless a task explicitly requires another language.

## Content Authoring

- Story, campaign, dialogue, reward, relationship, and encounter content should be data-driven: use small generic runtime code plus large authored definitions/configuration, not hardcoded one-off plot logic. See `docs/30-technical-design/content-pipeline/content-authoring-architecture.md`.

## World Terminology

- Persistent operable world locations are `WorldSite` / 场域. Do not call them battle scenes, cities, or generic scenes in design docs or code. See `docs/20-game-design/strategic-map/world-site-concept.md`.
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
- For UI creation or refactor with Codex, follow `docs/70-collaboration/codex-godot-ui-guidance.md` and consult the local GodotPrompter UI skills before changing scene trees, themes, HUDs, or responsive behavior.

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
