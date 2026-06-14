# Scene Transition Router Architecture

Status: Accepted Architecture

## Gameplay Authority

This document supports `gameplay-design/content-systems-long-term-design.md` and `system-design/hero-led-light-rts-system-architecture.md`.

The accepted game loop moves the player through strategic preparation, strategic locations, battle preparation, live battle, settlement reports, and campaign writeback:

```text
prepare heroes, corps, equipment, cities, and resources
-> enter strategic location or authored battle map
-> deploy and command hero-led battle groups
-> resolve runtime battle
-> explain result through a battle report
-> write consequences back to long-term state
-> return to strategic play
```

Scene transitions support that loop. They do not decide gameplay outcomes.

## Responsibility

`SceneTransitionRouter` is the single owner of player-facing root scene replacement.

It owns:

- typed transition requests between strategic world, strategic-location detail, battle preparation/runtime, settlement report, and return flows;
- transition busy state and duplicate transition rejection;
- setup and cleanup of typed scene-entry handoff payloads;
- loading overlay visibility and progress display;
- root scene loading through cached `PackedScene` resources or direct scene path loading;
- the actual `SceneTree.ChangeSceneToPacked` or `SceneTree.ChangeSceneToFile` call;
- waiting for `SceneTree.SceneChanged` before marking a transition entered;
- transition failure rollback, handoff cancellation, and low-noise diagnostics.

`ScenePreloadCache` is an optional acceleration service used by the router.

It owns:

- resource-only preload requests for `PackedScene` paths;
- Godot threaded resource load status polling;
- small cache budget enforcement;
- TTL, LRU, and pin-based eviction;
- preload failure diagnostics.

## Does Not Own

The router and cache do not own:

- hero, corps, battle group, city, strategic-location, equipment, or resource truth;
- battle runtime execution, damage, movement, outcome, settlement, or report truth;
- command validation or battle-entry gameplay rules;
- tactical AI, expedition navigation, or enemy intent prediction;
- UI layout authority beyond showing a transition/loading overlay;
- long-term state schema;
- instantiated scene-node pooling;
- automatic background scanning of the whole world to guess future gameplay.

## Persistent State

Scene transition state is not persistent save state in the first phase.

Persistent campaign state remains owned by Domain and Application services. If a transition fails, the router must restore or cancel the operation without fabricating persistent outcomes.

Future scene-resume support requires a separate proposal that defines which snapshot, event boundary, and runtime state are persistable.

## Runtime State

Router runtime state may include:

- `IsTransitioning`;
- active transition ID;
- active transition kind;
- source scene path;
- target scene path;
- requested return scene path;
- rollback token or callback supplied by the caller/application service;
- loading overlay state;
- handoff setup state;
- scene-change start time for diagnostics;
- final transition result.

For Strategic Management-backed battles, the battle handoff runtime state is a Bridge Active Context or typed reference owned by the Strategic Battle Bridge. The router may carry, write, or cancel that context during transition, but it does not own the contained gameplay facts.

Preload cache runtime state may include:

- scene path;
- load status: `None`, `Loading`, `Loaded`, `Failed`;
- priority;
- reason;
- expiry time or world tick;
- last-used time;
- pin count;
- loaded `PackedScene` reference;
- last load error.

## Inputs

Presentation/UI may request transitions only through typed APIs.

Recommended V0 transition request types:

| Request | Source | Target | Handoff |
|---|---|---|---|
| `EnterStrategicWorld` | startup, site return, battle settlement return | strategic world scene | optional resume flag |
| `EnterSiteDetail` | strategic selection | world site scene | `StrategicWorldRuntime.BeginSiteVisit` during migration |
| `EnterBattlePreparation` | strategic battle bridge active context | battle preparation/runtime scene | typed Bridge Active Context handoff |
| `ReturnFromSite` | site management UI | strategic world scene | `StrategicWorldRuntime.MarkWorldResumeAfterSiteReturn` |
| `ReturnAfterBattleSettlement` | settlement/report UI | strategic world scene | consumed battle result/writeback must already be resolved |

Application and world-flow code may submit preload hints:

```text
ScenePreloadHint(
    scenePath,
    reason,
    priority,
    expiresAt,
    pinPolicy)
```

Preload hints are advisory. They never grant permission to enter a scene.

## Outputs

Router outputs:

- `SceneTransitionResult` with success/failure, target path, elapsed time, and failure reason;
- low-noise diagnostic logs for transition start, success, failure, cancel, cache hit, cache miss, and rollback;
- optional UI-visible loading or failure notice;
- scene-entered event after `SceneTree.SceneChanged`.

Preload cache outputs:

- cache hit/miss status;
- loading progress where available;
- failed preload reason;
- evicted scene path and reason for diagnostics.

## Contracts

### Single Root Switch Authority

Gameplay-facing code must not call root `SceneTree.ChangeSceneToFile` or `SceneTree.ChangeSceneToPacked` directly.

Allowed exceptions:

- the router implementation itself;
- isolated tests, editor tooling, or diagnostic utilities that do not represent player-facing gameplay flow;
- engine/editor-only code that is clearly outside runtime scene navigation.

### Handoff Contract

The router carries typed handoff payloads for scene-entry transitions:

- strategic-location visit context for site-detail entry and return;
- Strategic Battle Bridge Active Context for battle preparation/runtime entry and cancellation.

The router owns when these handoff payloads are written or canceled for scene-entry transitions. Scene roots may consume handoff data, but they must not create competing handoff state.

