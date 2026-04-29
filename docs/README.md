# Documentation Index

Use this file as the first stop for project documentation.

Read progressively: start with the smallest relevant entry, then open deeper documents only when the task needs them.

## Primary Routes

- Product direction: `design/product/vision.md`
- Core loop: `design/gameplay/core-loop.md`
- Project architecture: `design/systems/project-architecture.md`
- Battle architecture: `design/systems/technical-architecture.md`
- Battle UI interaction review: `design/systems/battle-ui-interaction-review.md`
- World exploration architecture: `design/world/exploration-world.md`
- Tutorial battle content: `content/tutorial/tutorial-battle.md`
- Collaboration rules: `collaboration/ai-collaboration.md`
- User/AI working agreement: `collaboration/user-ai-working-agreement.md`
- Current roadmap: `roadmap/development-priority.md`
- Current design status: `roadmap/current-design-progress.md`
- Test case index: `testcases/README.md`
- Technical change gate: `technical-changes/README.md`

## Reading Order For Common Tasks

- New architecture work: `design/systems/project-architecture.md` -> relevant system document -> `technical-changes/README.md` if the change crosses boundaries.
- Battle work: `design/systems/technical-architecture.md` -> relevant system document -> `testcases/phase1-core-prototype.md`.
- World work: `design/world/exploration-world.md` -> relevant scene/resource files -> related test cases.
- Tutorial content work: `content/tutorial/tutorial-battle.md` -> `content/tutorial/tutorial-battle-spec.md`.
- Design discussion: `collaboration/ai-collaboration.md` -> relevant design document.

## Documentation Rule

If a document starts carrying dense details, split it into an overview plus focused subdocuments. Prefer routing over copying large explanations into entry files.
