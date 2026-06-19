# Strategic World Context UI Proposal

Status: Draft

## Relationship Metadata

- Requirement Id: UI-CTX-001
- Parent Proposal: None
- Supersedes: None
- Superseded By: None
- Amends: None
- Amended By: None
- Affected Authority Documents:
  - `system-design/presentation-ui-layout-architecture.md`
- Related Implementation Proposals:
  - Pending: `gameplay-alignment/implementation-proposals/2026-06-17-strategic-world-context-ui-foundation.md`

## Current Architecture

The accepted Presentation/UI architecture defines layout hosts, UI mode ownership, view-model boundaries, and scene-resource authoring rules. It already says UI must use authored Godot scenes and reusable controls instead of ad hoc runtime control trees.

The current authority does not yet define a strong information-architecture rule for strategic-world UI. The implemented strategic-world panels can therefore drift toward broad information display: selected-location summaries, facilities, garrison, actions, feedback, and operational text can accumulate in one long panel. This makes the UI behave like an information board rather than a contextual game interface.

The current authority also does not yet define a texture-skin rule for scalable UI controls. Existing player-facing HUD scenes still rely heavily on local `StyleBoxFlat` color blocks. The project has UI textures, but they have not been formalized into a reusable skin/resource taxonomy.

## Expected Architecture

Strategic-world UI should become context-first:

```text
current context -> relevant state -> current decisions -> short feedback
```

The UI must not expose every available fact by default. Each UI surface should serve the current context and only present capabilities that the player can understand or act on now.

First-slice strategic-world UI should prioritize:

- a quiet default map state with global time/resources and map affordances;
- selected strategic-location context with identity, current status, and only the most relevant current actions;
- action contexts such as expedition, site entry, construction, or battle entry that temporarily replace unrelated details with target, cost, risk, confirmation, and cancellation;
- compact right-side notices for recent results, warnings, and important state transitions;
- resource-backed UI skin assets for scalable panels and buttons.

The first UI skin source is limited to `assets/textures/ui/basic-ui/1/`. This keeps the first pass visually coherent. Other UI texture packs, including `basic-ui/2`, `basic-ui/3`, and `need-human`, are not part of the main strategic-world skin unless a later focused proposal or implementation slice accepts the style cost.

Scalable player-facing panels, buttons, and slots should use `StyleBoxTexture`, `Theme`, and authored `.tres` or `.tscn` resources. `TextureRect` remains valid for icons, portraits, fixed decoration, and atlas regions, but not for stretching a whole bordered panel or button background.

## Non-Goals

- No gameplay rule changes.
- No strategic-management authority changes.
- No battle Runtime or settlement changes.
- No full inventory, hero sheet, equipment, diplomacy, or city-management redesign in this proposal.
- No broad cross-pack UI asset mixing in the first skin pass.
- No requirement to replace every existing `StyleBoxFlat` in one implementation slice; each slice must define its specific migration target and guard.

## Acceptance Criteria

- `system-design/presentation-ui-layout-architecture.md` states the context-first UI principle.
- The strategic-world migration rules reject broad information-board panels as the default selected-location experience.
- The accepted architecture defines that first-slice strategic-world skinning uses `assets/textures/ui/basic-ui/1/`.
- The accepted architecture distinguishes scalable panel/button skin resources from fixed icons, portraits, and decorative textures.
- Implementation can proceed only through a focused implementation proposal that names the exact UI scenes, reusable skin resources, tests, diagnostics, and manual QA evidence.
