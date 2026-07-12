# Scene Transition Router And Preload Cache Proposal

Status: Archived
Created: 2026-05-19
Accepted: 2026-05-19
Implemented: 2026-05-19 (router boundary v0; preload cache and loading overlay deferred)
Merged: 2026-05-19
Archived: 2026-05-19

## Reason

The project currently switches between strategic world, site detail, battle preparation, battle runtime, settlement, and return flows through direct `SceneTree.ChangeSceneToFile` calls inside scene roots. Battle and site data are passed through static handoff stores such as `StrategicWorldRuntime` and `BattleSessionHandoff`.

This works for the current playable path, but it is becoming fragile:

- scene roots own both UI behavior and root scene switching;
- battle handoff, site handoff, rollback, and scene replacement are split across callers;
- overlapping clicks or repeated modal confirmations can trigger duplicate transition behavior;
- loading, progress, preloading, and failure logs have no common owner;
- future city, ruin, dungeon, opportunity, battle report, and bridge battle flows will multiply the same risks.

The accepted gameplay loop depends on reliable movement between strategic preparation, managed locations, authored battle maps, battle reports, and long-term state writeback. Scene switching therefore needs a single architecture boundary before more content breadth is added.

## Affected Authority Documents

- `system-design/README.md`
- `system-design/hero-led-light-rts-system-architecture.md`
- `system-design/presentation-ui-layout-architecture.md`
- `system-design/scene-transition-router-architecture.md` (new)

## Current Design Or Architecture

Current authority documents already place scene switching under Infrastructure support and define that Presentation/UI submits intent rather than owning gameplay truth. They do not yet define a concrete owner for root scene replacement, loading overlays, preload hints, transition busy state, failure rollback, or direct scene-change restrictions.

Current implementation has several direct scene switch sites:

- strategic world enters site detail by setting `StrategicWorldRuntime.BeginSiteVisit(...)` and calling `ChangeSceneToFile(SiteScenePath)`;
- strategic world enters battle by calling `BattleSessionHandoff.BeginBattle(...)` and then `ChangeSceneToFile(request.SiteScenePath)`;
- world site returns to the strategic map by calling `StrategicWorldRuntime.MarkWorldResumeAfterSiteReturn()` and then `ChangeSceneToFile(returnScenePath)`.

Preloading is ad hoc UI warmup for reusable controls, not a root-scene preload architecture. There is no accepted cache budget, TTL, pin, or eviction model for main scene resources.

## Expected Design Or Architecture

Introduce `SceneTransitionRouter` as the single authority for player-facing root scene switching.

Presentation roots and UI panels no longer call `ChangeSceneToFile` directly for strategic/site/battle/return flows. They submit typed transition requests after Application-level validation. The router owns:

- transition busy state and duplicate-entry rejection;
- setting and clearing handoff data for the current migration stage;
- showing and hiding an authored loading overlay;
- resolving cached or synchronously loaded `PackedScene` resources;
- calling `ChangeSceneToPacked` or `ChangeSceneToFile`;
- waiting for `SceneTree.SceneChanged` before treating the transition as entered;
- rollback and handoff cancellation on failure;
- low-noise transition diagnostics.

Introduce `ScenePreloadCache` as an optional acceleration layer below the router.

The cache may preload `PackedScene` resources using Godot threaded loading APIs. It must not instantiate scene nodes, touch the active scene tree from a background thread, own battle or city state, or infer gameplay rules. It accepts explicit `ScenePreloadHint` requests from Application or Presentation-adjacent flow code and applies a small budget, TTL, LRU, and pin policy.

V0 keeps `StrategicWorldRuntime` and `BattleSessionHandoff` as migration handoff stores. The router centralizes when they are written, canceled, or consumed. A future proposal may replace them with a unified `SceneTransitionContext`, but that is not required for the first implementation.

## Implementation Scope

Expected implementation impact:

- add a router/service node or autoload for root scene transitions;
- add typed transition request/result records;
- add a resource-only preload cache with conservative budget;
- add an authored loading overlay scene if a transition can visibly wait;
- migrate direct strategic world, site detail, battle entry, and return calls behind the router;
- preserve current battle request, site visit, return path, and rollback behavior;
- add regression coverage that direct root scene switching is limited to the router boundary;
- add tests for failed battle scene change canceling the active handoff and restoring rollback state;
- add tests or lightweight checks for cache TTL/LRU/pin behavior if implemented in the same slice.

Out of scope for this proposal:

- replacing `StrategicWorldRuntime` and `BattleSessionHandoff` completely;
- mid-battle save;
- a persistent scene stack with back-navigation;
- caching instantiated scene nodes;
- streaming battle map chunks;
- automatic background scanning of world state by the cache;
- changing the accepted gameplay loop or battle runtime truth.

## Acceptance

Architecture acceptance:

- `system-design/scene-transition-router-architecture.md` exists and is listed as an accepted system document after merge;
- affected expected documents define scene switching as a single router-owned Infrastructure boundary;
- preloading is described as a resource cache only, not a gameplay prediction owner;
- direct scene switching restrictions and failure rules are explicit.

Implementation acceptance, when this proposal is implemented:

- strategic world to site detail enters through the router and preserves pending site visit behavior;
- strategic world to battle preparation/runtime enters through the router and preserves battle handoff plus rollback behavior;
- site return to strategic world enters through the router and preserves world resume behavior;
- duplicate transition requests cannot start overlapping scene changes;
- failed battle scene transition cancels active battle handoff and restores pre-transition world/site mode;
- failed site transition clears pending site visit;
- preloading, if enabled in the slice, only caches `PackedScene` resources and respects budget/TTL/pin behavior;
- no new direct root `ChangeSceneToFile` calls appear outside the router boundary.

## Merge Plan

Copy expected documents into authority paths:

- `design-proposals/active/2026-05-19-scene-transition-router/expected/system-design/README.md` -> `system-design/README.md`
- `design-proposals/active/2026-05-19-scene-transition-router/expected/system-design/hero-led-light-rts-system-architecture.md` -> `system-design/hero-led-light-rts-system-architecture.md`
- `design-proposals/active/2026-05-19-scene-transition-router/expected/system-design/presentation-ui-layout-architecture.md` -> `system-design/presentation-ui-layout-architecture.md`
- `design-proposals/active/2026-05-19-scene-transition-router/expected/system-design/scene-transition-router-architecture.md` -> `system-design/scene-transition-router-architecture.md`

## Archive Note

The implemented 2026-05-19 slice completed the router boundary, typed requests/results, failure rollback, duplicate-transition rejection, and presentation-root migration. The preload cache and loading overlay remain deferred implementation work under the accepted architecture rules.
