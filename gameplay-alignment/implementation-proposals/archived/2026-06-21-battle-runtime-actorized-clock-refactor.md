# Battle Runtime Actorized Clock Refactor

Status: Accepted - Archived

## Origin

- Requirement: BRTL-001
- Design Discussion: runtime combat must move from center-heavy resolver orchestration toward actor-local action execution, while preserving deterministic battle truth and arbitrary-time tactical pause.
- Authority:
  - `system-design/battle-runtime-architecture.md`
  - `system-design/battle-command-architecture.md`
  - `system-design/battle-ai-boundary-architecture.md`
  - `system-design/battle-navigation-topology-architecture.md`
  - `system-design/battle-content-progression-architecture.md`
- Related Implementation Proposals:
  - None yet.

## Goal

Refactor Battle Runtime so player and AI commands modify intent, each runtime actor consumes authoritative battle time through actor-local controllers, effects are received by actors through explicit effect input, and final world mutations are applied through a deterministic commit barrier.

The refactor must keep tactical pause valid at any runtime or presentation moment:

```text
Runtime battle truth freezes
Presentation battle playback freezes
UI command and preview interaction continues
```

## Current Problem

The current implementation has the right high-level authority boundary, but the code shape is too center-heavy:

- `BattleRuntimeTickResolver` advances movement boundaries, resolves pending skills, builds decision contexts, applies decision outcomes, resolves attacks, commits movement, and logs action results in one central flow.
- `BattleRuntimeActor` is mainly a mutable data record, not an actor-local runtime object with owned action, ability, effect, and health processing.
- `BattleAttackResolver` batches all basic attacks in a center service and directly writes target HP and defeated state.
- `BattleEffectResolver` advances channels, scans all actors for area damage, directly mutates target HP, and marks defeated targets.
- `BattleRuntimeHeroSkillCommandResolver` owns active skill lifecycle and channel progression centrally instead of letting the caster's actor-local ability runtime consume time.
- Presentation uses async task tails for visual ordering. These tails are useful, but arbitrary-time pause requires battle presentation waits, tweens, animations, particles, and hit/death timing to be driven by a pausable battle presentation clock rather than real time.

This makes combat hard to reason about because one center resolver both interprets intent and directly mutates many actors. Mature realtime combat implementations usually keep center systems for world time, spatial authority, conflict resolution, and deterministic commits, while actor-local components own action execution and effect response.

## Target Architecture

### Runtime Layers

```text
BattleCommand / BattleGroupPlan / UI input
-> BattleIntentStore
-> BattleSimulationWorld tick snapshot
-> BattleActorRuntime.Tick for each actor
-> BattleCommitBuffer
-> deterministic commit barrier
-> BattleEventStream
-> Presentation observer
```

### Runtime Objects

```text
BattleSimulationWorld
  BattleRuntimeClock
  BattleIntentStore
  BattleSpatialIndex
  BattleEffectField
  BattleReservationService
  BattleCommitBuffer
  BattleEventStream
  BattleActorRuntime[]

BattleActorRuntime
  BattleActorState
  BattleIntentComponent
  BattleActionController
  BattleMovementController
  BattleAbilityController
  BattleEffectReceiver
  BattleHealthComponent
```

The intended code shape is component-oriented C# runtime logic, not Godot scene nodes. Godot Presentation remains a consumer of Runtime events.

### Ownership Rules

Player and AI command systems own intent submission and validation. They must not directly move actors, apply damage, finish casts, or kill actors.

`BattleActorRuntime` owns actor-local time consumption. It decides how the actor advances its current phase, when it proposes movement, when it starts or continues an action, when it consumes an effect input, and when it requests health or defeat changes.

`BattleCommitBuffer` owns mutation requests for the current tick. Actor-local controllers write requests; they do not directly mutate other actors or global spatial state.

The deterministic commit barrier owns final writes to authoritative battle state:

- movement reservation acceptance or rejection;
- anchor and occupancy updates;
- attack impact and effect application order;
- HP mutation;
- defeated state;
- command state events;
- battle event stream order.

Presentation owns only visual playback. It must not create Runtime damage, movement, pathfinding, target choice, or action completion truth.

## Pause And Time Model

Arbitrary-time pause requires three separate clocks.

```text
RuntimeClock
  Owns simulation time, actor phase time, cooldown, action impact, recovery, movement progress, channel ticks, effect durations.

PresentationClock
  Owns battle visual waits, movement interpolation, attack animation progress, skill effects, hit feedback, health-bar timing, death animation timing, battle particles, and battle shader time.

UiClock
  Owns command input, tactical pause UI, selection, hover, range preview, menus, and non-battle UI affordances.
```

### Pause Contract

When battle pause is active:

- `RuntimeClock` does not advance.
- `BattleActorRuntime.Tick` does not advance actor action time.
- `BattleAbilityController` does not advance casts, cooldowns, recovery, channels, or tick intervals.
- `BattleEffectField` does not advance effect duration or next tick time.
- `BattleCommitBuffer` does not submit new combat facts except command acceptance, rejection, or supersession facts that are explicitly allowed during pause.
- `PresentationClock` freezes battle lanes at their current visual progress.
- actor movement interpolation stops at the current pixel position;
- attack, cast, hit, death, and skill visual sequences stop at their current playback progress;
- battle particles, tweens, and shader animation used for combat feedback stop or are driven by pausable battle time;
- `UiClock` continues for selection, targeting preview, command panels, tooltips, and tactical pause menu.

Commands submitted during pause may update intent or pending command state. They do not advance cast time, damage, cooldown, area ticks, movement boundaries, or visible battle playback until battle time resumes.

### Resume Contract

On resume:

- `RuntimeClock` continues from the frozen runtime time.
- actor movement progress, action impact timers, skill channel windows, and effect durations continue from their frozen values.
- `PresentationClock` continues visual playback from the frozen frame or interpolated position.
- already queued visual tasks resume from remaining battle-presentation time, not from elapsed wall-clock time.

### Forbidden Patterns

- Runtime logic must not use `DateTime`, `Stopwatch`, Godot `Timer`, `Task.Delay`, or unscaled wall-clock time for combat progression.
- Runtime effects must not depend on AnimationPlayer, Tween, particle, or shader completion callbacks.
- Presentation async waits for battle playback must not use non-pausable real-time waits.
- Runtime state must not be advanced by Presentation animation-finished callbacks.
- Battle shader or particle effects that must freeze during pause must not rely on engine `TIME` without a pausable uniform or explicit process-mode control.

## Scope

### 1. Runtime Clock And Pause Gate

Introduce a Runtime-owned battle clock abstraction. Existing `currentTimeSeconds` flow should be replaced or wrapped so all Runtime systems receive time only from `BattleRuntimeClock`.

The clock must expose:

- current runtime time;
- fixed tick delta;
- pause state;
- a way to advance only when unpaused;
- deterministic test helpers for headless tests.

### 2. Presentation Clock

Introduce a battle presentation clock or equivalent pause-aware wait service for visual tasks.

The presentation clock must replace battle playback waits that currently depend on raw async task timing. It should allow existing task-tail ordering to remain while freezing remaining visual wait time during pause.

Animation/tween/particle process behavior must be audited so battle visuals freeze while UI stays interactive.

### 3. Actor Runtime Shell

Introduce `BattleActorRuntime` as the code unit that wraps actor state and actor-local controllers.

The first migration may keep `BattleRuntimeActor` as the serialized/state DTO, but new behavior should route through actor runtime controllers rather than adding more center resolver branches.

### 4. Commit Buffer

Introduce `BattleCommitBuffer` for per-tick requests:

- movement requests;
- damage requests;
- effect application requests;
- command state events;
- action start, impact, recovery, complete, fail, and interrupt events.

The commit barrier applies requests in deterministic phases and emits semantic events.

### 5. Basic Attack Migration

Move basic attack lifecycle away from immediate center damage batching:

```text
ActionStarted
-> AttackWindup
-> Runtime impact boundary
-> Damage request
-> AttackRecovery
-> AnchoredDecision
```

`DamageApplied` should be emitted at Runtime impact time, not only visually delayed by Presentation. This keeps pause semantics clear when the player pauses between windup and impact.

### 6. Skill And Channel Migration

Move active skill lifecycle into `BattleAbilityController`.

The controller owns:

- pending accepted skill orders for this actor;
- cast start;
- impact time;
- recovery;
- channel lifetime;
- channel tick cadence;
- cooldown or one-use state;
- interruption eligibility.

The effect field or commit buffer should receive effect requests from the ability controller. Area effects should become world effect facts that actors consume or are deterministically delivered through the effect receiver path.

### 7. Effect Receiver And Health

Move actor damage/death reaction into actor-local receiver and health components:

```text
EffectField / CommitBuffer exposes effect applications
-> BattleEffectReceiver validates target applicability
-> BattleHealthComponent requests HP mutation or defeat
-> Commit barrier applies final HP/defeat facts
```

The final HP write may still occur in the commit barrier for determinism, but the validation and state-response logic should live with the target actor instead of being hidden inside a center resolver.

### 8. Movement Migration

Keep central spatial authority, but move movement intent and continuation decisions into actor-local movement controller.

The actor proposes movement. `BattleReservationService` and commit barrier accept or reject movement, then emit movement events.

Local steering memory remains actor-owned. Route topology and occupancy remain world-owned.

## Non-Goals

- No gameplay rule expansion.
- No new manual tactical chess or AP/turn system.
- No Godot node-driven combat truth.
- No multithreaded simulation in this slice. "Concurrent" means actor-local independent processing over the same tick snapshot, with deterministic single-threaded commit.
- No Presentation authority over Runtime action completion.
- No full ECS rewrite.
- No settlement, report, save-schema, or campaign writeback changes unless required to keep Runtime event contracts intact.
- No new final battle UI design beyond preserving pause, targeting, and playback behavior.

## Touched Systems

- Runtime battle state and session control under `src/Runtime/Battle/`.
- Runtime battle effects under `src/Runtime/Battle/Effects/`.
- Runtime battle AI and target selection under `src/Runtime/Battle/AI/` and `src/Runtime/Battle/Tactics/`.
- Runtime battle navigation and reservation under `src/Runtime/Battle/Navigation/`.
- Presentation battle playback under `src/Presentation/World/Sites/` and `src/Presentation/Battle/Entities/`.
- Battle regression tests under `tests/BattleHitFeedbackRegression/` and any focused Runtime regression suites added for actorized execution.
- Static architecture or anti-rot tests if new Runtime architecture documents or proposal guardrails are introduced.

## GodotPrompter Skills

Use these implementation skills before code/resource work:

- `csharp-godot`
- `state-machine`
- `ability-system`
- `ai-navigation`
- `godot-debugging`
- `godot-testing`
- `godot-code-review`

## Implementation Phases

### Phase 0: Characterization And Guard Rails

- Add focused characterization tests for current pause-sensitive behavior:
  - pause before attack impact;
  - pause during movement interpolation;
  - pause during skill cast;
  - pause during channeled area damage;
  - pause after fatal damage event is queued but before death presentation completes.
- Add architecture scans that prevent new Runtime logic from depending on Godot timers or raw real-time waits.
- Document any currently failing behavior as RED evidence in this proposal before refactoring.

### Phase 1: Runtime Clock

- Introduce `BattleRuntimeClock` and route `BattleRuntimeSessionController.Advance` through it.
- Keep existing tick resolver behavior initially, but require all runtime systems to receive time from the clock wrapper.
- Add tests proving pause prevents Runtime tick advancement and pending commands do not apply combat effects until resume.

### Phase 2: Presentation Clock

- Introduce a pause-aware battle presentation wait abstraction.
- Replace battle playback waits in `BattleRuntimeLivePresentationState` and related observers so visual task tails freeze when battle is paused.
- Audit unit animation, movement interpolation, skill VFX, damage feedback, health-bar timing, death timing, battle particles, and battle shaders.
- Add manual QA evidence for pause/resume at arbitrary visual moments.

### Phase 3: Commit Buffer Skeleton

- Introduce `BattleCommitBuffer` and deterministic commit phases while still feeding it from existing resolvers.
- Move direct event emission and state writes in attack/effect/movement paths behind commit request types.
- Keep output events compatible with Presentation and reports.

### Phase 4: Actor Runtime Shell

- Introduce `BattleActorRuntime` and actor-local controller interfaces.
- Build actor runtimes from existing `BattleRuntimeActor` DTOs at session start or tick-start.
- Route actor phase advancement through actor runtime controllers while leaving target selection and movement proposal code in place where necessary.
- Add tests proving locked actors do not receive new actions, while unlocked actors independently consume the same tick snapshot.

### Phase 5: Basic Attack Actorization

- Move basic attack lifecycle into `BattleActionController`.
- Emit attack impact and `DamageApplied` at Runtime impact time.
- Keep same-tick deterministic damage application through commit barrier.
- Preserve existing Presentation playback by mapping new action/impact events to current observer behavior or by extending the event stream compatibly.

### Core Slice A: Basic Attack Commit Buffer Compatibility

The first actorization implementation slice combines the minimum useful parts of Phase 3, Phase 4, and Phase 5:

- introduce `BattleCommitBuffer` for basic-attack damage, attacker recovery, target HP, and defeat requests;
- introduce a thin `BattleActorRuntime` and `BattleActionController` for actor-local basic-attack proposal;
- keep target selection, skill/effect resolution, movement reservation, movement commits, and battle termination on their current authoritative paths;
- preserve current `DamageApplied`, `BattleGroupPlanStateChanged`, movement, report, settlement, and presentation contracts;
- preserve current same-tick semantics: basic attack requests read tick-start facts, damage events are emitted before movement commits, and actors defeated by same-tick damage cannot move in that tick.

This slice is intentionally not the final basic-attack lifecycle model. It keeps immediate impact timing for compatibility and creates the real request/commit boundary that later slices can extend into attack windup, impact, recovery, skill effects, health receivers, and movement requests.

### Core Slice B: Effect Damage And Health Commit Compatibility

The second actorization implementation slice migrates skill/effect damage and health response behind the commit boundary while preserving the current skill lifecycle:

- introduce actor-local effect receiving and health helpers used by `BattleActorRuntime`;
- route damage effects through `BattleCommitBuffer` for HP mutation, defeat marking, and `EffectApplied` / `DamageApplied` event creation;
- keep `SkillUsed`, `CommandAccepted`, `CommandRejected`, `CommandFailed`, Thunder Mark creation, Thunder Fold displacement, channel lifetime, channel tick cadence, and movement ordering on their current authoritative paths;
- preserve current skill/effect event contracts, including source command/action/definition ids, `EffectKind`, actor/target cells, `CorpsStrengthDelta`, and fatal `ReasonCode` values;
- preserve current ordering: `SkillUsed` before effect events, `EffectApplied` immediately before the matching `DamageApplied`, active channel ticks before pending command release, and effect damage before the same tick's tactical observation / basic attack / movement decisions.

This slice does not make the full ability lifecycle actor-local. It removes the current temporary direct HP/defeat writes from `BattleEffectResolver` and `BattleRuntimeHeroSkillCommandResolver` so later slices can move cast/channel ownership into `BattleAbilityController` without keeping a second damage authority alive.

### Core Slice C: Ability Controller Lifecycle Shell

The third actorization implementation slice introduces a caster-held ability controller for active skill execution while preserving the current command and effect contracts:

