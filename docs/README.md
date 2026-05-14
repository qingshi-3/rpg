# Documentation Index

Start here for project documentation. The docs are organized by responsibility, not by implementation folder.

## Document Layers

- `10-product/`: product positioning, audience, Steam-facing labels, and market-facing pillars.
- `20-game-design/`: player-facing gameplay design: strategic map, officer social play, tactical battle, and core loop.
- `30-technical-design/`: implementation architecture, system contracts, data models, and Godot/C# boundaries.
- `40-content/`: authored content specs: characters, world setup, tutorials, events, and encounter content.
- `50-production/`: roadmap, priorities, change notes, open questions, and production state.
- `60-qa/`: manual test cases, smoke checks, and acceptance coverage.
- `70-collaboration/`: AI collaboration rules, workflow, quality gates, and review checklists.
- `80-research/`: competitor analysis and reference-game notes.
- `90-archive/`: obsolete or superseded material that should not drive current implementation.

## First Reading Path

For product or design discussion, read in this order:

1. `10-product/positioning.md`
2. `20-game-design/gameplay-direction.md`
3. `20-game-design/core-loop.md`
4. The focused gameplay domain under `20-game-design/`
5. The matching technical domain under `30-technical-design/` only when implementation details are needed

## Core Product Rule

The gameplay pillars are:

```text
三国群英传式大地图战略
+ 三国志 / 三国立志传式人物社交经营
+ 回合制战棋战斗
```

异世界召唤、历史人物/传说人物/原创人物同台、裂隙等内容是表达层和内容扩展方式，不是 Steam 品类或玩法核心。 Do not treat summoning flavor as the primary gameplay genre.

## Common Routes

- Product positioning: `10-product/positioning.md`
- Game design overview: `20-game-design/README.md`
- Strategic map gameplay: `20-game-design/strategic-map/README.md`
- Officer social gameplay: `20-game-design/officer-social/README.md`
- Tactical battle gameplay: `20-game-design/tactical-battle/README.md`
- Technical architecture: `30-technical-design/README.md`
- Content specs: `40-content/README.md`
- Current priorities: `50-production/roadmap/development-priority.md`
- Test cases: `60-qa/testcases/README.md`
- Collaboration workflow: `70-collaboration/multi-agent-workflow.md`

## Routing Rule

Keep product, gameplay, expression, technical implementation, production status, and QA in separate document layers. If a document starts mixing layers, split it and leave a short route to the correct owner document.
