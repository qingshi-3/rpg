# Site Management Fullscreen Tab Rail Proposal

Status: Archived

## Relationship Metadata

- Requirement Id: UI-SITE-001
- Parent Proposal: None
- Supersedes: None
- Superseded By: None
- Amends: None
- Amended By: None
- Affected Authority Documents:
  - `system-design/presentation-ui-layout-architecture.md`
- Related Implementation Proposals:
  - Pending: `gameplay-alignment/implementation-proposals/2026-07-08-site-management-fullscreen-tab-rail.md`

## Scope

This proposal affects only the peacetime management UI after entering a player-held city or managed world site.

It does not change strategic-world selected-location UI, battle preparation, battle runtime, Strategic Management state authority, city command validation, or gameplay resource rules.

## Current Architecture

The accepted Presentation/UI architecture currently defines site management as a deliberate split-screen workspace. `WorldSitePeacetimeHud.tscn` keeps a permanent `SitePeacetimePanel` on the left, while the site map starts to the right of that panel.

That model keeps management controls visible, but it makes the city map feel secondary and leaves limited space for the actual function lists, especially recruitment and other card-heavy workflows.

## Expected Architecture

City/site management should become map-first:

```text
fullscreen site map
-> left vertical function tab rail
-> click a tab
-> open only that function's overlay panel
-> hide the other tabs while the panel is open
-> close panel to return to the tab rail
```

The tab rail uses `assets/textures/ui/tinyrpg_manasoulgui_v_1_0/20250420manaTabD-Sheet.png` as the first visual source. The implementation should cut or region the atlas into authored tab/button resources instead of stretching the raw sheet as a whole image.

Hover animation is presentation-only. It may slide, nudge, tint, or swap the tab frame state, but it must not own gameplay state or command legality.

Function panels are not all fullscreen. Fullscreen is the map rule, not the panel rule:

- overview and conscription should use compact panels sized to their content;
- build selection should use a left or near-left picker panel, then hide it during map placement so the footprint preview owns the interaction;
- recruitment can keep a large bounded workbench because hero selection and troop cards need space, but it should still remain a focused overlay rather than a default full-screen replacement;
- future city functions should choose the smallest panel size that keeps the current task readable.

Tabs represent major functions such as build, conscription, recruitment, and overview. The first-version separate corps tab remains excluded unless a later accepted workflow gives it a distinct purpose outside the recruitment workbench.

## Non-Goals

- No strategic-world selected-location redesign.
- No gameplay resource, refund, recruitment, building, or city-state rule changes.
- No new persistent city data model.
- No battle preparation or battle runtime layout change.
- No final project-wide UI theme routing decision.
- No requirement that every function panel become fullscreen.

## Acceptance Criteria

- The authority document states that entered-city/site management keeps the site map fullscreen.
- The authority document replaces the permanent split-screen site-management panel rule with a left vertical tab rail plus overlay function panels.
- The tab rail source texture is named and routed as a Presentation/UI visual resource.
- The interaction contract says hover animation is display-only.
- The interaction contract says opening one function hides the other tabs until the function panel closes.
- The function-panel sizing rule says panels are task-sized and not fullscreen by default.
- Build placement hides management panels while map placement and footprint feedback are active.
- Implementation may proceed only through a focused implementation proposal after the authority document is merged and this proposal is archived.
