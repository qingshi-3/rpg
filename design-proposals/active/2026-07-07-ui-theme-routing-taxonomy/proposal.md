# UI Theme Routing Taxonomy Proposal

Status: Deferred Draft

## Relationship Metadata

- Requirement Id: UI-THEME-001
- Parent Proposal: None
- Supersedes: None
- Superseded By: None
- Amends: None
- Amended By: None
- Affected Authority Documents:
  - `system-design/presentation-ui-layout-architecture.md`
  - `system-design/resource-authoring-taxonomy.md`
- Related Implementation Proposals:
  - None yet. Create one only after this proposal is accepted and merged into authority documents.

## Purpose

This proposal preserves a known UI architecture debt: the project needs a clear theme-routing taxonomy that says which UI surface should use which `Theme` resource, which theme variations are canonical, and when a local override is allowed.

The routing is intentionally not decided yet. UI surfaces and visual direction are still moving. Locking the map now would likely create churn. The reason to keep this active proposal is to make sure the work is not forgotten when the main UI surfaces are closer to complete.

## Current Problem

The current UI theme system has grown through practical iteration:

- `game-ui-focus_defaults.tres` provides project-level defaults such as hidden focus visuals, transparent popup shells, and common scrollbars.
- `game-ui-skin` contains the broad strategic/world UI skin resources and some specialized button/card variations.
- `mana-soul-gui` contains a large imported/generated theme family from the ManaSoul UI pack.
- `recruitment-ui-v1` contains custom recruitment and military-workbench skins, including the shared scrollbar style currently reused by other themes.
- `travel-book-lite` and earlier UI skins still exist as specific theme/resource families.
- Some scenes use a root theme and `theme_type_variation`; others still have local style overrides for panels or special controls.

This is workable during UI exploration, but it is not a stable long-term contract. Without a routing taxonomy, future UI work can accidentally duplicate theme resources, mix unrelated skins, or leave one-off overrides that make visual polish and maintenance harder.

## Deferred Expected Architecture

When player-facing UI is mostly in place, the accepted UI architecture should define a routing matrix similar to:

```text
UI surface / interaction context
-> owning theme family
-> canonical Theme resource
-> allowed theme_type_variation values
-> allowed local override exceptions
-> regression guard
```

The final matrix should decide, at minimum:

- which theme owns global defaults such as focus, popup shell transparency, scrollbars, and native tooltip shell cleanup;
- which theme owns strategic-world HUD, selected-location overlays, site-management panels, battle-gate dialogs, and settlement dialogs;
- whether recruitment, conscription, corps editing, and military workbench UI stay on a dedicated recruitment theme or merge into a broader game UI theme;
- whether building picker cards remain a specialized inventory/build-preview skin or are folded into a shared card-button route;
- how battle preparation, battle runtime HUD, command buttons, skill buttons, roster rows, and unit preview cards map to theme families;
- which UI surfaces are allowed to use fixed visual assets without becoming a general Theme route;
- how debug/tool scenes are excluded from player-facing theme-routing rules;
- what naming convention future Theme resources and theme variations must follow.

## Open Questions

- Should the final project have one primary player-facing Theme with many variations, or several domain-owned Theme families with a shared global default layer?
- Should `recruitment-ui-v1` remain a domain skin, or should its successful controls become shared game UI assets?
- Should the shared scrollbar live in the recruitment theme family, or move to a neutral common UI theme directory before the routing is finalized?
- Should build/inventory card UI stay intentionally distinct from general buttons, or be normalized into the same button/card taxonomy?
- Which scene-level `theme_override_*` usages are legitimate exceptions, and which should become Theme variations?
- Should a machine-readable manifest drive regression tests for scene-to-theme mapping?

## Resume Trigger

Return to this proposal when at least one of these is true:

- most first-playable player-facing UI surfaces exist and are no longer changing shape every session;
- another UI skin pack is about to be imported or generated;
- UI polish work begins before a milestone or release;
- theme duplication, visual inconsistency, or local overrides start slowing implementation;
- a future task asks why different screens use different Theme resources.

## Non-Goals

- Do not restyle UI immediately from this proposal.
- Do not delete current Theme resources just because this proposal exists.
- Do not decide the final theme-routing matrix until the relevant UI surfaces are mature enough to evaluate together.
- Do not use this proposal as code, scene, or resource implementation authority.
- Do not move accepted authority documents until the user accepts a concrete routing plan.

## Acceptance Criteria

- The accepted UI architecture defines a theme-routing taxonomy for player-facing UI.
- The taxonomy names the owner and intended use of each canonical Theme family.
- A routing matrix maps major UI surfaces to Theme resources and theme variations.
- Local style override exceptions are narrow and documented.
- Shared cross-theme primitives such as focus style, popup shell cleanup, and scrollbars have a neutral ownership rule.
- Regression tests or resource-authoring guards prevent player-facing scenes from drifting back into ad hoc Theme duplication.
- A focused implementation proposal exists before any code, scene, or resource migration starts.