- introduce `BattleAbilityController` and expose it through `BattleActorRuntime`;
- keep hero skill command submission, validation, queue ownership, command acceptance/rejection events, and pending command removal in `BattleRuntimeHeroSkillCommandResolver`;
- move active skill action start, active cast impact checks, recovery completion, and actor `CurrentSkill*` lifecycle handling behind the caster's `BattleAbilityController`;
- keep `BattleEffectResolver.Apply` as the effect primitive executor, so damage, Thunder Mark creation, Thunder Fold displacement, report attribution, and Presentation event contracts remain unchanged;
- preserve current ordering: movement boundary before skill processing, active skill/channel advancement before pending command release, `SkillUsed` before effect events, and skill consumption before tactical observation/basic attack/movement decisions;
- keep active channel state in the current runtime field for this slice, but route channel advancement through the ability controller so the hero skill resolver no longer directly owns channel ticking.

This slice is intentionally a lifecycle shell, not the final full ability system. It makes the caster-local execution owner explicit without moving skill targeting, command validation, effect primitives, or spatial displacement authority.

### Core Slice D: Effect Delivery Request Boundary

The fourth actorization implementation slice moves channel area hits from direct effect execution into an explicit effect delivery request path while preserving current event contracts:

- introduce `BattleCommitBuffer` effect delivery requests that capture source context, target actor, and effect payload;
- keep `BattleChannelDamageResolver` as area hit discovery only: it may identify overlapping targets and enqueue delivery requests, but it must not execute damage, mutate target health, mark defeat, or own channel lifecycle;
- route delivery commit through the target actor's `BattleEffectReceiver`, which then requests health mutation through `BattleHealthComponent`;
- keep final HP/defeat writes and event id uniqueness in the commit buffer;
- preserve current ordering for this compatibility slice: active channel delivery resolves before pending command release, `EffectApplied` immediately precedes matching `DamageApplied`, and skill/channel events keep existing source command/action/definition attribution.

This slice does not introduce the full `BattleEffectField` or persistent timed buff/debuff runtime. It only establishes the request boundary needed for later effect-field actorization.

### Core Slice E: Actor-Local Pending Ability Orders

The fifth actorization implementation slice moves accepted but unstarted active-skill orders from the center hero-skill resolver into the caster's `BattleAbilityController` while preserving command and event contracts:

- keep `BattleRuntimeHeroSkillCommandResolver` as the command submission, validation, and event-result boundary;
- convert accepted commands into actor-local pending ability orders owned by the caster's `BattleAbilityController`;
- let the ability controller own same-caster pending order replacement, active-skill queue waiting, basic-attack recovery waiting, release readiness, and pending removal;
- preserve current accepted/rejected/interrupted/failed/`SkillUsed`/effect event order and source command/action/definition attribution;
- preserve tactical pause semantics: paused command submission may update pending ability intent, but no cast time, cooldown, damage, channel tick, HP mutation, or defeat advances until runtime resumes.

This slice does not introduce the full `BattleEffectField`, full cooldown/cost resource model, movement actorization, or the final basic-attack lifecycle model. The center resolver may still orchestrate tick order and validation helper calls, but it must not own pending skill queue storage or pending-order iteration/removal after this slice.

### Core Slice F: Basic Attack Lifecycle Completion

The sixth actorization implementation slice completes the basic-attack lifecycle that Core Slice A only wrapped in a compatibility commit boundary:

- let `BattleActionController` own basic attack start, locked target/action payload, windup advancement, impact readiness, recovery transition, and recovery completion;
- keep `BattleAttackResolver` as orchestration over decision contexts and active attack advancement, not as the owner of immediate damage timing or HP mutation;
- start valid basic attacks by entering `AttackWindup` and storing the actor-local locked attacker/target anchors and declared damage payload;
- emit `DamageApplied` only at the Runtime impact boundary, through `BattleCommitBuffer`, so pausing before impact freezes without HP loss or death;
- transition the attacker into `AttackRecovery` immediately after impact, then return to `AnchoredDecision` only after Runtime time reaches the recovery boundary;
- keep same-impact-tick deterministic batching: all attacks whose impact boundary is due in the current tick read stable locked payloads and commit damage in deterministic order;
- preserve existing `DamageApplied` event shape, report attribution, settlement inputs, and Presentation compatibility fields such as action duration and impact delay.

This slice does not move movement continuation, skill/effect primitive execution, tactical target selection, settlement/report contracts, or the final full effect-field model. It may update old attack-cadence compatibility tests that assumed adjacent attacks deal damage at time zero, because the accepted architecture now requires `AttackWindup -> impact -> recovery` as Runtime truth.

### Core Slice G: Movement Controller Shell

The seventh actorization implementation slice introduces the actor-local movement controller without changing square-grid movement gameplay:

- expose `BattleMovementController` through `BattleActorRuntime`;
- move moving-phase time-boundary advancement out of `BattleRuntimeTickResolver` and into the actor's movement controller;
- move same-intent movement continuation context construction behind the actor's movement controller while still using the existing topology, occupancy, target facts, tactical region facts, route hints, and reservation checks;
- move ended movement-chain cleanup behind the actor's movement controller so local steering and intent snapshot lifetime stay actor-local;
- keep `BattleMovementCommitResolver`, `BattleMovementReservationMap`, `BattleDynamicOccupancy`, `BattleNavigationGraph`, route topology, and final movement event emission as world/commit authority;
- preserve current behavior for reservation conflicts, defeated-before-move, target-defeated-before-move, command-change invalidation, objective stop, local enemy stop, and pause.

This slice is a shell and delegation boundary. It must not move map topology, occupancy, reservation ordering, plan-state event emission, target acquisition, local-combat selection, or movement event writes into the actor controller.

### Core Slice H1: Objective/Region Movement Proposal Boundary

The eighth actorization implementation slice moves already-resolved objective and region movement proposal construction behind the actor's movement controller while preserving current movement behavior:

- add `BattleMovementController.BuildMovementProposalContext(...)` as the actor-local entry for already-selected objective and region movement requests;
- keep `BattleRuntimeTickResolver` responsible for target selection, AI request construction, local-combat scope selection, stale-retarget callbacks, and final movement event ordering;
- route `AdvanceTowardObjective`, `AdvanceTowardRegion`, continuation `ReturnToObjective`, combat-zone outsider region advance, and pressure region advance through movement-controller entry points;
- keep `AdvanceTowardTarget`, `JoinLocalCombat`, `HoldSupport`, alternate combat-zone target fallback, and stale-retarget rebuilds on their current paths for this slice;
- keep `BattleMovementCommitResolver`, `BattleMovementReservationMap`, `BattleDynamicOccupancy`, `BattleNavigationGraph`, route topology, plan-state events, reservation ordering, and final movement event emission world/commit-owned;
- preserve current movement regressions for objective advance, region advance, local obstacle steering, reservation fallback, same-tick released footprint blocking, movement/attack ordering, pause, and event order.

This slice is not the full movement actorization. It only creates the request-to-proposal boundary for objective/region movement after the request has already been chosen by commander/AI logic.

### Core Slice H2: Target/Local-Combat Movement Proposal Boundary

The ninth actorization implementation slice moves already-resolved target and local-combat movement proposal construction behind the actor's movement controller while preserving current target choice, local-combat policy, and movement commit behavior:

- add a `BattleMovementController` entry point for already-selected `AdvanceTowardTarget`, `JoinLocalCombat`, and `HoldSupport` movement proposal construction;
- keep `BattleRuntimeTickResolver` responsible for target acquisition, candidate scoping, behavior-tree request choice, local-combat situation construction, alternate combat-zone target fallback, stale-retarget callbacks, and final tick/event ordering;
- keep continuation validation in `BattleMovementContinuationPlanner`: retained target identity, command match, immediate attack stop, local-combat scope selection, and local-combat situation matching remain there for this slice;
- route only the chosen target/local-combat request, target fact, scoped local-combat region, and already-built local-combat situation through the movement-controller proposal entry point;
- preserve existing move option ordering, support-slot preference, stored slot reuse, named local-combat failure reasons, pressure fallback trigger conditions, `AllowReservationFallback` behavior, and diagnostic emission;
- keep `BattleMovementCommitResolver`, `BattleMovementReservationMap`, `BattleDynamicOccupancy`, `BattleNavigationGraph`, route topology, plan-state events, reservation ordering, and final movement event emission world/commit-owned.

This slice is still not full movement actorization. It only moves proposal construction after target/local-combat intent has already been selected by commander/AI logic.

### Core Slice H3: Movement Context Helper Decoupling

The tenth actorization implementation slice removes the accidental reverse dependency from actor/local movement helpers back into the center tick resolver while preserving the current tick context shape:

- extract `BattleRuntimeTickContext` construction into a shared runtime helper used by the tick resolver, objective/region planners, combat-zone retargeting, and movement controller;
- extract orthogonal attack-gap calculation into a shared combat geometry helper used by movement, target selection, AI decision facts, and the tick resolver;
- remove `BattleMovementController` calls to `BattleRuntimeTickResolver.CreateContext(...)` and `BattleRuntimeTickResolver.GetOrthogonalAttackGap(...)`;
- keep `BattleRuntimeTickResolver` as the tick-order owner and keep behavior-tree request choice, target acquisition, diagnostics, movement commits, event stream writes, and final state mutation unchanged;
- preserve all existing `BattleRuntimeTickContext`, `BattleRuntimeActionProposal`, movement reason, local-combat situation, combat-slot intent, and failure-reason fields.

This slice is a structural decoupling only. It does not change movement choice, attack range rules, local-combat policy, reservation behavior, or event order.

### Core Slice H4: Movement Continuation Primitive Decoupling

The eleventh actorization implementation slice removes the redundant batch-wrapper loop between `BattleMovementController` and `BattleMovementContinuationPlanner` while preserving continuation validation ownership for this phase:

- keep `BattleRuntimeTickResolver` calling `BattleMovementController.BuildContinuationContexts(...)` as the public runtime movement continuation entry point;
- keep retained target identity, command match, immediate attack stop, local-combat scope selection, and local-combat situation matching inside `BattleMovementContinuationPlanner`;
- expose a single-actor continuation primitive from `BattleMovementContinuationPlanner` so `BattleMovementController.BuildContinuationContext(...)` does not call the planner's batch `BuildContinuationContexts(...)` with a one-item actor list;
- expose a single-actor cleanup primitive so `BattleMovementController.ClearEndedMovementChain()` does not call the planner's batch `ClearEndedMovementChains(...)` with a one-item actor list;
- preserve `AllowStaleTargetRetarget = false`, `AllowReservationFallback = false`, movement intent cleanup semantics, exhausted local-steering preservation, and current event order.

This slice is structural only. It does not move continuation validation into the movement controller yet and does not change movement proposal selection, movement commits, or event emission.

### Core Slice H5: Movement Continuation Batch Wrapper Retirement

The twelfth actorization implementation slice removes the now-unused batch continuation wrappers from `BattleMovementContinuationPlanner` so there is only one runtime batch entry for movement continuation:

- keep `BattleRuntimeTickResolver` calling `BattleMovementController.BuildContinuationContexts(...)` and `BattleMovementController.ClearEndedMovementChains(...)`;
- keep single-actor continuation validation in `BattleMovementContinuationPlanner.TryBuildContinuationContext(...)` for this phase;
- keep single-actor cleanup in `BattleMovementContinuationPlanner.ClearEndedMovementChain(...)` for this phase;
- remove `BattleMovementContinuationPlanner.BuildContinuationContexts(...)` and `BattleMovementContinuationPlanner.ClearEndedMovementChains(...)` so future code cannot choose between two batch authorities;
- preserve deterministic actor-id ordering, `AllowStaleTargetRetarget = false`, `AllowReservationFallback = false`, cleanup semantics, exhausted local-steering preservation, and current event order through the controller-owned batch entry.

This slice is structural only. It does not move continuation validation into the movement controller yet and does not change movement proposal selection, movement commits, topology, occupancy, reservation behavior, or event emission.

### Core Slice H6: Movement Cleanup Ownership

The thirteenth actorization implementation slice moves ended movement-chain cleanup from the continuation planner into `BattleMovementController`:

- keep `BattleRuntimeTickResolver` calling `BattleMovementController.ClearEndedMovementChains(...)`;
- make `BattleMovementController.ClearEndedMovementChain()` own actor-local movement intent cleanup directly;
- remove `BattleMovementContinuationPlanner.ClearEndedMovementChain(...)` so planner no longer owns actor movement-state cleanup;
- preserve the cleanup guard that does not clear a still-moving actor with an active movement target;
- preserve exhausted local-steering behavior: `FollowObstacle` with `MovementSteeringBudgetRemaining <= 0` keeps steering memory, all other cleanup clears it;
- keep continuation context validation in `BattleMovementContinuationPlanner.TryBuildContinuationContext(...)` for this slice.

This slice is structural only. It does not change movement proposal selection, movement continuation validation, movement commits, topology, occupancy, reservation behavior, or event emission.

### Core Slice H7: Runtime Identity And Command Rule Decoupling

The fourteenth actorization implementation slice removes faction and command helper ownership from `BattleRuntimeTickResolver`:

- introduce a shared Runtime helper for faction normalization, same-faction checks, player-faction checks, and initial corps command normalization;
- route `BattleRuntimeTickResolver`, target selection, AI request building, movement continuation, local-combat region helpers, tactical observation, ability validation, channel hit scanning, and tactical builders through the shared helper instead of `BattleRuntimeTickResolver` static methods;
- remove `SameFaction(...)`, `NormalizeFaction(...)`, `IsFocusFireCommand(...)`, `IsHoldLineCommand(...)`, `NormalizeCorpsCommandId(...)`, and the command constants from `BattleRuntimeTickResolver`;
- keep `BattleRuntimeTickResolver` as tick-order owner only; it must not become a general identity/command utility dependency for actor-local or tactical services;
- preserve command string compatibility for `FocusFire`, `HoldLine`, and default `Assault`, including case-insensitive input normalization and the existing empty-faction default to `player`;
- preserve target acquisition, behavior-tree policy choice, ability target validation, movement proposal selection, channel hit discovery, diagnostics, event order, pause behavior, and state mutation semantics.

This slice is structural only. It does not change command rules, faction rules, AI policy, movement, damage, skill effects, topology, occupancy, reservations, commits, or emitted event contracts.

### Core Slice H8: Decision Outcome Applier Boundary

The fifteenth actorization implementation slice removes actor decision outcome application from `BattleRuntimeTickResolver`:

- introduce `BattleDecisionOutcomeApplier` as the service that applies already-built actor decision contexts to actor-local outcome state;
- keep target selection, AI request construction, movement proposal selection, movement commits, attack resolution, effect execution, ability lifecycle, damage, health, defeat, displacement, topology, occupancy, reservations, and event commit authority on their existing owners;
- keep `BattleRuntimeTickResolver` calling a single decision-outcome service entry point after contexts are built and before continuation / attack / movement commit phases;
- move the advance-failure callback type to a neutral Runtime boundary file so AI request construction does not own or expose failure-state mutation;
- preserve `Hold`, invalid-target, target-lock plan state, objective plan state, proposal-failure, wait-for-attack-charge, and unsupported-action semantics;
- preserve event ordering, diagnostics, pause behavior, movement behavior, skill behavior, attack behavior, report attribution, and settlement contracts.

This slice is structural only. It does not change AI policy, target selection, movement, damage, skill effects, topology, occupancy, reservations, commits, or emitted event contracts.

### Core Slice H9: Decision Context Builder Boundary

