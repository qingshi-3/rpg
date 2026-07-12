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
- root scene loading through direct scene paths or an explicitly supplied `PackedScene`;
- the actual `SceneTree.ChangeSceneToPacked` or `SceneTree.ChangeSceneToFile` call;
- waiting for `SceneTree.SceneChanged` before marking a transition entered;
- transition failure rollback, handoff cancellation, and low-noise diagnostics.

A future `ScenePreloadCache` is an optional acceleration service for the router. It is not a current capability. If implemented, that cache would own:

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
- UI layout authority beyond a future transition/loading overlay;
- long-term state schema;
- instantiated scene-node pooling;
- automatic background scanning of the whole world to guess future gameplay.

## Persistent State

Scene transition state is not persistent save state.

Persistent campaign state remains owned by Domain and Application services. If a transition fails, the router must restore or cancel the operation without fabricating persistent outcomes.

Future scene-resume support requires confirmed discussion and an authority update defining which snapshot, event boundary, and runtime state are persistable.

## Runtime State

Router runtime state may include:

- `IsTransitioning`;
- active transition ID;
- active transition kind;
- source scene path;
- target scene path;
- requested return scene path;
- rollback token or callback supplied by the caller/application service;
- handoff setup state;
- scene-change start time for diagnostics;
- final transition result.

For Strategic Management-backed battles, the battle handoff runtime state is a Bridge Active Context or typed reference owned by the Strategic Battle Bridge. The router may carry, write, or cancel that context during transition, but it does not own the contained gameplay facts.

When implemented, preload cache runtime state may include:

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

Current transition request families:

| Request | Source | Target | Handoff |
|---|---|---|---|
| `EnterStrategicWorld` | startup, site return, battle settlement return | strategic world scene | optional resume flag |
| `EnterSiteDetail` | strategic selection | world site scene | site-visit context used only for scene/world-flow handoff |
| `EnterBattlePreparation` | strategic battle bridge active context | battle preparation/runtime scene | typed Bridge Active Context handoff |
| `ReturnFromSite` | site management UI | strategic world scene | world-resume context used only for scene/world-flow handoff |
| `ReturnAfterBattleSettlement` | settlement/report UI | strategic world scene | consumed battle result/writeback must already be resolved |

A future preload cache may accept explicit hints from Application and world-flow code:

```text
ScenePreloadHint(
    scenePath,
    reason,
    priority,
    expiresAt,
    pinPolicy)
```

Future preload hints are advisory. They never grant permission to enter a scene.

## Outputs

Router outputs:

- `SceneTransitionResult` with success/failure, target path, elapsed time, and failure reason;
- low-noise diagnostic logs for transition start, success, failure, cancel, and rollback;
- optional UI-visible failure notice;
- scene-entered event after `SceneTree.SceneChanged`.

Future preload cache outputs:

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

`BattleSessionHandoff` is a legacy compatibility store, not the current battle bridge authority. Strategic Management battle entry uses Strategic Battle Bridge Active Context and must not require `BattleSessionHandoff` to boot or return from battle. A future unified `SceneTransitionContext` is allowed if it preserves this ownership boundary.

### Application Validation Contract

The router does not decide whether a site may be entered, whether a battle may start, who owns a city, whether an army has arrived, or whether a battle group can sortie.

Callers must perform or request Application-level validation before submitting a transition. The router may reject malformed requests, missing paths, busy transitions, or missing required handoff payloads.

### Deferred Preload Resource Contract

The preload cache caches resources only:

- cache `PackedScene`;
- do not cache instantiated `Node` trees;
- do not access or mutate the active scene tree from a background thread;
- do not store battle requests, city state, snapshots, or settlement data as cache entries;
- do not treat cache hit as entry permission.

Scene instantiation and root scene replacement happen on the main scene transition path.

### Deferred Loading Overlay Contract

The loading overlay is an authored Godot scene/control. Runtime code may instantiate or bind it, but must not build a new permanent UI tree with hardcoded controls.

Player-visible loading and failure text defaults to Chinese.

## Deferred Game-Flow Preload Policy

Preloading exists to reduce stalls in expected near-term transitions. It is not a gameplay prediction engine.

Potential future hint sources:

| Flow Moment | Hint | Priority | Notes |
|---|---|---|---|
| Player keeps a visible strategic location selected | site detail scene | Low | Debounce selection before hinting. |
| Player begins expedition target confirmation | likely battle/site scene | Medium | Only after a concrete target exists. |
| Army is close to arriving at an enemy strategic location | battle preparation scene | High | World progression code supplies the hint. |
| Pre-battle dialog or battle gate opens | target battle scene | High, pinned | Pin until player chooses enter/withdraw/cancel. |
| Site detail or battle scene is active | strategic world scene | Medium | Returning to strategic world is likely. |
| Settlement report is visible | strategic world scene | High | Return should be responsive. |

Do not preload every visible strategic location. Do not let the cache poll all armies and opportunities by itself.

## Deferred Cache Budget And Eviction

Future cache limits should be conservative:

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

- cancel only the Bridge Active Context matching the transition's expected context/session/snapshot identity;
- invoke the supplied identity-matched Strategic Management/Bridge rollback boundary;
- restore every surviving participant to the exact rollback station already recorded by Strategic Management and clear expedition/carrier association atomically;
- restore prior world/site mode where possible;
- show failure notice;
- keep or return to a consistent scene state.

The router cannot invent a station, settlement identity, or strategic consequence. A stale transition callback that no longer matches the active context must fail without clearing the newer context or rolling back its expedition.

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
- the current policy rejects duplicate player-triggered transitions while `IsTransitioning` is true;
- rejection should be low-noise and should not create another modal loop.

## Current Capability And Future Extensions

### Current Router Boundary

- The router is the only runtime owner of player-facing root scene replacement.
- Strategic-world site entry, Strategic Management battle entry, site return, and post-battle return use typed transition requests.
- Strategic Management battle entry carries Strategic Battle Bridge Active Context; retained site-visit carriers are scene/world-flow adapters, not strategic gameplay authority.
- Direct player-facing root scene changes outside the router are rejected by regression guards.

### Deferred Loading Presentation

- The authored loading overlay is not a current player-facing capability.
- A future loading surface must remain Presentation-only, wait for scene entry before reporting success, and show router-provided failure state without fabricating gameplay results.

### Deferred Conservative Preload Cache

- Scene preloading is not a current capability.
- A future cache is limited to `PackedScene` resources, accepts only explicit hints from stable gameplay context, and enforces a small budget, TTL, LRU, and pin behavior.

### Later World-Flow Hints

- World progression may later provide high-priority hints when an army is near a likely battle transition.
- Prediction stays outside the cache; world systems provide explicit hints.

### Optional Unified Transition Context

- Consider consolidating remaining static handoff stores into a typed `SceneTransitionContext`.
- This phase must return to discussion and update authority if it changes persistent state, runtime ownership, or save/resume behavior.

## Acceptance

This architecture is acceptable when:

- the running game has one identifiable router owner for player-facing root scene replacement;
- scene roots submit transition requests instead of performing player-facing root scene changes directly;
- Application remains responsible for gameplay validation before transitions;
- Runtime and Settlement remain responsible for battle truth and writeback;
- any later preload cache is limited to `PackedScene` resources;
- any later cache hints are explicit and bounded, not autonomous world scanning;
- failure and rollback semantics are documented for battle, site, and return flows;
- Strategic Management-backed battle transitions use Bridge Active Context instead of static `BattleSessionHandoff` as authority;
- current strategic-world, site, battle, and return transitions preserve the accepted gameplay loop behind typed router requests.
