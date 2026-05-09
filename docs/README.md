# Documentation Index

Use this file as the first stop for project documentation.

Read progressively: start with the smallest relevant entry, then open deeper documents only when the task needs them.

## Primary Routes

- Product direction: `design/core/vision.md`
- Core game direction: `design/core/core-game-direction.md`
- Core loop: `design/core/core-loop.md`
- Summoned character relationship model: `design/character/summoned-social-relationship-model.md`
- Unit talent, rank, and control authority: `design/character/unit-talent-rank-control.md`
- Sanguo Qunying reference architecture: `design/architecture/sanguo-qunying-reference-architecture.md`
- Global system decomposition: `design/architecture/global-system-decomposition.md`
- Project architecture: `design/architecture/project-architecture.md`
- Content authoring architecture: `design/architecture/content-authoring-architecture.md`
- Character definition: `design/character/character-definition.md`
- Emotion and relationship system: `design/character/emotion-system.md`
- Emotion gameplay query contracts: `design/character/emotion-gameplay-query-contracts.md`
- Strategic world V1: `design/world/strategic-world-v1.md`
- Strategic world initial state authoring: `design/world/strategic-world-initial-state-authoring.md`
- Strategic world RTS navigation and armies: `design/world/strategic-world-rts-navigation-and-armies.md`
- WorldSite state deployment authority: `design/world/world-site-state-deployment.md`
- Strategic world wilderness opportunities: `design/world/strategic-world-v1-opportunities.md`
- World battle progression change: `technical-changes/2026-05-07-world-battle-progression.md`
- WorldSite state driven deployment: `technical-changes/2026-05-08-world-site-state-deployment.md`
- World scene structure cleanup: `technical-changes/2026-05-04-scene-structure-cleanup.md`
- Mechanism battle slice: `design/battle/mechanism-battle-slice.md`
- Battle architecture: `design/battle/technical-architecture.md`
- Battle runtime responsibility review: `design/battle/battle-runtime-responsibility-review.md`
- Battle input and command architecture: `design/battle/battle-input-command-architecture.md`
- Enemy Intent system: `design/battle/intent-system.md`
- Enemy Intent gameplay design: `design/battle/enemy-intent-design.md`
- Battle UI interaction review: `design/battle/battle-ui-interaction-review.md`
- Battle unit authoring: `design/battle/unit-authoring.md`
- Unit animation system: `design/battle/unit-animation-system.md`
- Tutorial battle content: `content/tutorial/tutorial-battle.md`
- Collaboration rules: `collaboration/ai-collaboration.md`
- User/AI working agreement: `collaboration/user-ai-working-agreement.md`
- Multi-agent workflow: `collaboration/multi-agent-workflow.md`
- Codex Godot UI guidance: `collaboration/codex-godot-ui-guidance.md`
- Game-studio quality gates: `collaboration/game-studio-quality-gates.md`
- Godot C# review checklist: `collaboration/godot-csharp-review-checklist.md`
- Current roadmap: `roadmap/development-priority.md`
- Current design status: `roadmap/current-design-progress.md`
- Test case index: `testcases/README.md`
- Smoke check template: `testcases/smoke-check-template.md`
- Technical change gate: `technical-changes/README.md`

## Reading Order For Common Tasks

- New architecture work: `design/architecture/global-system-decomposition.md` -> `design/architecture/project-architecture.md` -> relevant domain document -> `technical-changes/README.md` if the change crosses boundaries.
- Battle work: `design/battle/mechanism-battle-slice.md` -> `design/battle/technical-architecture.md` -> relevant battle document -> `testcases/phase1-core-prototype.md`.
- World work: `design/world/strategic-world-v1.md` -> focused V1 child document -> `technical-changes/2026-05-02-strategic-world-v1.md` -> `testcases/strategic-world-v1.md`.
- Tutorial content work: `content/tutorial/tutorial-battle.md` -> `content/tutorial/tutorial-battle-spec.md`.
- Design discussion: `collaboration/ai-collaboration.md` -> relevant design document.
- UI creation or refactor with Codex: `collaboration/codex-godot-ui-guidance.md` -> relevant design/UI document -> target scene/script.
- Implementation review: `collaboration/game-studio-quality-gates.md` -> `collaboration/godot-csharp-review-checklist.md` -> relevant testcase document.

## Documentation Rule

If a document starts carrying dense details, split it into an overview plus focused subdocuments. Prefer routing over copying large explanations into entry files.