The sixteenth actorization implementation slice removes per-actor decision context construction from `BattleRuntimeTickResolver`:

- introduce `BattleRuntimeDecisionContextBuilder` as the service that builds `BattleRuntimeTickContext` from tick-start actor facts, scoped tactical facts, AI requests, local-combat situations, and actor-local movement proposal builders;
- keep `BattleRuntimeTickResolver` as the tick-order caller that filters decision-ready actors and passes world inputs to a single decision-context service entry point;
- route stale-target advance retargeting through the same decision-context builder so retarget refresh does not keep a second hidden `BuildTickContext(...)` path in the resolver;
- keep movement commits, attack resolution, effect execution, ability lifecycle, damage, health, defeat, displacement, topology mutation, occupancy mutation, reservations, commit-buffer phases, and event-stream writes on their existing owners;
- preserve target-selection policy, command-scoped AI request semantics, local-combat scope selection, alternate combat-zone join fallback, pressure advance fallback, diagnostics, movement behavior, attack behavior, skill behavior, event ordering, pause behavior, report attribution, and settlement contracts.

This slice is structural only. It does not change AI policy, target scoring, movement proposal semantics, movement commits, damage, skill effects, topology, occupancy, reservations, or emitted event contracts.

### Core Slice H10: Attack Engagement Coordinator Boundary

The seventeenth actorization implementation slice removes post-attack engagement coordination from `BattleRuntimeTickResolver`:

- introduce `BattleAttackEngagementCoordinator` as the service that wraps basic-attack resolution and the follow-up engagement-state trigger pass;
- keep `BattleRuntimeTickResolver` as the tick-order caller that invokes the coordinator after decision outcome application and movement-continuation context construction, and before movement commit;
- keep `BattleAttackResolver` responsible for basic-attack proposal/impact orchestration and commit-buffer damage application;
- keep `BattleTacticalObservationUpdater` responsible for post-attack engagement-state mutation;
- pass only the `DamageApplied` events emitted by the current attack-resolution slice into post-attack engagement triggers, so earlier same-tick skill/channel damage is not replayed as post-attack input;
- remove the resolver-owned attack engagement partial so the resolver no longer reads event-stream internals or directly calls the basic-attack resolver / post-attack engagement updater.

This slice is structural only. It does not change basic-attack timing, damage, HP/defeat mutation, engagement-state rules, movement commits, skill effects, event ordering, pause behavior, report attribution, or settlement contracts.

### Core Slice H11: Advance Failure State Boundary

The eighteenth actorization implementation slice removes advance-failure counter ownership from `BattleRuntimeTickResolver`:

- make `BattleAdvanceFailureStateBoundary` own `RecordAdvanceFailure(...)` and `ResetAdvanceFailureState(...)` in addition to the callback delegate type;
- route decision-outcome application, movement commit failure handling, basic-attack start cleanup, and stale-target retarget cleanup through `BattleAdvanceFailureStateBoundary`;
- keep `BattleRuntimeTickResolver` as the tick-order caller only; it must not define or be referenced as a general advance-failure utility owner;
- preserve existing failure reasons, consecutive failure counter increments, last failure reason fallback to `advance_failed`, reset behavior on successful movement/attack/retarget, diagnostics, movement behavior, event ordering, pause behavior, report attribution, and settlement contracts.

This slice is structural only. It does not change movement proposal selection, reservation ordering, stale-target retarget policy, attack timing, damage, skill effects, topology, occupancy, or emitted event contracts.

### Core Slice H12: Stale Advance Retargeting Boundary

The nineteenth actorization implementation slice removes stale-target advance retargeting from `BattleRuntimeTickResolver`:

- introduce `BattleStaleAdvanceRetargeting` as the service that refreshes a movement context when its already-selected target dies before movement commit;
- keep `BattleRuntimeTickResolver` as the tick-order caller only; it may pass the Runtime AI executor into the retargeting service but must not define retarget policy or context-refresh methods;
- keep `BattleMovementCommitResolver` detecting stale target death at the movement-commit boundary and invoking the narrow retarget callback;
- fail fast when the retargeting service is created without an AI executor, so stale-target recovery does not hide a late fallback failure;
- preserve the same retarget eligibility: `AdvanceTowardTarget`, `JoinLocalCombat`, and `HoldSupport` only, living actor only, refreshed live target required, refreshed request still retargetable, refreshed proposal must have a move and no failure reason;
- preserve context mutation order, target lock update, advance-failure reset, diagnostics, movement behavior, reservation ordering, event ordering, pause behavior, report attribution, and settlement contracts.

This slice is structural only. It does not change target selection policy, retarget policy, movement proposal selection, movement commits, topology, occupancy, reservations, attack timing, damage, skill effects, or emitted event contracts.

### Core Slice H13: Movement Commit Apply Boundary

The twentieth actorization implementation slice removes successful movement-commit application from `BattleMovementCommitResolver`:

- introduce `BattleMovementCommitBoundary` as the service that applies an accepted movement commit after reservation has already succeeded;
- keep `BattleMovementCommitResolver` responsible for movement candidate filtering, reservation ordering, reservation rejection, stale-target detection, retarget callback invocation, movement-failure diagnostics, and returning movement-event counts;
- move accepted-move actor reservation fields, plan-state transition emission, combat-slot intent logging, `MarkMovementCommitted(...)`, advance-failure reset, success result assignment, `MovementStarted` event creation, and movement performance event recording behind the new boundary;
- keep topology, dynamic occupancy snapshots, same-tick reservation maps, move candidate ordering, reservation fallback, and reservation rejection outside the new boundary;
- preserve movement-start event payloads, plan-state log/event semantics, target id resolution, movement reason fallback order, action timing, pause behavior, report attribution, and settlement contracts.

This slice is structural only. It does not change movement proposal selection, stale-retarget policy, reservation ordering, topology, occupancy, movement timing, attack timing, damage, skill effects, or emitted event contracts.

### Core Slice H14: Basic Attack Health Commit Boundary

The twenty-first actorization implementation slice routes basic-attack HP and defeat mutation through the actor-local health component:

- keep `BattleCommitBuffer` as the deterministic basic-attack commit coordinator that batches same-tick attack applications from tick-start facts;
- add a basic-attack health commit path to `BattleHealthComponent`, so target HP writes and defeated-state marking live with the target actor's health component instead of direct `BattleCommitBuffer` field mutation;
- preserve current basic-attack batch semantics: requests read tick-start HP, multiple attacks against the same target accumulate deterministically, `DamageApplied` events are emitted before same-tick movement, and defeated actors cannot move after same-tick damage;
- keep basic-attack event creation, plan-state defeated event emission, basic attack damage normalization, attack lifecycle, target selection, movement commits, skill/effect damage, channel timing, report attribution, pause behavior, and settlement contracts unchanged.

This slice is structural only. It does not change attack timing, damage amount, defeated event semantics, event ordering, movement ordering, skill/effect damage behavior, or emitted event contracts.

### Core Slice H15: Movement Continuation Ownership

The twenty-second actorization implementation slice moves movement-chain continuation policy behind the actor-local movement controller:

- keep `BattleMovementController.BuildContinuationContexts(...)` as the batch adapter that gathers completed movement actors and supplies world inputs;
- move continuation eligibility checks such as command match, retained-target validity, local-combat scope match, objective/region stop, and immediate-attack stop into `BattleMovementController`;
- keep topology, dynamic occupancy, tactical store, group action zones, combat zones, and reservation/commit authority as explicit world inputs, not hidden controller-owned global state;
- retire or narrow `BattleMovementContinuationPlanner` so it no longer owns movement-chain validity policy;
- preserve existing continuation behavior, movement-stop reasons, movement-start/completed event ordering, stale-retarget behavior, pause behavior, report attribution, and settlement contracts.

This slice is structural only. It does not change navigation scoring, local steering, topology legality, occupancy, reservations, movement timing, attack timing, skill/effect behavior, or emitted event contracts.

### Core Slice H16: Active Channel Ownership

The twenty-third actorization implementation slice moves active channeled-skill state from global runtime state to the caster actor's ability runtime:

- store active channel instances with the caster actor instead of `BattleRuntimeState`;
- tick channel lifetime and cadence through the caster's `BattleAbilityController`;
- keep channel area hit discovery in `BattleChannelDamageResolver` and effect delivery/health mutation through the existing commit-buffer path;
- keep `BattleEffectResolver` responsible only for starting an effect primitive, not for owning a global channel list;
- preserve current channel start tick behavior, ordinary cadence ticks, same-tick multi-channel event-id uniqueness, opposing-channel same-tick damage commit, pause behavior, report attribution, and settlement contracts.

This slice is structural only. It does not change channel damage amount, area shape, tick interval semantics, skill command validation, teleport/mark behavior, event contracts, or final HP/defeat ordering.

### Core Slice H17: Displacement Commit Boundary

The twenty-fourth actorization implementation slice moves non-neighbor spatial displacement commit authority out of skill/effect primitive code and into a shared Runtime displacement boundary:

- introduce `BattleDisplacementCommitBoundary` as the owner of Thunder Mark Fold release-time validation, final anchor mutation, stale movement/target context clearing, teleport event creation, and low-noise displacement logging;
- route command-time Thunder Mark Fold destination validation through the same boundary validation helper so command acceptance and release use the same mark/radius/topology/occupancy rules;
- keep `BattleEffectResolver` responsible for dispatching the teleport effect primitive only; it must not build occupancy snapshots, call actor displacement mutation directly, create teleport events, or own displacement diagnostics;
- keep `BattleRuntimeActorStateMachine.CommitDisplacement(...)` as the low-level actor state mutation primitive called only by the displacement boundary;
- preserve current Thunder Mark Fold behavior, selected-mark semantics, occupied-destination rejection, stale movement context cleanup, active-channel continuity after fold, event payloads, pause behavior, report attribution, and settlement contracts.

This slice is structural only. It does not change mark lifetime, teleport radius, destination legality, displacement timing, skill command ordering, channel behavior, movement topology, occupancy semantics, or emitted event contracts.

### Core Slice H18: Ability Effect Release Boundary

The twenty-fifth actorization implementation slice moves skill-effect payload dispatch out of the actor ability controller and into a dedicated release boundary:

- introduce `BattleAbilityEffectReleaseBoundary` as the owner of converting a released skill action into effect execution contexts and payload dispatch calls;
- keep `BattleAbilityController` responsible for actor-local pending order selection, cast start, impact timing, recovery, channel lifecycle timing, and action-lock decisions;
- keep `BattleEffectResolver` responsible for effect primitive execution, including damage, mark creation, displacement boundary dispatch, and channel start primitive dispatch;
- keep channel-start batching semantics from H16: direct skill damage commits immediately, while `StartChanneledAreaDamage` start-tick deliveries may defer to the shared start-phase commit buffer;
- preserve `SkillUsed`, `EffectApplied`, `DamageApplied`, Thunder Mark, Thunder Fold, channel start, channel cadence, event-id uniqueness, pause behavior, report attribution, and settlement contracts.

This slice is structural only. It does not change skill targeting, command validation, cast timing, recovery timing, damage amount, channel cadence, effect event ordering, displacement behavior, or emitted event contracts.

### Core Slice H19: Ability Tick Coordinator Boundary

The twenty-sixth actorization implementation slice narrows the hero skill command resolver back to command submission and validation by moving runtime ability ticking into a dedicated coordinator:

- introduce `BattleAbilityTickCoordinator` as the Runtime tick entry for active ability advancement, active channel cadence, and actor-local pending ability order resolution;
- keep `BattleRuntimeHeroSkillCommandResolver` responsible for command submission, command validation, command acceptance/rejection events, command payload locking, and command-level helper rules;
- keep `BattleAbilityController` responsible for caster-local lifecycle primitives and actor-owned pending/channel state;
- keep `BattleRuntimeTickResolver` responsible only for ordering the ability tick phase relative to movement boundaries, tactical observation, attack, and movement commit;
- preserve pause behavior, movement-boundary waiting, channel-start batching, channel cadence batching, pending-order actor action consumption, skill events, report attribution, and settlement contracts.

This slice is structural only. It does not change command validation, skill targeting, actor action locks, channel timing, damage timing, effect ordering, movement ordering, or emitted event contracts.

### Core Slice H20: Runtime Action Phase Coordinator

The twenty-seventh actorization implementation slice moves the first tick action phase out of `BattleRuntimeTickResolver` into a focused coordinator:

- introduce `BattleRuntimeActionPhaseCoordinator` as the owner of movement-boundary advancement, movement-completed event emission, attack recovery boundary advancement, and ability tick entry for the current runtime tick;
- return the action-phase facts needed by later phases: dynamic occupancy, movement-completed actor ids, and skill-consumed actor ids;
- keep `BattleRuntimeTickResolver` responsible for high-level phase ordering only: action phase, tactical observation, decision context construction, decision outcome application, continuation, attack engagement, movement commit, movement cleanup, and diagnostics;
- preserve existing ordering: movement boundaries before attack recovery, attack recovery before ability ticking, ability ticking before tactical observation, and skill-consumed actors excluded from same-tick decisions and movement continuation;
- preserve pause behavior, movement-completed event payloads, attack recovery timing, ability command waiting after movement boundary, channel batching, event ordering, report attribution, and settlement contracts.

This slice is structural only. It does not change movement progress, action readiness, skill timing, channel timing, target selection, attack resolution, movement commit, or emitted event contracts.

### Core Slice H21: Runtime Decision Phase Coordinator

The twenty-eighth actorization implementation slice moves tick decision-phase construction out of `BattleRuntimeTickResolver` into a focused coordinator:

- introduce `BattleRuntimeDecisionPhaseCoordinator` as the owner of tick-start tactical observation refresh, living-corps projection, decision-ready actor filtering, actor decision context construction, decision outcome application, and movement-continuation context construction;
- return the decision-phase facts needed by later phases: tick-start facts, ordered contexts, decision-ready actor count, and whether living actors remain;
- keep `BattleRuntimeTickResolver` responsible for high-level phase ordering only: action phase, decision phase, attack engagement, movement commit, movement cleanup, and diagnostics;
- preserve existing ordering: action phase before tactical observation, tactical observation before decision contexts, decision outcome application before continuation contexts, and continuation contexts before attack engagement / movement commit;
- preserve movement-completed and skill-consumed filtering so actors that just completed movement or consumed/waited for a skill do not also take an ordinary decision or movement continuation in the same tick.

This slice is structural only. It does not change tactical observation rules, target selection, AI request construction, decision outcomes, movement continuation policy, attack resolution, movement commit, or emitted event contracts.

### Core Slice H22: Runtime Resolution Phase Coordinator

The twenty-ninth actorization implementation slice moves post-decision resolution out of `BattleRuntimeTickResolver` into a focused coordinator:

- introduce `BattleRuntimeResolutionPhaseCoordinator` as the owner of post-decision attack engagement entry, movement commit entry, movement-chain cleanup entry, and the movement/no-move performance counters for the current runtime tick;
- keep `BattleAttackEngagementCoordinator`, `BattleMovementCommitResolver`, `BattleMovementCommitBoundary`, `BattleStaleAdvanceRetargeting`, and `BattleMovementController` as the existing narrow owners below this phase boundary;
- return the ordered contexts needed by later diagnostics so action-result logging can remain unchanged in this slice;
- keep `BattleRuntimeTickResolver` responsible for high-level phase ordering only: action phase, decision phase, resolution phase, and diagnostics;
- preserve existing ordering: decision contexts before attack engagement, attack engagement before movement commit, movement commit before movement-chain cleanup, and movement cleanup before action-result diagnostics.