`BattleSessionHandoff` is a legacy migration store, not the long-term battle bridge authority. New Strategic Management battle entry must use the Strategic Battle Bridge Active Context and must not require `BattleSessionHandoff` to boot or return from battle. A future unified `SceneTransitionContext` is allowed if it preserves this ownership boundary.

### Application Validation Contract

The router does not decide whether a site may be entered, whether a battle may start, who owns a city, whether an army has arrived, or whether a battle group can sortie.

Callers must perform or request Application-level validation before submitting a transition. The router may reject malformed requests, missing paths, busy transitions, or missing required handoff payloads.

### Preload Resource Contract

The preload cache caches resources only:

- cache `PackedScene`;
- do not cache instantiated `Node` trees;
- do not access or mutate the active scene tree from a background thread;
- do not store battle requests, city state, snapshots, or settlement data as cache entries;
- do not treat cache hit as entry permission.

Scene instantiation and root scene replacement happen on the main scene transition path.

### Loading Overlay Contract

The loading overlay is an authored Godot scene/control. Runtime code may instantiate or bind it, but must not build a new permanent UI tree with hardcoded controls.

Player-visible loading and failure text defaults to Chinese.

## Game-Flow Preload Policy

Preloading exists to reduce stalls in expected near-term transitions. It is not a gameplay prediction engine.

Recommended V0 hint sources:

| Flow Moment | Hint | Priority | Notes |
|---|---|---|---|
| Player keeps a visible strategic location selected | site detail scene | Low | Debounce selection before hinting. |
| Player begins expedition target confirmation | likely battle/site scene | Medium | Only after a concrete target exists. |
| Army is close to arriving at an enemy strategic location | battle preparation scene | High | World progression code supplies the hint. |
| Pre-battle dialog or battle gate opens | target battle scene | High, pinned | Pin until player chooses enter/withdraw/cancel. |
| Site detail or battle scene is active | strategic world scene | Medium | Returning to strategic world is likely. |
| Settlement report is visible | strategic world scene | High | Return should be responsive. |

Do not preload every visible strategic location. Do not let the cache poll all armies and opportunities by itself.

## Cache Budget And Eviction

V0 cache limits should be conservative:

```text
MaxCachedScenes: 2 or 3 non-current root scenes
DefaultTTL: about 60 seconds
LowPriorityTTL: about 20-30 seconds
HighPriorityTTL: about 90-120 seconds
```

Eviction order:

```text
never evict pinned entries
-> evict expired entries
-> evict failed entries
-> evict lower priority entries
-> evict least recently used entries
```

If the cache is full and all entries are pinned, the new preload hint is ignored or delayed. It must not force eviction of a transition-critical scene.

## Failure Rules

Invalid transition request:

- reject before handoff is written;
- log failure reason;
- show caller/UI feedback if appropriate.

Preload failure:

- record failed status;
- do not mutate game state;
- transition may still attempt direct load when requested;
- repeated preload retries should be throttled.

Scene load failure before handoff:

- reject transition;
- keep current scene active;
- log target path and error.

Scene change failure after battle handoff:

- cancel active battle handoff or Bridge Active Context;
- run supplied rollback token/callback;
- restore prior world/site mode where possible;
- show failure notice;
- keep or return to a consistent scene state.

Scene change failure after site-visit handoff:

- clear pending site visit;
- restore caller UI state where possible;
- show failure notice.

Destination boot failure:

- destination scene must fail explicitly with diagnostics;
- do not fabricate battle result, victory, defeat, site visit, or settlement;
- if boot failure leaves the player unable to continue, route to a safe failure state or return path through a separate accepted recovery design.

Overlapping transition:

- reject or queue according to explicit router policy;
- V0 should reject duplicate player-triggered transitions while `IsTransitioning` is true;
- rejection should be low-noise and should not create another modal loop.

## Migration Plan

### Phase 1: Router Boundary

- Add the router as the only runtime owner of root scene replacement.
- Migrate strategic-world site entry, strategic-world battle entry, and site return behind typed router calls.
- Keep only explicitly scoped migration handoff stores while moving player-facing scene changes behind the router. New Strategic Management battle entry must target the Strategic Battle Bridge Active Context.
- Add direct-scene-change regression guard.

### Phase 2: Loading Overlay And Diagnostics

- Add authored loading overlay resource.
- Wait for `SceneTree.SceneChanged` before marking success.
- Add start/success/failure timing logs.

### Phase 3: Conservative Preload Cache

- Add resource-only `ScenePreloadCache`.
- Support manual preload hints from stable selection, pre-battle gate, and return-to-world flows.
- Enforce small budget, TTL, LRU, and pin behavior.

### Phase 4: World-Flow Hints

- Add high-priority hints from world progression when an army is near a likely battle transition.
- Keep prediction outside the cache. World systems provide explicit hints.

### Phase 5: Unified Transition Context

- Consider consolidating remaining static handoff stores into a typed `SceneTransitionContext`.
- This phase needs its own proposal if it changes persistent state, runtime ownership, or save/resume behavior.

## Acceptance

This architecture is acceptable when:

- future work can identify the single owner for root scene replacement;
- scene roots submit transition requests instead of performing player-facing root scene changes directly;
- Application remains responsible for gameplay validation before transitions;
- Runtime and Settlement remain responsible for battle truth and writeback;
- preload caching is clearly limited to `PackedScene` resources;
- cache hints are explicit and bounded, not autonomous world scanning;
- failure and rollback semantics are documented for battle, site, and return flows;
- Strategic Management-backed battle transitions use Bridge Active Context instead of static `BattleSessionHandoff` as authority;
- the first implementation can migrate current direct scene switches without changing the accepted gameplay loop.
