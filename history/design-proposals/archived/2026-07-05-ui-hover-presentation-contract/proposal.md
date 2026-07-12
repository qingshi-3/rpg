# UI Hover Presentation Contract Proposal

Status: Archived

## Relationship Metadata

- Requirement Id: UI-HOVER-001
- Parent Proposal: None
- Supersedes: None
- Superseded By: None
- Amends: None
- Amended By: None
- Affected Authority Documents:
  - `system-design/presentation-ui-layout-architecture.md`
- Related Implementation Proposals:
  - `gameplay-alignment/implementation-proposals/2026-07-05-ui-hover-presentation-contract.md`
- Related Design Proposals:
  - `design-proposals/active/2026-06-17-strategic-world-context-ui/` (`UI-CTX-001`) defines context-first UI direction, but does not define hover presentation ownership.

## Current Architecture

The accepted Presentation/UI architecture names `OverlayHost` as the owner for hover tooltips, but it does not define the contract for simple text tooltip usage, complex hover detail panels, or map and battle hover overlays.

The implementation currently has several independent hover paths:

- Simple controls set Godot `TooltipText` directly in local presenter or widget code.
- Strategic-world site hover uses a focused `WorldSiteHoverSummaryPanel` and presenter.
- Building construction cards now use a focused authored tooltip scene through `_MakeCustomTooltip`.
- Battle hover frames, unit health-bar hover visibility, debug cell hover panels, and construction footprint previews are owned by map or battle Presentation systems.

This is workable for isolated features, but future UI work can easily add new local tooltip panels, inconsistent positioning, or business-rule text directly into widget code. The architecture needs a narrow contract that keeps hover presentation reusable without forcing map and battle overlays into a generic tooltip manager.

## Expected Architecture

Presentation/UI should classify hover presentation into three paths:

1. `TooltipText` is allowed only for short, low-risk text that does not require custom layout, rich structure, stateful controls, or special positioning.
2. Complex UI hover detail must use an authored tooltip scene, normally instantiated through `GameUiSceneFactory` or an equivalent resource-backed factory path. Business widgets may bind display data into that scene, but must not construct ad hoc tooltip control trees in local code.
3. Map, battle, construction placement, deployment, debug, and other world-space hover overlays remain owned by their subsystem presenters. They must still share the common Presentation styling and must expose clear naming, positioning, and data-source boundaries so they do not become hidden gameplay authority.

All hover presentation is display-only. It may consume definitions, view models, rule results, runtime snapshots, or debug data, but it must not mutate gameplay state, run strategic or battle validation, or become a second owner for command legality.

## Non-Goals

- No immediate migration of every existing `TooltipText` assignment.
- No global hover manager that replaces Godot's native tooltip behavior in one step.
- No change to battle Runtime, Strategic Management, construction placement rules, or command validation.
- No requirement to merge battle grid hover frames, building footprint previews, or debug panels into generic UI tooltip scenes.
- No new UI art pack or theme migration beyond reusing accepted theme variations.

## Acceptance Criteria

- `system-design/presentation-ui-layout-architecture.md` defines the three hover presentation paths.
- The authority document limits `TooltipText` to simple low-risk text.
- The authority document requires complex hover details to use authored tooltip scenes rather than ad hoc local control trees.
- The authority document keeps map and battle hover overlays under their subsystem presenters while requiring shared styling, naming, positioning, and data-source boundaries.
- The authority document states that hover presentation is display-only and cannot own gameplay state, strategic rules, battle validation, or command legality.
- A follow-up implementation proposal can migrate one slice at a time without blocking unrelated simple `TooltipText` usage.