This slice is structural only. It does not change attack timing, damage ordering, movement reservation policy, stale-target retargeting, movement event payloads, movement cleanup policy, or emitted event contracts.

### Core Slice H23: Runtime Action Diagnostics Boundary

The thirtieth actorization implementation slice moves action-result diagnostics out of `BattleRuntimeTickResolver`:

- introduce a focused diagnostics boundary that owns per-context unresolved-action fallback assignment and low-noise runtime action result logging;
- keep `BattleRuntimeTickResolver` responsible for high-level phase ordering only: action phase, decision phase, resolution phase, and diagnostics boundary entry;
- remove stale helper leftovers from `BattleRuntimeTickResolver` that are no longer part of runtime tick orchestration;
- preserve existing diagnostic category, log text shape, unresolved-action fallback semantics, attack-charge wait suppression, and action-result ordering.

This slice is structural only. It does not change actor decisions, attack or movement resolution, combat facts, event stream contents, reports, settlement, or presentation playback.

### Scope Closure After H23

Core Slice H23 closes the planned structural decomposition for this implementation proposal. The remaining work is final acceptance verification, known unrelated regression-guard cleanup outside this battle-runtime scope, and requested manual QA evidence. Do not create additional H-numbered slices unless final verification exposes a concrete violation of the acceptance criteria below.

The Phase 6-9 sections below remain as original scope markers. Their goals have been covered by Core Slices C-H23 through the ability controller, effect receiver and health components, movement controller, deterministic commit boundaries, runtime phase coordinators, and diagnostics boundary.

### Phase 6: Skill Actorization

- Move active skill lifecycle into `BattleAbilityController`.
- Convert pending hero skill commands into actor-local pending ability orders.
- Move cast, impact, recovery, channel lifetime, and channel tick cadence into caster-owned ability runtime.
- Preserve accepted command, rejected command, interrupted command, failed command, `SkillUsed`, damage, mark, teleport, and channel events.

### Phase 7: Effect Receiver Actorization

- Introduce effect application requests and actor-local effect receivers.
- Convert channeled area damage from center "scan and direct HP write" to effect delivery plus target receiver validation.
- Move HP/death request creation into `BattleHealthComponent`; keep final HP/defeat writes in commit barrier.
- Add tests for simultaneous area damage and basic attack damage on the same target.

### Phase 8: Movement Actorization

- Move movement continuation and local steering consumption into `BattleMovementController`.
- Keep topology, occupancy, route hints, and reservation service world-owned.
- Actor controllers propose movement requests; commit barrier accepts/rejects and emits movement events.
- Add tests for reservation conflicts, defeated-before-move, target-defeated-before-move, and command-change invalidation.

### Phase 9: Center Resolver Retirement

- Shrink `BattleRuntimeTickResolver` into orchestration only:
  - build tick snapshot;
  - tick actors;
  - tick world effects;
  - commit buffer;
  - emit diagnostics.
- Retire or rename center resolvers that no longer own behavior.
- Keep small services for pure queries, scoring, validation, and deterministic commit phases.

## Tests

Add or update automated tests for:

- Runtime pause stops actor phase time, skill cast time, channel ticks, movement progress, cooldowns, and impact boundaries.
- Pause during attack windup does not emit `DamageApplied` until Runtime resumes and reaches impact time.
- Pause during movement preserves runtime progress and resumes from the same progress.
- Pause during skill channel prevents extra damage ticks until resume.
- Presentation pause freezes movement interpolation, attack animation, skill VFX, damage feedback, health-bar timing, and death presentation.
- UI command preview and command submission still work while battle is paused.
- Commands submitted during pause update intent or pending command state without applying combat effects.
- Multiple actors can independently tick from the same tick snapshot without center resolver deciding their whole action lifecycle.
- Commit barrier applies simultaneous damage deterministically.
- Commit barrier rejects movement reservation conflicts deterministically.
- Effect receiver handles overlapping area effects and actor footprint overlap correctly.
- Presentation still receives enough semantic events for existing playback and battle reports.

Recommended verification commands:

```powershell
dotnet run --project tests\BattleHitFeedbackRegression\BattleHitFeedbackRegression.csproj -v:minimal
dotnet build rpg.csproj -maxcpucount:2 -v:minimal
git diff --check
```

Add new focused Runtime test projects or expand existing battle regression tests as implementation requires. Use low concurrency for full builds.

## Diagnostics

Add low-noise diagnostics for:

- battle pause/resume with runtime time and presentation time;
- command accepted during pause;
- actor action phase transitions;
- ability cast start, impact, recovery, complete, fail, and interrupt;
- channel start, tick, expire, and cancellation;
- effect delivery accepted/rejected by target receiver;
- commit barrier movement conflict resolution;
- commit barrier same-tick damage merge and defeat application.

Do not log every frame, every visual interpolation update, every shader update, or every particle update.

## Manual QA

Desktop Mono QA must confirm:

1. Pause during unit movement freezes the unit at the current visual position.
2. Resume continues movement from the same visual position.
3. Pause during attack windup freezes the attack animation and does not show hit, HP loss, or death until resume reaches impact.
4. Pause after impact but before death presentation freezes the death presentation state and resumes without duplicate damage.
5. Pause during a skill cast freezes caster animation and skill VFX.
6. Pause during a channeled area skill freezes VFX and prevents extra visible damage ticks.
7. While paused, unit selection, skill targeting indicators, range preview, and command UI continue to respond.
8. A command issued during pause is accepted/rejected visually, but combat execution starts only after resume.
9. Battle result, report, and settlement remain consistent after pause/resume around movement, attack, skill, and death boundaries.

## Acceptance

This implementation proposal is accepted when:

- center Runtime orchestration no longer owns per-actor action lifecycle, skill lifecycle, effect receiving, or health/death response;
- actor-local controllers consume Runtime time and write requests into a commit buffer;
- deterministic commit phases own final state writes and event order;
- commands modify intent or pending orders rather than directly applying combat facts;
- Runtime pause can happen at any time without advancing actor actions, skills, effects, movement, cooldowns, damage, or death;
- Presentation pause can happen at any time without battle animations, movement interpolation, VFX, hit feedback, health bars, or death presentation continuing under wall-clock time;
- UI input and targeting preview continue during pause;
- existing battle reports, settlement, and Presentation event consumption remain compatible or receive documented event-contract updates;
- automated tests and manual QA evidence are recorded below.

## Verification Evidence

- 2026-06-21: Phase 1 Runtime Clock RED evidence: `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal` failed before implementation because `BattleRuntimeSessionController.SetPaused` did not exist. A later review-fix RED run failed `runtime pause blocks advance next tick termination`, proving paused `AdvanceNextTick` could still complete battle termination.
- 2026-06-21: Phase 1 Runtime Clock implementation added `BattleRuntimeClock`, routed `BattleRuntimeSessionController` and headless autonomous combat time through it, and exposed runtime pause state. Paused advance now returns frozen results before pre-tick termination, max-tick completion, tick resolution, time advancement, or combat mutation. Commands can still be accepted/rejected while paused at frozen runtime time.
- 2026-06-21: Phase 1 Runtime Clock review fix added RED evidence for paused `AdvanceToCompletion`: the new regression timed out before the fix because paused `AdvanceNextTick` no longer progressed. `AdvanceToCompletion` now returns the current incomplete result while paused and completes only after resume.
- 2026-06-21: Phase 1 Presentation Clock RED evidence: `dotnet run --project tests\BattleHitFeedbackRegression\BattleHitFeedbackRegression.csproj -v:minimal` failed before `BattlePresentationClockWaiter` existed. A follow-up RED run proved the helper needed to re-sample pause after each timer step before decrementing remaining battle-presentation time.
- 2026-06-21: Phase 1 Presentation Clock implementation added `BattlePresentationClockWaiter` and routed `WorldSiteRoot` battle presentation waits through it. The helper keeps `processAlways: true` for UI responsiveness, decrements remaining battle-presentation time only when unpaused before and after the step, and preserves the existing runtime-advance gate before `AdvanceFixedTick`.
- 2026-06-21: UI pause intent coverage confirmed through existing `battle runtime pause target click submits intent without advancing runtime` regression: paused skill target clicks submit `RuntimeController.SubmitCommand(...)` and do not call `AdvanceFixedTick` or unpause battle.
- 2026-06-21: Verification passed: `dotnet run --project tests\BattleHitFeedbackRegression\BattleHitFeedbackRegression.csproj -v:minimal`; `dotnet build rpg.csproj -maxcpucount:2 -v:minimal`; `git diff --check` with only pre-existing CRLF warnings in `tests/BattleHitFeedbackRegression/BattleHitFeedbackRegressionCases.BattleResult.cs` and `tests/BattleHitFeedbackRegression/BattleHitFeedbackRegressionCases.WorldText.cs`.
- 2026-06-21: Verification partially blocked by pre-existing unrelated regression guards: `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal` passes the three new runtime pause tests but exits 1 on oversized file guard for `tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegressionCases.PresentationAntiRot.cs:1123`; `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal` passes the pause target-click regression but exits 1 on existing `semantic marker authoring uses business subclasses` and `legacy manual battle authority docs stay deleted` guards.
- 2026-06-21: Core Slice A RED evidence: `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal` produced PASS for the two compatibility tests and FAIL for the two source guards because `BattleCommitBuffer`, `BattleActorRuntime`, and `BattleActionController` were not yet authored and `BattleAttackResolver` still wrote `BattleEventStream` directly.
- 2026-06-21: Core Slice A implementation added `BattleActorRuntime`, `BattleActionController`, and `BattleCommitBuffer`. Basic attacks now route through actor-local proposal and a deterministic basic-attack commit barrier that emits existing `DamageApplied` events, applies attacker recovery, writes target HP, and marks defeat in the previous event order. The slice intentionally leaves skill/effect, health receiver, and movement actorization for later phases.
- 2026-06-21: Sub-agent reviews completed for Core Slice A. Spec review found no Critical or Important issues. Code-quality review found only Minor notes; the review fixes added actor/context mismatch diagnostics, explicit commit-boundary null argument failures, commit-buffer-side attack-damage normalization, and clearer source-guard wording.
- 2026-06-21: Core Slice A verification: `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal` passes the four new Core Slice A tests, attack cadence/recovery tests, event-order goldens, pause tests, and report/settlement source-event tests, but exits 1 on the pre-existing unrelated oversized guard `tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegressionCases.PresentationAntiRot.cs:1123`.
- 2026-06-21: Core Slice A verification passed: `dotnet run --project tests\BattleHitFeedbackRegression\BattleHitFeedbackRegression.csproj -v:minimal`; `dotnet build rpg.csproj -maxcpucount:2 -v:minimal` with 0 warnings and 0 errors; `git diff --check` with only pre-existing CRLF warnings in `tests/BattleHitFeedbackRegression/BattleHitFeedbackRegressionCases.BattleResult.cs` and `tests/BattleHitFeedbackRegression/BattleHitFeedbackRegressionCases.WorldText.cs`.
- 2026-06-21: Core Slice B RED evidence added effect damage commit-boundary guards and compatibility cases. The first RED run failed because `BattleEffectReceiver` / `BattleHealthComponent` were missing, `BattleEffectResolver` directly wrote target HP / defeated state, and `BattleRuntimeHeroSkillCommandResolver` still tail-patched defeated targets. Follow-up RED coverage caught repeated damage effects reporting duplicate defeated transitions and duplicate effect event ids.
- 2026-06-21: Core Slice B implementation added actor-held `BattleEffectReceiver` and `BattleHealthComponent`, exposed them through `BattleActorRuntime`, routed damage effects through `BattleCommitBuffer`, removed direct HP/defeat writes from `BattleEffectResolver`, and removed the hero-skill defeated tail patch. Effect damage now keeps `EffectApplied` -> `DamageApplied` compatibility, preserves source command/action/definition/effect attribution, floors negative effect damage to zero instead of basic-attack minimum damage, reports defeated only on the HP transition to zero, and gives repeated same-base effect events deterministic duplicate suffixes while preserving the original base id for the first event.
- 2026-06-21: Core Slice B sub-agent reviews completed. Spec review found no compliance findings. Code-quality review found no remaining Critical or Important issues after the defeated-transition and duplicate-event-id fixes; the full outer tick commit barrier remains an intentional later-slice item because Core Slice B preserves immediate effect-event ordering.
- 2026-06-21: Core Slice B verification: `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal` passes the Core Slice B receiver/health/source guards, hero-skill damage order and attribution, lethal clamp/report attribution, zero-floor damage, multi-damage defeated-once, duplicate-id coverage, channel cadence, pause, skill, attack, movement, report, and settlement regressions, but exits 1 on the pre-existing unrelated oversized guard `tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegressionCases.PresentationAntiRot.cs:1123`.
- 2026-06-21: Core Slice B verification passed: `dotnet run --project tests\BattleHitFeedbackRegression\BattleHitFeedbackRegression.csproj -v:minimal`; `dotnet build rpg.csproj -maxcpucount:2 -v:minimal` with 0 warnings and 0 errors; `git diff --check` with only pre-existing CRLF warnings in `tests/BattleHitFeedbackRegression/BattleHitFeedbackRegressionCases.BattleResult.cs` and `tests/BattleHitFeedbackRegression/BattleHitFeedbackRegressionCases.WorldText.cs`.
- 2026-06-21: Core Slice C RED evidence added `BattleAbilityController` source guards and delayed targeted-cell payload coverage. The first RED source guard failed because `BattleAbilityController` / actor exposure did not exist and the hero skill resolver still owned active skill lifecycle. Follow-up RED coverage failed while `BattleEffectResolver.AdvanceActiveChannels` still owned channel ticking, and `runtime delayed cell skill preserves locked target payload` failed because delayed impacts lost the accepted target cell payload after the pending command was removed.
- 2026-06-21: Core Slice C implementation added actor-held `BattleAbilityController`, exposed it through `BattleActorRuntime`, moved active skill start/impact/recovery and `CurrentSkill*` lifecycle mutation out of `BattleRuntimeHeroSkillCommandResolver`, removed the stale resolver-side `SkillUsed` helper partial, and moved active channel ticking/expiry/target scanning into the ability controller while keeping `BattleEffectResolver.Apply` as the effect primitive executor. Active skill state now stores locked target-grid and selected-mark payload so delayed impacts, pause/resume, and recovery do not depend on removed pending commands.
- 2026-06-21: Core Slice C sub-agent reviews completed. Spec review identified stale resolver `SkillUsed` duplication and channel ticking still living in `BattleEffectResolver`; code-quality review identified delayed non-actor targeting payload loss. Fixes removed the duplicate partial, expanded resolver partial source guards, moved active channel ticking into `BattleAbilityController`, and added locked payload state plus the delayed cell mark regression. Remaining minor helper duplication between resolver validation and ability execution is accepted for this lifecycle-shell slice and should be cleaned during the fuller ability-order migration.
- 2026-06-21: Core Slice C verification: `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal` passes the Core Slice C source guards, delayed targeted-cell payload regression, hero-skill regressions, Thunder Mark / Thunder Spiral regressions, channel cadence, pause, attack, movement, report, and settlement regressions, but exits 1 on the pre-existing unrelated oversized guard `tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegressionCases.PresentationAntiRot.cs:1123`.
- 2026-06-21: Core Slice C verification passed: `dotnet run --project tests\BattleHitFeedbackRegression\BattleHitFeedbackRegression.csproj -v:minimal`; `dotnet build rpg.csproj -maxcpucount:2 -v:minimal` with 0 warnings and 0 errors.
- 2026-06-21: Core Slice C post-review RED evidence added same-tick double-channel event-id coverage after code-quality review found ordinary active channel ticks were not sharing a commit buffer. The RED run failed `runtime same tick channel damage effect ids stay unique` because two same-caster channels hitting the same target in one runtime tick emitted duplicate `EffectApplied` ids.
- 2026-06-21: Core Slice C post-review fix routes channel hit scans through `BattleChannelDamageResolver`, keeps `BattleAbilityController` as active channel lifecycle owner, removes the `BattleEffectResolver` -> `BattleAbilityController` callback, and shares one `BattleCommitBuffer` per ordinary active channel advancement tick so repeated `EffectApplied` / `DamageApplied` ids receive deterministic suffixes. The focused verification now passes the new same-tick double-channel regression and ability/effect boundary guards, with the suite still exiting 1 only on the pre-existing unrelated oversized guard.
- 2026-06-21: Core Slice C post-review follow-up RED evidence added opposing same-tick channel coverage after code-quality review found ordinary channel ticks still committed damage during each hit scan. The RED run failed `runtime opposing same tick channels both resolve before defeat commit` because the first lethal channel defeated the opposing channel caster before that caster's same-tick channel resolved.
- 2026-06-21: Core Slice C post-review follow-up implementation added deferred effect-damage commit support for Runtime-internal effect execution contexts. Ordinary active channel advancement now gathers all due channel damage requests into the shared tick buffer, then commits once after the due-channel scan; channel-start immediate damage keeps the existing immediate event behavior. Source guards now keep `BattleChannelDamageResolver` free of channel lifecycle ownership, direct HP/defeat writes, Godot API, and wall-clock waits. Focused verification passes both same-tick channel id and opposing-channel commit regressions, with the suite still exiting 1 only on the pre-existing unrelated oversized guard.
- 2026-06-21: Core Slice D RED evidence added the effect delivery request boundary guard. The RED run failed `runtime channel damage uses effect delivery request boundary` because `BattleChannelDamageResolver` still directly called `BattleEffectResolver.Apply` and constructed damage payloads during hit scan.
- 2026-06-21: Core Slice D implementation added `BattleCommitBuffer` effect delivery requests and a delivery commit phase. Channel damage hit scan now enqueues deliveries only; delivery commit calls the target actor's `BattleEffectReceiver`, which routes damage through `BattleHealthComponent` and the existing effect damage commit path. Existing channel-start immediate tick and ordinary active-channel batch behavior, event id suffixing, and `EffectApplied` -> `DamageApplied` ordering remain covered by the existing channel regressions.
- 2026-06-21: Core Slice D review follow-up tightened delivery anchors and commit phase boundaries. Delivery commit now passes captured actor/target anchors into the final damage request so later phase interleaving cannot drift event cells, and `CommitEffectDeliveries` no longer flushes unrelated direct effect damage when no delivery requests are queued.
- 2026-06-21: Core Slice E RED evidence added actor-local pending ability order ownership guards. The RED run failed `runtime ability controller owns pending ability orders` because `BattleRuntimeState.PendingHeroSkillCommands` still owned the global queue and `BattleRuntimeHeroSkillCommandResolver` still iterated/removed pending commands.
- 2026-06-21: Core Slice E implementation moved accepted pending skill orders onto `BattleRuntimeActor.PendingAbilityOrders`, added `BattleAbilityController.EnqueuePendingSkillOrder` and `ResolvePendingSkillOrders`, and left `BattleRuntimeHeroSkillCommandResolver` as the submission/validation/result boundary. Same-caster idle supersession, active-skill queue waiting, basic-attack recovery waiting, release readiness, and pending removal now live with the caster's ability controller. `BattleRuntimeState` keeps only an order sequence for stable cross-actor pending release ordering, not pending order storage.
- 2026-06-21: Core Slice E sub-agent reviews found one Important issue: multiple pending orders for the same caster could release in one runtime tick if the first queued skill had no cast or recovery. Review follow-up added behavior coverage and fixed the pending pass so an actor that consumes an action in the current tick leaves later pending orders waiting. A second behavior regression also split missing-skill failure reason from dead/invalid-caster failure and preserved pending command actor/group attribution in failure events.
- 2026-06-21: Core Slice E verification: `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal` passes the new pending-order ownership guard, one-action-per-actor-tick regression, pending failure attribution regression, hero-skill regressions, Thunder Mark / Thunder Spiral regressions, channel cadence, pause, attack, movement, report, and settlement regressions, but exits 1 on the pre-existing unrelated oversized guard `tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegressionCases.PresentationAntiRot.cs:1123`.
- 2026-06-21: Core Slice F RED evidence added explicit delayed-impact and authored instant-impact coverage. The focused RED coverage failed before the fix because default `AttackImpactDelaySeconds = 0` collapsed unset timing into instant impact, while older compatibility fixtures still needed a way to opt into immediate damage.
- 2026-06-21: Core Slice F implementation completed the actor-local basic attack lifecycle. `BattleActionController` now owns attack windup, impact readiness, recovery transition, and recovery-boundary return to `AnchoredDecision`; `BattleRuntimeTickResolver` advances recovery boundaries before selecting new decisions; `BattleCommitBuffer` applies impact damage at the Runtime impact boundary. Snapshot/request impact timing now uses `NaN` for unset default timing, while authored `0` remains valid instant-impact compatibility.
- 2026-06-21: Core Slice F verification: `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal` passes the basic-attack lifecycle, windup pause, explicit zero impact, event-order golden, continuous movement handoff, hero-skill, Thunder Mark / Thunder Spiral, channel cadence, pause, movement, report, and settlement regressions, but exits 1 on the pre-existing unrelated oversized guard `tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegressionCases.PresentationAntiRot.cs:1123`.
- 2026-06-21: Core Slice F broader verification passed: `dotnet run --project tests\BattleHitFeedbackRegression\BattleHitFeedbackRegression.csproj -v:minimal`; `dotnet build rpg.csproj -maxcpucount:2 -v:minimal` with 0 warnings and 0 errors; `git diff --check` with only pre-existing CRLF warnings in `tests/BattleHitFeedbackRegression/BattleHitFeedbackRegressionCases.BattleResult.cs` and `tests/BattleHitFeedbackRegression/BattleHitFeedbackRegressionCases.WorldText.cs`.
- 2026-06-21: Core Slice F sub-agent review fixes added RED coverage for mixed delayed-impact and instant-impact basic attacks in the same runtime tick. The RED run failed because separate basic-attack commits rebuilt target HP from tick-start facts and overwrote the first batch. `BattleAttackResolver` now gathers active due impacts and newly-started instant impacts into one `BattleCommitBuffer` commit per tick, preserving deterministic same-impact batching.
- 2026-06-21: Core Slice F review fixes added RED coverage for attack recovery reopening an attack-ready decision boundary. The RED run failed because recovery returned to `AnchoredDecision` with `AttackCharge = 0`, adding an extra retry slice. `BattleRuntimeActorStateMachine.MarkAnchoredDecision` now restores attack readiness when an actor leaves an action lock.
- 2026-06-21: Core Slice F review fixes added RED coverage for skill commands submitted exactly at a basic-attack impact boundary. The RED run failed because `BattleAbilityController` treated an already-due attack as pre-impact windup and interrupted it. Active skills now interrupt basic attack windup only while `CurrentBasicAttackImpactAtSeconds` is still in the future.
- 2026-06-21: Core Slice F presentation compatibility fix added a BattleHitFeedback source guard so target-side HP and hit feedback do not wait `ActionImpactDelaySeconds` or fallback animation impact delay again after `DamageApplied`. `BattleRuntimeLivePresentationObserver` now treats `DamageApplied` as already impact-aligned for target damage application while preserving action duration for animation and death timing.
- 2026-06-21: Core Slice F final review follow-up strengthened the impact-boundary skill regression to use the same visible corps actor as both basic-attack attacker and queued skill caster. The test now proves the due `auto_attack` `DamageApplied` event still resolves in the same tick while the skill command waits instead of reporting a pre-impact interruption.
- 2026-06-21: Core Slice F post-review verification passed: `dotnet run --project tests\BattleHitFeedbackRegression\BattleHitFeedbackRegression.csproj -v:minimal`; `dotnet build rpg.csproj -maxcpucount:2 -v:minimal` with 0 warnings and 0 errors; `git diff --check` with only the pre-existing CRLF warnings in two BattleHitFeedback files. `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal` passes Slice F, command-boundary, cadence, golden, skill, movement, report, and settlement regressions, and still exits 1 only on the pre-existing unrelated oversized guard `tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegressionCases.PresentationAntiRot.cs:1123`.
- 2026-06-21: Core Slice G RED evidence added movement-controller shell source guards. The first RED run failed because `BattleMovementController` and `BattleActorRuntime.MovementController` were not authored, `BattleRuntimeTickResolver` still directly advanced movement boundaries, and continuation / ended-chain cleanup still called `BattleMovementContinuationPlanner` directly. A review-fix RED run then failed because the first movement-controller shell built `BattleDynamicOccupancy.FromActors(...)` inside the actor controller, violating the world-owned occupancy boundary.
- 2026-06-21: Core Slice G implementation added actor-held `BattleMovementController`, exposed it through `BattleActorRuntime`, routed moving-phase boundary advancement, continuation context construction, and ended-chain cleanup through movement-controller entry points, and added `BattleMovementBoundaryCoordinator` as the world-side pre-boundary occupancy / boundary-event coordinator. Occupancy snapshot construction remains before completed movement boundary advancement, preserving the prior tick-start occupancy semantics while keeping `BattleDynamicOccupancy`, reservations, topology, plan-state events, and movement event emission outside the actor controller.
- 2026-06-21: Core Slice G sub-agent reviews completed. Spec review found one Important issue, the misplaced occupancy snapshot in `BattleMovementController`; the fix moved occupancy construction to `BattleMovementBoundaryCoordinator` and added a regression guard. Code-quality review found no Critical or Important issues after the fix; its only Minor note was that the authority guard is string-based and sufficient for this immediate anti-rot case.
- 2026-06-21: Core Slice G verification: `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal` passes the three Core Slice G movement-controller tests, tick-resolver decomposition guard, movement, skill, attack, report, and settlement regressions, but exits 1 on the pre-existing unrelated oversized guard `tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegressionCases.PresentationAntiRot.cs:1123`.
- 2026-06-21: Core Slice G broader verification passed: `dotnet run --project tests\BattleHitFeedbackRegression\BattleHitFeedbackRegression.csproj -v:minimal`; `dotnet build rpg.csproj -maxcpucount:2 -v:minimal` with 0 warnings and 0 errors.
- 2026-06-21: Core Slice H1 RED evidence added objective/region movement proposal boundary guards. The first RED run failed because `BattleMovementController.BuildMovementProposalContext(...)`, `BattleMovementProposalBuildRequest`, and `BattleMovementProposalWorldInputs` were missing while `BattleRuntimeTickResolver` and movement continuation still called objective/region planners directly. Review-fix RED coverage later failed while continuation outsider and pressure region proposals still bypassed the movement controller, and a code-quality RED guard failed until the controller validated `request.Request.ActorId`.
- 2026-06-21: Core Slice H1 implementation routes `AdvanceTowardObjective`, `ReturnToObjective`, `AdvanceTowardRegion`, combat-zone outsider region advance, and pressure region advance through `BattleMovementController` entry points. Target/local-combat movement, stale retargeting, alternate combat-zone target fallback, reservation, topology, occupancy snapshots, movement event emission, and movement commits remain on their previous world-owned paths. The controller now fails fast on actor/context/request identity mismatches and unsupported non-H1 movement proposal kinds.
- 2026-06-21: Core Slice H1 sub-agent reviews completed. Spec review found and closed two Important continuation bypasses plus a guard gap. Code-quality review found and closed two Important issues: missing request actor-id validation at the controller boundary and too-narrow source-guard coverage. Follow-up review found no remaining Critical, Important, or Minor issues.
- 2026-06-21: Core Slice H1 verification: `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal` passes the H1 movement-controller proposal-boundary guard, movement, skill, attack, report, and settlement regressions, but exits 1 on the pre-existing unrelated oversized guard `tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegressionCases.PresentationAntiRot.cs:1123`.
- 2026-06-21: Core Slice H1 broader verification passed: `dotnet run --project tests\BattleHitFeedbackRegression\BattleHitFeedbackRegression.csproj -v:minimal`; `dotnet build rpg.csproj -maxcpucount:2 -v:minimal` with 0 warnings and 0 errors; `git diff --check` with only pre-existing CRLF warnings in `tests/BattleHitFeedbackRegression/BattleHitFeedbackRegressionCases.BattleResult.cs` and `tests/BattleHitFeedbackRegression/BattleHitFeedbackRegressionCases.WorldText.cs`.
- 2026-06-21: Core Slice H2 RED evidence added target/local-combat movement proposal boundary guards. The first guard passed after the initial implementation, then a stale navigation anti-rot guard failed because it still required `BattleRuntimeTickResolver` to call `BattleCrowdMovementPlanner` directly. A review-fix RED guard then failed because `BattleTargetMovementProposalBuildRequest` carried the full tick-start fact map into `BattleMovementController`, and a final RED guard failed until the controller validated `request.Request.TargetActorId` against the supplied target fact.
- 2026-06-21: Core Slice H2 implementation routes already-selected `AdvanceTowardTarget`, `JoinLocalCombat`, `HoldSupport`, stored combat-slot reuse, and fresh target/local-combat proposal construction through `BattleMovementController.BuildTargetMovementProposalContext(...)`. Target acquisition, scoped candidate facts, behavior-tree request choice, local-combat situation construction, continuation validation, alternate combat-zone fallback, stale retargeting, diagnostics, pressure fallback, reservation maps, topology, commits, and movement event emission remain outside the controller. The controller receives a caller-resolved `TargetEngagedBySameFactionActor` scalar instead of the full tick-start facts and fails fast on actor/request/target identity mismatches.
- 2026-06-21: Core Slice H2 sub-agent reviews completed using `csharp-godot`, `state-machine`, `ai-navigation`, `godot-testing`, and `godot-code-review` guidance. Spec review found and closed the full-facts boundary violation. Code-quality review found no Critical or Important issues; its minor target/request consistency note was fixed and re-reviewed clean. The remaining minor `BattleMovementController` dependency on `BattleRuntimeTickResolver.CreateContext(...)` / attack-gap helpers is accepted for this slice and should be removed in a later movement/controller decomposition slice.
- 2026-06-21: Core Slice H2 verification: `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal` passes the H2 target/local-combat movement-controller boundary guard, updated navigation anti-rot guard, movement, skill, attack, report, and settlement regressions, but exits 1 on the pre-existing unrelated oversized guard `tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegressionCases.PresentationAntiRot.cs:1123`.
- 2026-06-21: Core Slice H2 broader verification passed: `dotnet run --project tests\BattleHitFeedbackRegression\BattleHitFeedbackRegression.csproj -v:minimal`; `dotnet build rpg.csproj -maxcpucount:2 -v:minimal` with 0 warnings and 0 errors; `git diff --check` with only pre-existing CRLF warnings in `tests/BattleHitFeedbackRegression/BattleHitFeedbackRegressionCases.BattleResult.cs` and `tests/BattleHitFeedbackRegression/BattleHitFeedbackRegressionCases.WorldText.cs`.
- 2026-06-21: Core Slice H3 RED evidence added movement-helper decoupling guards. The first RED run failed because `BattleRuntimeTickContextFactory` / `BattleCombatGeometry` did not exist and movement/runtime helpers still depended on `BattleRuntimeTickResolver.CreateContext(...)` / `BattleRuntimeTickResolver.GetOrthogonalAttackGap(...)`.
- 2026-06-21: Core Slice H3 implementation added `BattleRuntimeTickContextFactory` and `BattleCombatGeometry`, moved the previous context construction and orthogonal attack-gap helpers out of `BattleRuntimeTickResolver`, and replaced callers in the tick resolver, objective/region planners, combat-zone retargeting, movement continuation/controller, AI request facts, and target selection. Tick ordering, target acquisition, diagnostics, movement commits, event stream writes, and final state mutation remain on their existing owners.
- 2026-06-21: Core Slice H3 sub-agent reviews completed. Spec review found no compliance issues. Code-quality review found no Critical or Important issues; its minor guard-hardening recommendation was applied so the architecture guard also rejects reintroducing `CreateContext(...)` or `GetOrthogonalAttackGap(...)` definitions on `BattleRuntimeTickResolver`.
- 2026-06-21: Core Slice H3 verification: `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal` passes the H3 movement-helper decoupling guard, H1/H2 movement-controller guards, movement, skill, attack, report, and settlement regressions, but exits 1 on the pre-existing unrelated oversized guard `tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegressionCases.PresentationAntiRot.cs:1123`.
- 2026-06-21: Core Slice H3 broader verification passed: `dotnet run --project tests\BattleHitFeedbackRegression\BattleHitFeedbackRegression.csproj -v:minimal`; `dotnet build rpg.csproj -maxcpucount:2 -v:minimal` with 0 warnings and 0 errors; `git diff --check` with only pre-existing CRLF warnings in `tests/BattleHitFeedbackRegression/BattleHitFeedbackRegressionCases.BattleResult.cs` and `tests/BattleHitFeedbackRegression/BattleHitFeedbackRegressionCases.WorldText.cs`.
- 2026-06-21: Core Slice H4 RED evidence added the movement continuation primitive guard. The RED run failed because `BattleMovementController.BuildContinuationContext(...)` still re-entered `BattleMovementContinuationPlanner.BuildContinuationContexts(...)` for a one-actor list.
- 2026-06-21: Core Slice H4 implementation exposed `BattleMovementContinuationPlanner.TryBuildContinuationContext(...)` and `ClearEndedMovementChain(...)` as single-actor primitives. `BattleMovementController` now calls those primitives from its single-actor continuation and cleanup entry points, while `BattleRuntimeTickResolver` still enters continuation through `BattleMovementController.BuildContinuationContexts(...)` and `ClearEndedMovementChains(...)`. Continuation validation, target identity checks, local-combat matching, topology, occupancy, reservation, movement commit, and event emission ownership remain unchanged.
- 2026-06-21: Core Slice H4 sub-agent reviews completed. Spec review found no Critical, Important, or Minor issues. Code-quality review found no Critical or Important issues; its Minor guard-hardening recommendation was applied so the H4 guard also requires the tick resolver to enter through `BattleMovementController` and rejects direct single-actor planner primitive calls from other runtime files.
- 2026-06-21: Core Slice H4 verification: `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal` passes the H4 movement-continuation primitive guard, H1/H2/H3 movement-controller guards, movement, skill, attack, report, and settlement regressions, but exits 1 on the pre-existing unrelated oversized guard `tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegressionCases.PresentationAntiRot.cs:1123`.
- 2026-06-21: Core Slice H4 broader verification passed: `dotnet run --project tests\BattleHitFeedbackRegression\BattleHitFeedbackRegression.csproj -v:minimal`; `dotnet build rpg.csproj -maxcpucount:2 -v:minimal` with 0 warnings and 0 errors; `git diff --check` with only pre-existing CRLF warnings in `tests/BattleHitFeedbackRegression/BattleHitFeedbackRegressionCases.BattleResult.cs` and `tests/BattleHitFeedbackRegression/BattleHitFeedbackRegressionCases.WorldText.cs`.
- 2026-06-21: Core Slice H5 RED evidence added the single batch-entry guard. The RED run failed `runtime movement continuation has one batch entry` because `BattleMovementContinuationPlanner` still exposed `BuildContinuationContexts(...)`.
- 2026-06-21: Core Slice H5 implementation removed `BattleMovementContinuationPlanner.BuildContinuationContexts(...)` and `ClearEndedMovementChains(...)`. `BattleMovementController` became the only runtime batch continuation entry; at the H5 boundary, the planner retained only the single-actor continuation and cleanup primitives.
- 2026-06-21: Core Slice H5 sub-agent reviews completed. Spec review and code-quality review found no Critical, Important, or Minor issues. Reviews confirmed tick resolver entry still routes through `BattleMovementController`, deterministic actor-id ordering and cleanup semantics remain on the controller path, and no Godot node/timer/scene authority or hidden fallback was introduced.
- 2026-06-21: Core Slice H5 verification: `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal` passes the H5 single batch-entry guard, H1-H4 movement-controller guards, movement, skill, attack, report, and settlement regressions, but exits 1 on the pre-existing unrelated oversized guard `tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegressionCases.PresentationAntiRot.cs:1123`.
- 2026-06-21: Core Slice H5 broader verification passed: `dotnet run --project tests\BattleHitFeedbackRegression\BattleHitFeedbackRegression.csproj -v:minimal`; `dotnet build rpg.csproj -maxcpucount:2 -v:minimal` with 0 warnings and 0 errors; `git diff --check` with only pre-existing CRLF warnings in `tests/BattleHitFeedbackRegression\BattleHitFeedbackRegressionCases.BattleResult.cs` and `tests/BattleHitFeedbackRegression\BattleHitFeedbackRegressionCases.WorldText.cs`.
- 2026-06-21: Core Slice H6 RED evidence added the movement cleanup ownership guard. The RED run failed `runtime movement cleanup is controller owned` because `BattleMovementController` still delegated cleanup to `BattleMovementContinuationPlanner`.
- 2026-06-21: Core Slice H6 implementation moved ended movement-chain cleanup into `BattleMovementController.ClearEndedMovementChain()` and removed `BattleMovementContinuationPlanner.ClearEndedMovementChain(...)`. The controller preserves the still-moving active-target guard and exhausted `FollowObstacle` steering preservation before calling `BattleRuntimeActorStateMachine.ClearMovementIntentSnapshot(...)`; continuation validation remains in `BattleMovementContinuationPlanner.TryBuildContinuationContext(...)`.
- 2026-06-21: Core Slice H6 sub-agent reviews completed. Spec review and code-quality review found no Critical, Important, or Minor issues. Reviews confirmed cleanup semantics, controller ownership, planner validation ownership, and absence of Godot node/timer/scene authority, reservation fallback, spatial commit writes, event stream writes, or hidden cleanup fallback.
- 2026-06-21: Core Slice H6 verification: `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal` passes the H6 movement-cleanup ownership guard, H1-H5 movement-controller guards, movement, skill, attack, report, and settlement regressions, but exits 1 on the pre-existing unrelated oversized guard `tests/WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegressionCases.PresentationAntiRot.cs:1123`.
- 2026-06-21: Core Slice H6 broader verification passed: `dotnet run --project tests\BattleHitFeedbackRegression\BattleHitFeedbackRegression.csproj -v:minimal`; `dotnet build rpg.csproj -maxcpucount:2 -v:minimal` with 0 warnings and 0 errors; `git diff --check` with only pre-existing CRLF warnings in `tests/BattleHitFeedbackRegression\BattleHitFeedbackRegressionCases.BattleResult.cs` and `tests/BattleHitFeedbackRegression\BattleHitFeedbackRegressionCases.WorldText.cs`.
- 2026-06-21: Core Slice H7 RED evidence added runtime identity/command ownership guards. The RED run failed `runtime identity and command rules are not owned by tick resolver` because `BattleRuntimeIdentityRules` did not exist and the center tick resolver/session/tactical helpers still owned or duplicated faction and command helper rules.
- 2026-06-21: Core Slice H7 implementation added `BattleRuntimeIdentityRules` and routed tick resolver, session bootstrap/termination, AI request construction, target selection, tactical observation, local-combat filtering/builders, movement continuation, ability validation, hero-skill validation, and channel hit scanning through the shared Runtime identity helper. The tick resolver no longer defines `SameFaction`, `NormalizeFaction`, `IsFocusFireCommand`, `IsHoldLineCommand`, `NormalizeCorpsCommandId`, or command constants. Command compatibility remains `FocusFire` / `HoldLine` case-insensitive normalization with unknown or empty commands defaulting to `Assault`; empty factions still normalize to `player`.
- 2026-06-21: Core Slice H7 added semantic guard coverage for faction and command compatibility, then mutation-verified it by temporarily breaking unknown-command normalization and observing `runtime identity rules preserve command and faction compatibility` fail before restoring the helper.
- 2026-06-21: Core Slice H7 sub-agent reviews completed. Spec review found no Critical, Important, or Minor issues and recommended semantic guard coverage, which was added. Code-quality review found no Critical or Important issues; its Minor guard-hardening recommendation was applied so non-helper Runtime files cannot redefine shared identity/command helper methods with `private`, `internal`, or `public` static visibility.
- 2026-06-21: Core Slice H7 verification: `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal` passes the H7 identity/command ownership guard, H7 semantic compatibility guard, H1-H6 movement-controller guards, movement, skill, attack, report, and settlement regressions, but exits 1 on the pre-existing unrelated oversized guard `tests/WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegressionCases.PresentationAntiRot.cs:1123`.
- 2026-06-21: Core Slice H7 broader verification passed: `dotnet run --project tests\BattleHitFeedbackRegression\BattleHitFeedbackRegression.csproj -v:minimal`; `dotnet build rpg.csproj -maxcpucount:2 -v:minimal` with 0 warnings and 0 errors; `git diff --check` with only pre-existing CRLF warnings in `tests/BattleHitFeedbackRegression\BattleHitFeedbackRegressionCases.BattleResult.cs` and `tests/BattleHitFeedbackRegression\BattleHitFeedbackRegressionCases.WorldText.cs`.
- 2026-06-21: Core Slice H8 RED evidence added the decision-outcome service ownership guard. The RED run failed `runtime decision outcome application is service owned` because `BattleDecisionOutcomeApplier` did not exist, `BattleRuntimeTickResolver` still defined `ApplyDecisionOutcomes(...)`, and the resolver did not call a decision-outcome service entry point.
- 2026-06-21: Core Slice H8 implementation added `BattleDecisionOutcomeApplier`, moved decision outcome application out of `BattleRuntimeTickResolver`, and kept the resolver as the tick-order caller. The applier preserves the previous handling for hold, invalid target, target lock, objective advance state, proposal failure, wait-for-charge, and unsupported action results while explicitly not owning AI request construction, target selection, movement proposals, movement commits, attacks, effects, ability lifecycle, damage, health, defeat, displacement, topology, occupancy, reservations, or commit-buffer phases. The shared `RecordAdvanceFailureCallback` now lives in `BattleAdvanceFailureStateBoundary` instead of the AI request builder.
- 2026-06-21: Core Slice H8 guard hardening and mutation checks completed. The guard now blocks decision outcome body tokens from regrowing in `BattleRuntimeTickResolver`, blocks world-authority dependencies from `BattleDecisionOutcomeApplier`, requires stream fail-fast, and prevents AI request construction from depending on advance-failure callbacks. Temporary mutation probes using `BattleAttackResolver` inside the applier and `BattleRuntimeActorStateMachine.MarkHolding` inside the resolver made the H8 guard fail before the probes were removed.
- 2026-06-21: Core Slice H8 sub-agent reviews completed. Spec review initially found an Important guard coverage gap; guard hardening closed it and re-review passed with no Critical, Important, or Minor issues. Code-quality review found one Important residual callback coupling in `BattleAiActionRequestBuilder`, which was removed and re-reviewed clean; no Godot, Presentation, movement, attack, effect, damage, or commit authority regressions were found.
- 2026-06-21: Core Slice H8 verification: `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal` passes the H8 decision-outcome ownership guard, H7 identity/command guards, H1-H6 movement-controller guards, movement, skill, attack, report, and settlement regressions, but exits 1 on the pre-existing unrelated oversized guard `tests/WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegressionCases.PresentationAntiRot.cs:1123`.
- 2026-06-21: Core Slice H8 broader verification passed: `dotnet run --project tests\BattleHitFeedbackRegression\BattleHitFeedbackRegression.csproj -v:minimal`; `dotnet build rpg.csproj -maxcpucount:2 -v:minimal` with 0 warnings and 0 errors; `git diff --check` with only pre-existing CRLF warnings in `tests/BattleHitFeedbackRegression\BattleHitFeedbackRegressionCases.BattleResult.cs` and `tests/BattleHitFeedbackRegression\BattleHitFeedbackRegressionCases.WorldText.cs`.
- 2026-06-21: Core Slice H9 RED evidence added the decision-context builder ownership guard. The first RED run failed because `BattleRuntimeDecisionContextBuilder` did not exist, `BattleRuntimeTickResolver` still defined `BuildTickContext(...)`, the resolver still built AI requests / target candidates / local-combat situations directly, and stale-target retargeting still called the hidden resolver context builder. Review-fix RED coverage then failed because `BattleCombatZoneJoinRetargeting` still bypassed the actor movement controller with direct slot/crowd proposal construction.
- 2026-06-21: Core Slice H9 implementation added `BattleRuntimeDecisionContextBuilder`, moved decision context construction out of `BattleRuntimeTickResolver`, and routed stale-target retarget refresh through the same builder. The resolver now filters decision-ready actors and delegates context construction before decision-outcome, continuation, attack, movement-commit, cleanup, and logging phases. Alternate combat-zone join fallback remains candidate-scoped, but proposal construction now enters `BattleMovementController.TryBuildAlternateCombatZoneJoinProposalContext(...)` so slot/crowd movement construction stays behind the actor movement controller boundary.
- 2026-06-21: Core Slice H9 sub-agent reviews completed. Spec review found and closed one Important boundary issue: alternate combat-zone retargeting still built target/local-combat movement proposals outside `BattleMovementController`. Code-quality review found no Critical or Important issues; its Minor guard-hardening note was applied so the H9 guard also blocks slot/crowd proposal construction from regrowing inside `BattleRuntimeDecisionContextBuilder`.
- 2026-06-21: Core Slice H9 verification: `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal` passes the H9 decision-context builder guard, updated navigation anti-rot guard, H8 decision-outcome guard, H7 identity/command guards, H1-H6 movement-controller guards, movement, skill, attack, report, and settlement regressions, but exits 1 on the pre-existing unrelated oversized guard `tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegressionCases.PresentationAntiRot.cs:1123`.
- 2026-06-21: Core Slice H9 broader verification passed: `dotnet run --project tests\BattleHitFeedbackRegression\BattleHitFeedbackRegression.csproj -v:minimal`; `dotnet build rpg.csproj -maxcpucount:2 -v:minimal` with 0 warnings and 0 errors; `git diff --check` with only pre-existing CRLF warnings in `tests\BattleHitFeedbackRegression\BattleHitFeedbackRegressionCases.BattleResult.cs` and `tests\BattleHitFeedbackRegression\BattleHitFeedbackRegressionCases.WorldText.cs`.
- 2026-06-21: Core Slice H10 RED evidence added the attack-engagement coordinator ownership guard. The RED run failed `runtime attack engagement coordination is service owned` because `BattleAttackEngagementCoordinator` did not exist, `BattleRuntimeTickResolver.Engagement.cs` still defined `ResolveAttackProposalsAndEngagementTriggers(...)`, the resolver still called `BattleAttackResolver.Resolve(...)` and `BattleTacticalObservationUpdater.ApplyPostAttackEngagementTriggers(...)` directly, and the resolver still scanned `BattleEventKind.DamageApplied` from the event stream.
- 2026-06-21: Core Slice H10 implementation added `BattleAttackEngagementCoordinator`, moved the post-attack engagement wrapper out of `BattleRuntimeTickResolver`, deleted the resolver-owned engagement partial, and kept the coordinator in the same tick-order position: after decision outcome application and movement-continuation context construction, before movement commit. The coordinator now fail-fasts on missing required runtime inputs, slices only events emitted after the current `BattleAttackResolver.Resolve(...)` call, filters those events to `DamageApplied`, and passes the filtered `attackEvents` into `BattleTacticalObservationUpdater.ApplyPostAttackEngagementTriggers(...)`.
- 2026-06-21: Core Slice H10 sub-agent reviews completed. Spec review found no runtime compliance issues and identified the missing proposal record plus guard-hardening opportunities; code-quality review found an Important guard dataflow gap and an Important authority-guard gap, plus Minor fail-fast and naming-consistency issues. Follow-up fixes added explicit old-partial removal, event-slice order and parameter guards, channel/effect/health authority exclusions, fail-fast required inputs, and consistent `BattleAttackEngagementCoordinator.Resolve(...)` guard naming. Re-review found no remaining Critical, Important, or Minor issues.
- 2026-06-21: Core Slice H10 verification: `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal` passes the H10 attack-engagement coordinator guard, H9 decision-context builder guard, H8 decision-outcome guard, H7 identity/command guards, H1-H6 movement-controller guards, attack cadence, event-order golden, movement, skill, report, and settlement regressions, but exits 1 on the pre-existing unrelated oversized guard `tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegressionCases.PresentationAntiRot.cs:1123`.
- 2026-06-21: Core Slice H10 broader verification passed: `dotnet run --project tests\BattleHitFeedbackRegression\BattleHitFeedbackRegression.csproj -v:minimal`; `dotnet build rpg.csproj -maxcpucount:2 -v:minimal` with 0 warnings and 0 errors; `git diff --check` with only pre-existing CRLF warnings in `tests\BattleHitFeedbackRegression\BattleHitFeedbackRegressionCases.BattleResult.cs` and `tests\BattleHitFeedbackRegression\BattleHitFeedbackRegressionCases.WorldText.cs`.
- 2026-06-21: Core Slice H11 RED evidence added the advance-failure state boundary guard. The RED run failed `runtime advance failure state is boundary owned` because `BattleAdvanceFailureStateBoundary` only defined the callback delegate, `BattleRuntimeTickResolver.Diagnostics.cs` still defined `RecordAdvanceFailure(...)` and `ResetAdvanceFailureState(...)`, and action/movement decision paths still reached those helpers through the tick resolver.
- 2026-06-21: Core Slice H11 implementation added `BattleAdvanceFailureStateBoundary.RecordAdvanceFailure(...)` and `ResetAdvanceFailureState(...)`, removed advance-failure helper ownership from `BattleRuntimeTickResolver`, and routed decision-outcome application, movement commit failure/success handling, basic-attack start cleanup, and stale-target retarget cleanup through the neutral boundary. Existing null no-op behavior, `advance_failed` fallback reason, failure counter increments, reset semantics, tick order, diagnostics, movement behavior, event ordering, pause behavior, report attribution, and settlement contracts remain unchanged.
- 2026-06-21: Core Slice H11 sub-agent reviews completed. Spec review found no Critical or Important issues and identified a Minor missing stale-retarget guard assertion, which was added. Code-quality review found one Important guard coverage gap; follow-up hardening now scans `src/Runtime/Battle` recursively and rejects direct advance-failure counter or reason mutation outside `BattleAdvanceFailureStateBoundary` while still allowing read-only consumers.
- 2026-06-21: Core Slice H11 verification: `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal` passes the H11 advance-failure state boundary guard, H10 attack-engagement coordinator guard, H9 decision-context builder guard, H8 decision-outcome guard, H7 identity/command guards, H1-H6 movement-controller guards, attack cadence, event-order golden, movement, skill, report, and settlement regressions, but exits 1 on the pre-existing unrelated oversized guard `tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegressionCases.PresentationAntiRot.cs:1123`.
- 2026-06-21: Core Slice H11 broader verification passed: `dotnet run --project tests\BattleHitFeedbackRegression\BattleHitFeedbackRegression.csproj -v:minimal`; `dotnet build rpg.csproj -maxcpucount:2 -v:minimal` with 0 warnings and 0 errors; `git diff --check` with only pre-existing CRLF warnings in `tests\BattleHitFeedbackRegression\BattleHitFeedbackRegressionCases.BattleResult.cs` and `tests\BattleHitFeedbackRegression\BattleHitFeedbackRegressionCases.WorldText.cs`.
- 2026-06-21: Core Slice H12 RED evidence added the stale advance retargeting service boundary guard. The RED run failed `runtime stale advance retargeting is service owned` because the new guard required explicit AI executor fail-fast before `BattleStaleAdvanceRetargeting.CreateCallback(...)` had `ArgumentNullException.ThrowIfNull(aiExecutor)`.
- 2026-06-21: Core Slice H12 implementation moved stale-target advance retarget refresh into standalone `BattleStaleAdvanceRetargeting`, with `BattleRuntimeTickResolver` passing only `BattleStaleAdvanceRetargeting.CreateCallback(_aiExecutor)` into `BattleMovementCommitResolver`. The service rebuilds through `BattleRuntimeDecisionContextBuilder`, preserves the previous live-actor/live-target/retargetable-request/move/no-failure checks, updates the target lock and context, and resets advance-failure state through `BattleAdvanceFailureStateBoundary`.
- 2026-06-21: Core Slice H12 sub-agent reviews completed. Spec review found only a stale ownership comment, which was corrected. Code-quality review found one Important guard-hardening issue and one Minor fail-fast issue; follow-up guard coverage now asserts the concrete retarget eligibility and rejection semantics, blocks movement commit from rebuilding decision contexts directly, blocks hidden executor fallbacks, and requires immediate AI executor fail-fast.
- 2026-06-21: Core Slice H12 verification: `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal` passes the H12 stale-retargeting guard, H11 advance-failure boundary guard, H10 attack-engagement coordinator guard, H9 decision-context builder guard, H8 decision-outcome guard, H7 identity/command guards, H1-H6 movement-controller guards, attack cadence, event-order golden, movement, skill, report, and settlement regressions, but exits 1 on the pre-existing unrelated oversized guard `tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegressionCases.PresentationAntiRot.cs:1123`.
- 2026-06-21: Core Slice H12 broader verification passed: `dotnet run --project tests\BattleHitFeedbackRegression\BattleHitFeedbackRegression.csproj -v:minimal`; `dotnet build rpg.csproj -maxcpucount:2 -v:minimal` with 0 warnings and 0 errors; `git diff --check` with only pre-existing CRLF warnings in `tests\BattleHitFeedbackRegression\BattleHitFeedbackRegressionCases.BattleResult.cs` and `tests\BattleHitFeedbackRegression\BattleHitFeedbackRegressionCases.WorldText.cs`.
- 2026-06-21: Core Slice H13 RED evidence added the accepted movement commit boundary guard. The RED run failed `runtime accepted movement commit application is boundary owned` because `BattleMovementCommitBoundary` did not exist and `BattleMovementCommitResolver` still wrote accepted reservation state, marked movement committed, created `MovementStarted`, assigned success, and recorded movement events directly.
- 2026-06-21: Core Slice H13 implementation added `BattleMovementCommitBoundary.ApplyAcceptedMove(...)` and routed successful reserved movement through it. `BattleMovementCommitResolver` retains movement candidate filtering, stale-target detection/retarget callback invocation, reservation map ownership, `TryReserveMove`, reservation rejection, failure diagnostics, and movement-event counting. The boundary owns accepted reservation fields, plan-state transition/logging, combat-slot trace logging, movement phase transition, advance-failure reset, success result assignment, `MovementStarted` event creation, and movement performance recording.
- 2026-06-21: Core Slice H13 sub-agent reviews completed. Spec review found no issues. Code-quality review found Important fail-fast, planner-coupling, and guard-hardening issues; follow-up fixes make `ApplyAcceptedMove(...)` validate context, stream, and actor before mutation, move movement-event target-id resolution into the commit boundary, forbid planner dependencies from the boundary, and forbid the resolver from regrowing accepted movement plan-state, trace logging, or movement performance recording.
- 2026-06-21: Core Slice H13 verification: `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal` passes the H13 movement commit boundary guard, H12 stale-retargeting guard, H11 advance-failure boundary guard, H10 attack-engagement coordinator guard, H9 decision-context builder guard, H8 decision-outcome guard, H7 identity/command guards, H1-H6 movement-controller guards, attack cadence, event-order golden, movement, skill, report, and settlement regressions, but exits 1 on the pre-existing unrelated oversized guard `tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegressionCases.PresentationAntiRot.cs:1123`.
- 2026-06-21: Core Slice H13 broader verification passed: `dotnet run --project tests\BattleHitFeedbackRegression\BattleHitFeedbackRegression.csproj -v:minimal`; `dotnet build rpg.csproj -maxcpucount:2 -v:minimal` with 0 warnings and 0 errors; `git diff --check` with only pre-existing CRLF warnings in `tests\BattleHitFeedbackRegression\BattleHitFeedbackRegressionCases.BattleResult.cs` and `tests\BattleHitFeedbackRegression\BattleHitFeedbackRegressionCases.WorldText.cs`.
- 2026-06-21: Core Slice H14 RED evidence added the basic-attack health ownership guard. The RED run failed `runtime basic attack health mutation routes through health component` because `BattleHealthComponent.CommitBasicAttackDamage(...)` did not exist, basic-attack HP/defeat mutation still lived directly in `BattleCommitBuffer`, and the guard did not yet enforce target-scoped health commits.
- 2026-06-21: Core Slice H14 implementation added `BattleHealthComponent.CommitBasicAttackDamage(...)`, shared HP/defeat transition semantics through `CommitHitPointChange(...)`, and routed basic-attack target HP/defeat mutation through a target actor health component. `BattleCommitBuffer` remains the deterministic same-tick basic-attack coordinator: it batches from tick-start HP, emits `DamageApplied` before movement, commits only attacked targets, emits defeated plan-state transitions in target actor-id order, and keeps basic-attack absolute remaining-HP commits scoped to the basic-attack batch phase.
- 2026-06-21: Core Slice H14 sub-agent reviews completed. Spec review found only guard-strengthening coverage for target-scoped commits, which was added. Code-quality review found Important issues around broad all-actor health commits, defeated plan-state ordering, and implicit absolute-HP commit semantics; follow-up fixes restrict commits to `basicAttackTargetIds`, sort defeated plan-state emission by actor id, and document that interleaved non-basic-attack HP sources must commit in their own phase rather than between tick-start basic-attack accumulation and basic-attack commit.
- 2026-06-21: Core Slice H14 verification: `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal` passes the H14 basic-attack health boundary guard, target-scoped deterministic plan-state guard, H13 movement commit boundary guard, H12 stale-retargeting guard, attack cadence, event-order golden, movement, skill, report, and settlement regressions, but exits 1 on the pre-existing unrelated oversized guard `tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegressionCases.PresentationAntiRot.cs:1123`.
- 2026-06-21: Core Slice H14 broader verification passed before the final guard-hardening edit: `dotnet run --project tests\BattleHitFeedbackRegression\BattleHitFeedbackRegression.csproj -v:minimal`; `dotnet build rpg.csproj -maxcpucount:2 -v:minimal` with 0 warnings and 0 errors; `git diff --check` with only pre-existing CRLF warnings in `tests\BattleHitFeedbackRegression\BattleHitFeedbackRegressionCases.BattleResult.cs` and `tests\BattleHitFeedbackRegression\BattleHitFeedbackRegressionCases.WorldText.cs`. Final broad verification will be rerun after the next slice or before stopping.
- 2026-06-21: Core Slice H15 RED evidence added the movement continuation ownership guard. The RED run showed continuation policy still lived outside the actor movement controller and that the controller file had grown past the intended decomposition guard.
- 2026-06-21: Core Slice H15 implementation moved movement-chain continuation eligibility, retained-target validity, local-combat situation matching, objective/region stop checks, immediate-attack stops, and ended-chain cleanup into `BattleMovementController`, split the controller into a continuation partial, and deleted `BattleMovementContinuationPlanner` instead of leaving an empty compatibility owner.
- 2026-06-21: Core Slice H15 verification: `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal` passes the H15 movement-continuation ownership guards and existing movement/skill/attack/report/settlement regressions, but exits 1 on the pre-existing unrelated oversized guard `tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegressionCases.PresentationAntiRot.cs:1123`. Sub-agent review found no Critical or Important issues after the empty planner was removed.
- 2026-06-21: Core Slice H16 RED evidence added the active-channel ownership guard. The RED run failed because `BattleRuntimeActor` did not own active channels and runtime state still carried global channel ownership.
- 2026-06-21: Core Slice H16 implementation moved active channeled-skill instances onto the caster `BattleRuntimeActor`, ticks cadence and expiry through the caster's `BattleAbilityController`, and keeps one shared commit buffer across ordinary channel ticks so overlapping or opposing channels resolve as a same-tick batch.
- 2026-06-21: Core Slice H16 review hardening added a lethal opposing channel-start regression and broader active-channel ownership guard. The first RED run failed because an earlier caster's lethal channel-start tick killed the later caster before it could start. The fix batches channel-start deliveries through a shared start-phase commit buffer while direct skill damage remains immediate and event ids share one buffer namespace.
- 2026-06-21: Core Slice H16 verification: `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal` passes active-channel ownership, channel-start batching, same-tick channel damage, movement, skill, attack, report, and settlement regressions, but exits 1 on the pre-existing unrelated oversized guard `tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegressionCases.PresentationAntiRot.cs:1123`. H16 follow-up sub-agent review found no Critical or Important issues.
- 2026-06-21: Core Slice H17 RED evidence added the displacement commit boundary guard. The RED run failed because `BattleDisplacementCommitBoundary` did not exist and `BattleEffectResolver` still owned Thunder Fold release validation, occupancy construction, actor displacement mutation, teleport event construction, and displacement diagnostics.
- 2026-06-21: Core Slice H17 implementation added `BattleDisplacementCommitBoundary`, routed command-time Thunder Fold validation and release-time teleport commit through it, and left `BattleEffectResolver` as a teleport effect dispatcher only. The boundary owns shared mark/radius/topology/occupancy validation, low-level displacement primitive invocation, stale movement/target context cleanup through the state machine primitive, teleport event construction, and low-noise displacement logging.
- 2026-06-21: Core Slice H17 verification: `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal` passes the H17 displacement boundary guard and existing Thunder Mark/Fold/Spiral behavior regressions, but exits 1 on the pre-existing unrelated oversized guard `tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegressionCases.PresentationAntiRot.cs:1123`.
- 2026-06-21: Core Slice H17 sub-agent review found no Critical runtime issues and two Important guard/test issues. Follow-up fixes broadened the displacement guard to scan `src/Runtime/Battle/**/*.cs`, allowing direct `BattleRuntimeActorStateMachine.CommitDisplacement(...)` only inside `BattleDisplacementCommitBoundary` and the state-machine primitive definition, and updated the battle-hit-feedback playback regression to expect Thunder Fold displacement diagnostics on the displacement boundary instead of `BattleEffectResolver`.
- 2026-06-21: Core Slice H18 RED evidence added the ability-effect release boundary guard. The RED run failed `runtime ability effect release is boundary owned` because `BattleAbilityEffectReleaseBoundary` did not exist and `BattleAbilityController` still built `BattleEffectExecutionContext` / `BattleEffectPayload` and called `BattleEffectResolver.Apply(...)` directly.
- 2026-06-21: Core Slice H18 implementation added `BattleAbilityEffectReleaseBoundary.ReleaseSkillEffects(...)` and routed active-skill impact, immediate offhand release, and zero-delay skill release through it. `BattleAbilityController` now owns actor-local skill lifecycle only; the boundary owns released-skill effect context/payload construction, immediate direct-effect dispatch, deferred channel-start delivery when a shared start buffer exists, and `UsedHeroSkillKeys` marking.
- 2026-06-21: Core Slice H18 verification: `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal` passes the H18 ability-effect release boundary guard, H17 displacement guard, active-channel batching regressions, Thunder Mark/Fold/Spiral behavior, movement, skill, attack, report, and settlement regressions, but exits 1 on the pre-existing unrelated oversized guard `tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegressionCases.PresentationAntiRot.cs:1123`. H18 sub-agent review is pending.
- 2026-06-21: Core Slice H18 sub-agent review found two Important release-boundary issues. Follow-up RED regressions reproduced duplicate one-use skill queuing while the first release was active, and duplicate `EffectApplied` / `DamageApplied` event ids when a delayed active-skill impact and a queued instant release occurred in the same runtime tick.
- 2026-06-21: Core Slice H18 follow-up implementation rejects same-battle-group same-skill submissions while that skill is actively casting/recovering, represented by an active channel, or already queued behind the same active caster, using `hero_skill_already_queued` before release and the existing `hero_skill_already_used` after release. Idle unstarted same-skill pending intent remains supersedable for retargeting, matching `battle-command-architecture.md`. The same follow-up shares one ability-tick commit buffer across active impacts, channel cadence, and pending releases so event-id uniqueness survives mixed ability phases while effect requests still commit at their existing phase boundaries.
- 2026-06-21: Core Slice H18 follow-up verification: `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal` passes duplicate queued-skill rejection, active-impact plus pending-release event-id uniqueness, H18/H19 guards, active-channel batching, Thunder Mark/Fold/Spiral, movement, skill, attack, report, and settlement regressions, but exits 1 on the pre-existing unrelated oversized guard `tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegressionCases.PresentationAntiRot.cs:1123`. The newly added effect-commit regression was split into a partial test file so no new oversized test file remains.
- 2026-06-21: Core Slice H19 RED evidence added the ability-tick coordinator ownership guard. The RED run failed `runtime ability ticking is coordinator owned` because `BattleAbilityTickCoordinator` did not exist and `BattleRuntimeHeroSkillCommandResolver` still owned active ability advancement, active channel cadence, pending order dispatch, and ability-effect commit barrier calls.
- 2026-06-21: Core Slice H19 implementation added `BattleAbilityTickCoordinator`, routed `BattleRuntimeTickResolver` through it for the ability phase, and removed runtime ticking methods from `BattleRuntimeHeroSkillCommandResolver`. The command resolver now remains focused on submit/validation/acceptance/rejection/payload locking, while the coordinator owns the tick-phase ordering across active skills, active channels, and pending actor-local ability orders.
- 2026-06-21: Core Slice H19 verification: `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal` passes the H19 ability-tick coordinator guard, H18 follow-up behavior regressions, ability lifecycle tests, active-channel batching, Thunder Mark/Fold/Spiral behavior, movement, skill, attack, report, and settlement regressions, but exits 1 on the pre-existing unrelated oversized guard `tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegressionCases.PresentationAntiRot.cs:1123`.
- 2026-06-21: Core Slice H19 sub-agent review found one Important command-supersession regression and two Minor cleanup/HUD issues. Follow-up RED/GREEN added `runtime idle caster can retarget same pending skill intent`, narrowed same-skill duplicate rejection so idle unstarted pending commands are superseded instead of rejected, removed unused release-era helpers from `BattleRuntimeHeroSkillCommandResolver`, and mapped `hero_skill_already_queued` to the existing pending-command HUD text. `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal` passes the new same-skill retarget regression plus H18/H19/H20 guards and existing battle regressions, but exits 1 on the pre-existing unrelated oversized guard `tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegressionCases.PresentationAntiRot.cs:1123`. `dotnet run --project tests\BattleHitFeedbackRegression\BattleHitFeedbackRegression.csproj -v:minimal` passes.
- 2026-06-21: Core Slice H20 RED evidence added the runtime action phase coordinator ownership guard. The RED run failed `runtime action phase coordination is service owned` because `BattleRuntimeActionPhaseCoordinator` did not exist and `BattleRuntimeTickResolver` still directly advanced movement boundaries, emitted movement-completed events, advanced attack recovery, and entered ability ticking.
- 2026-06-21: Core Slice H20 implementation added `BattleRuntimeActionPhaseCoordinator.AdvanceActionPhase(...)` and routed `BattleRuntimeTickResolver` through it. The coordinator owns movement-boundary advancement, movement-completed event emission, attack recovery boundary advancement, and ability tick entry, returning post-boundary occupancy, movement-completed actor ids, and skill-consumed actor ids for later decision/continuation filtering. H19 guard expectations were updated so `BattleAbilityTickCoordinator` remains the ability tick owner while H20 owns the TickResolver entry path.
- 2026-06-21: Core Slice H20 verification: `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal` passes the H20 action-phase coordinator guard, adjusted H19 guard, same-skill retarget regression, H18 follow-up regressions, movement/skill/attack/report/settlement regressions, but exits 1 on the pre-existing unrelated oversized guard `tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegressionCases.PresentationAntiRot.cs:1123`. `dotnet run --project tests\BattleHitFeedbackRegression\BattleHitFeedbackRegression.csproj -v:minimal` passes. `dotnet build rpg.csproj -maxcpucount:2 -v:minimal` passes with 0 warnings and 0 errors. H20 sub-agent review is pending.
- 2026-06-21: Core Slice H20 sub-agent review found one Important guard gap: H19/H20 guards scanned only `BattleRuntimeTickResolver.cs` rather than all resolver partials. Follow-up hardening now scans `BattleRuntimeTickResolver*.cs`, and the H20 post-boundary occupancy wording was corrected to movement-boundary occupancy.
- 2026-06-21: Core Slice H21 RED evidence added the runtime decision phase coordinator guard. The RED run failed `runtime decision phase coordination is service owned` because `BattleRuntimeDecisionPhaseCoordinator` did not exist and `BattleRuntimeTickResolver` still directly owned tick-start tactical observation, fact projection, decision-ready filtering, decision-context construction, decision-outcome application, and movement-continuation context construction.
- 2026-06-21: Core Slice H21 implementation added `BattleRuntimeDecisionPhaseCoordinator.AdvanceDecisionPhase(...)` and routed `BattleRuntimeTickResolver` through it. The coordinator owns tactical observation refresh, tick-start fact projection, decision-ready filtering, context building, decision outcome application, and movement-continuation context construction. `BattleRuntimeTickResolver` now retains only high-level action phase, decision phase, resolution entry, and diagnostics ordering.
- 2026-06-21: Core Slice H21 sub-agent specification review found no blocking issues. Code-quality review found Important fail-fast and hidden-fallback gaps: required decision-phase inputs could be treated as null/empty, and `BattleRuntimeTickResolver` created a hidden default AI executor. Follow-up guards now require explicit `ArgumentNullException.ThrowIfNull(...)`, reject null-as-empty movement/skill actor id filtering, scan resolver partials recursively, and require the session/controller layer to pass an explicit executor.
- 2026-06-21: Core Slice H21 verification: `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal` passes the H21 decision-phase coordinator guard, H20 action-phase coordinator guard, H19 ability-tick coordinator guard, movement/skill/attack/report/settlement regressions, but exits 1 on the pre-existing unrelated oversized guard `tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegressionCases.PresentationAntiRot.cs:1123`. Broader verification passed: `dotnet run --project tests\BattleHitFeedbackRegression\BattleHitFeedbackRegression.csproj -v:minimal`; `dotnet build rpg.csproj -maxcpucount:2 -v:minimal` with 0 warnings and 0 errors; `git diff --check` with only pre-existing CRLF warnings in `tests\BattleHitFeedbackRegression\BattleHitFeedbackRegressionCases.BattleResult.cs` and `tests\BattleHitFeedbackRegression\BattleHitFeedbackRegressionCases.WorldText.cs`.
- 2026-06-21: Core Slice H22 RED evidence added the runtime resolution phase coordinator guard. The RED run failed `runtime resolution phase coordination is service owned` because `BattleRuntimeResolutionPhaseCoordinator` did not exist and `BattleRuntimeTickResolver` still directly entered attack engagement, movement commit, stale-target retarget callback creation, movement cleanup, and movement/no-move performance counters.
- 2026-06-21: Core Slice H22 implementation added `BattleRuntimeResolutionPhaseCoordinator.AdvanceResolutionPhase(...)` and routed `BattleRuntimeTickResolver` through it. The coordinator owns post-decision attack-engagement entry, movement-commit entry, stale-target retarget callback creation, movement resolve timing counters, no-move counters, and movement-chain cleanup while leaving `BattleAttackEngagementCoordinator`, `BattleMovementCommitResolver`, `BattleMovementCommitBoundary`, `BattleStaleAdvanceRetargeting`, and `BattleMovementController` as the narrow lower-level owners.
- 2026-06-21: Core Slice H22 sub-agent review found one Important hidden-fallback issue: required resolution-phase inputs were silently converted to `BattleRuntimeResolutionPhaseResult.Empty`. Follow-up fixes made the resolution phase fail fast on required inputs and hardened H10/H12/H22 guards to scan `BattleRuntimeTickResolver*.cs` recursively.
- 2026-06-21: Core Slice H22 verification: `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal` passes H22, H21 review hardening, H20, H19, movement, skill, attack, report, and settlement regressions, but exits 1 on the pre-existing unrelated oversized guard `tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegressionCases.PresentationAntiRot.cs:1123`. Broader verification passed: `dotnet run --project tests\BattleHitFeedbackRegression\BattleHitFeedbackRegression.csproj -v:minimal`; `dotnet build rpg.csproj -maxcpucount:2 -v:minimal` with 0 warnings and 0 errors; `git diff --check` with only pre-existing CRLF warnings in `tests\BattleHitFeedbackRegression\BattleHitFeedbackRegressionCases.BattleResult.cs` and `tests\BattleHitFeedbackRegression\BattleHitFeedbackRegressionCases.WorldText.cs`.
- 2026-06-21: Core Slice H23 RED evidence added the runtime action diagnostics boundary guard. The RED run failed because the deleted TickResolver diagnostics script still had a stale `.uid` metadata file and the guard did not yet enforce the final no-diagnostics-in-TickResolver boundary.
- 2026-06-21: Core Slice H23 implementation added `BattleRuntimeActionDiagnostics.LogTickActionResults(...)`, routed `BattleRuntimeTickResolver` through that boundary, deleted `BattleRuntimeTickResolver.Diagnostics.cs`, and removed the stale `.uid`. The diagnostics boundary preserves unresolved-action fallback assignment, `BattleRuntimeAction` trace text, attack-charge wait suppression, and deterministic actor-id log ordering while keeping TickResolver as high-level phase orchestration only.
- 2026-06-21: Core Slice H23 sub-agent review found two Important fail-fast issues: missing contexts were treated as empty in `BattleRuntimeActionDiagnostics` and `BattleRuntimeResolutionPhaseResult`. Follow-up guards now require `ArgumentNullException.ThrowIfNull(contexts)`, reject `contexts ??` normalization, reject direct `GameLog.Trace` and `BattleRuntimeAction battle=` diagnostics inside `BattleRuntimeTickResolver*.cs`, and verify no deleted diagnostics script or `.uid` remains. Follow-up sub-agent review approved the H23 fixes.
- 2026-06-21: Core Slice H23 verification: `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -maxcpucount:2 -v:minimal` passes H23, H22, H21, H20, ability, movement, skill, attack, report, and settlement regressions, but exits 1 on the pre-existing unrelated oversized guard `tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegressionCases.PresentationAntiRot.cs:1123`. Broader verification passed: `dotnet run --project tests\BattleHitFeedbackRegression\BattleHitFeedbackRegression.csproj -v:minimal`; `dotnet build rpg.csproj -maxcpucount:2 -v:minimal` with 0 warnings and 0 errors; `git diff --check` with only pre-existing CRLF warnings in `tests\BattleHitFeedbackRegression\BattleHitFeedbackRegressionCases.BattleResult.cs` and `tests\BattleHitFeedbackRegression\BattleHitFeedbackRegressionCases.WorldText.cs`.
- 2026-06-21: Scope closure decision: H23 is the final planned structural slice for this proposal. The proposal remains active only for final acceptance verification and requested manual QA evidence; additional H-numbered slices require a concrete acceptance violation rather than optional further decomposition.
- 2026-06-21: Final acceptance bugfix: latest runtime logs showed player upper-route movement stayed logically continuous, while visual stutter appeared after combat-join retargeting when committed one-cell movement events arrived slightly later than the fixed visual step. Presentation movement lanes now keep their continuation hold based on the last committed step duration, bounded at 0.32s, so route rebuild jitter does not restart the visual lane every cell. This is not a new H slice and does not change Runtime movement, target selection, pathfinding, or commit authority. Verification: `dotnet run --project tests\BattleHitFeedbackRegression\BattleHitFeedbackRegression.csproj -v:minimal` passes; `dotnet build rpg.csproj -maxcpucount:2 -v:minimal` passes with 0 warnings and 0 errors.
- 2026-06-21: Final acceptance tuning: temporary target-region refresh changed from 5 Runtime ticks to 50 Runtime ticks, about two seconds at the accepted fixed simulation cadence. This keeps non-engaged movement from re-aiming toward moving enemy clusters every few cells while preserving immediate reselection after an empty reached region clears its selected target. Verification: `temporary region refresh interval defaults to about two seconds` passes in `tests\TargetBattleArchitectureRegression`; the suite still exits 1 only on the pre-existing unrelated oversized guard `tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegressionCases.PresentationAntiRot.cs:1123`. `dotnet build rpg.csproj -maxcpucount:2 -v:minimal` passes with 0 warnings and 0 errors.

## Design Gate

Stop and create a design proposal before implementation if this work requires any of the following:

- changing accepted square-grid Runtime spatial authority;
- making Presentation authoritative for combat timing or state;
- replacing battle Runtime with Godot scene-node truth;
- changing command channels or battle-group ownership rules;
- changing settlement/report contracts;
- introducing a full ECS or multithreaded simulation architecture;
- adding new gameplay rules beyond refactoring execution ownership and pause semantics.
