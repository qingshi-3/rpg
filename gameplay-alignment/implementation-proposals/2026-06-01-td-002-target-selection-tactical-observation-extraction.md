# TD-002 Target Selection, Tactical Observation, And Engagement Extraction

Status: Reviewed - slices A/B approved for phased implementation; slice C approved as design but requires explicit ownership sign-off before coding (reviewer: Claude, 2026-06-01)

## Requirement / Authority

Requirement: plan the TD-002 extraction of target selection, AI request shaping, and tactical observation / engagement update responsibilities out of `BattleRuntimeTickResolver` while preserving current behavior unless a later accepted ownership decision explicitly changes it.

Authority and routing:

- `gameplay-design/content-systems-long-term-design.md`: hero-led light RTS combat, medium-frequency command, plan-scoped autonomy, local combat response, and reportable battle facts.
- `system-design/hero-led-light-rts-system-architecture.md`: Runtime owns live combat truth and emits the facts consumed by settlement and reports.
- `system-design/battle-runtime-architecture.md`: Runtime owns actor target locks, actor phases, tactical observations, battle-group tactical regions, engagement state, semantic events, and battle outcome.
- `system-design/battle-ai-boundary-architecture.md`: tactical AI selects targets and typed intent inside player command / plan scope, but Runtime remains final validator for movement, attacks, events, damage, and outcome.
- `system-design/battle-group-tactical-region-architecture.md`: target regions, temporary regions, local combat regions, and engagement state are battle-group-owned runtime facts; enemy policy must not overwrite player command intent.
- `system-design/battle-command-architecture.md`: target choice and plan-state events must remain attributable to command or battle-plan scope.
- `gameplay-alignment/tech-debt-register.md` row `TD-002`: target choice should move to `BattleTargetSelectionService`; perception / engagement mutation should move to a tactical observation or engagement updater; a guard must prevent direct resolver calls to the tactical builders / state machine.

This proposal is an implementation proposal only. It does not change accepted gameplay, target policy, engagement rules, tactical-region policy, or Runtime event semantics.

## Current Verified Shape

TD-003 has already extracted attack and movement resolution. `BattleRuntimeTickResolver` now delegates attack mutation to `BattleAttackResolver` and movement commit mutation to `BattleMovementCommitResolver`, with event-order goldens and a decomposition guard protecting that seam.

The remaining TD-002 surface has three different risk profiles:

| Slice | Area | Current Shape | Nature |
|---|---|---|---|
| A | Target selection | `BattleRuntimeTickResolver.Targeting.cs` contains private static `Find*EnemyCorps` helpers and assault scoring helpers. They read tick-start facts, navigation, occupancy, flow-field cache, and performance counters, then return a target fact. | Pure engineering refactor, behavior unchanged. |
| B | AI request construction | `BattleRuntimeTickResolver.Ai.cs` builds command-scoped `BattleRuntimeAiActionRequest` and `BattleRuntimeAiDecisionFacts`. One branch records advance failure for outside-leash degradation. | Pure engineering refactor, behavior unchanged, but not pure logic because it preserves one existing failure-state mutation. |
| C | Perception / tactical region / engagement updater | `BattleRuntimeTickResolver.Perception.cs` builds perception summaries, applies engagement transitions, clears target locks after engagement exits, refreshes local combat regions, refreshes temporary target regions, and appends emitted events. `BattleRuntimeTickResolver.Engagement.cs` still applies post-attack member-action engagement transitions. | Architecture-ownership change. It changes who is allowed to mutate engagement / tactical-region state and must have explicit ownership review before implementation. |

The important TD-002 boundary is that selection helpers do not currently write `actor.TargetActorId` and do not append `target_locked` plan events. Those side effects happen in the resolver after a `BattleRuntimeTickContext` has been built. Slice A must keep that split.

## Scope

### Slice A - Target Selection

Extract current target-choice helpers into a Runtime-owned target selection service:

- `FindEnemyCorpsForCommand`;
- `FindLowestHealthEnemyCorps`;
- `FindImmediateAttackOpportunityEnemyCorps`;
- `FindNearestEnemyCorps`;
- `FindRetainedEnemyCorps`;
- `FindRouteBlockingEnemyCorps`;
- `FindPlanScopedEnemyCorps`;
- `FindFastestAttackOpportunityEnemyCorps`;
- `ScoreAssaultTarget`;
- `ResolveAttackOpportunityTravelCost`;
- `IsBetterAssaultTarget`;
- closely related pure selection helpers that are only needed by those methods.

This service must preserve the existing command and engagement-rule branch order byte-for-byte in behavior:

```text
focus-fire
-> hold-line
-> authored Hold plan
-> objective anchor not reached
-> objective anchor reached
-> default immediate / retained / fastest attack opportunity / nearest fallback
```

`FindFastestAttackOpportunityEnemyCorps` must carry the existing `performanceCounters?.RecordTargetScoringElapsedTicks(...)` behavior with it. Moving that counter out of the scoring branch would change diagnostics and performance acceptance.

### Slice B - AI Request Construction

Optionally extract command-scoped AI request construction into a separate Runtime-owned builder after slice A is stable:

- `BuildCommandScopedAiActionRequest`;
- `BuildAiDecisionFacts`;
- local-combat decision fact projection;
- command-specific request fallbacks such as move-first objective advance, region advance, hold-line out-of-range hold, and missing-AI fallback.

This service must not be merged into target selection. Target selection returns candidates; AI request construction chooses typed intent using those candidates, local combat facts, command scope, and the AI executor.

The existing outside-leash branch records advance failure. If slice B is extracted, that mutation must be explicit in the builder contract, either through a narrow callback to the existing advance-failure recorder or through a small Runtime-owned failure recorder. Do not let the target selection service write failure state.

### Slice C - Tactical Observation / Engagement Updater

Plan, but do not implement without explicit ownership approval, a `BattleTacticalObservationUpdater`-style service that owns the current perception / engagement / tactical-region refresh choreography.

The updater should cover:

- ordered living corps capture for tactical observation;
- `state.GroupPerceptionSummaryStore` refresh;
- perception-driven engagement enter / exit transitions;
- post-attack member-action engagement transitions;
- local combat region refresh for engaged groups;
- enemy temporary target-region refresh;
- appending the exact events produced by those transitions / mutations to the original `BattleEventStream`;
- engagement-exit target-lock clearing through a narrow target-lock lifecycle boundary.

The updater does not own attack resolution, movement commit, damage, defeat, victory / defeat outcome, settlement, command validation, navigation legality, or AI target scoring.

## Proposed Service Boundary

### `BattleTargetSelectionService` - Slice A

Responsibility: choose candidate enemy corps from immutable tick-start facts and read-only navigation / cache inputs.

Contract shape:

```text
SelectEnemyForCommand(
    IReadOnlyDictionary<string, BattleRuntimeTickStartActorFact> facts,
    BattleRuntimeTickStartActorFact actorFact,
    BattleNavigationGraph navigationGraph,
    BattleDynamicOccupancy occupancy,
    BattleFlowFieldCache flowFields,
    BattlePerformanceCounters performanceCounters)
    -> BattleRuntimeTickStartActorFact?

SelectRegionScopedEnemy(
    IReadOnlyDictionary<string, BattleRuntimeTickStartActorFact> facts,
    BattleRuntimeTickStartActorFact actorFact)
    -> BattleRuntimeTickStartActorFact?
```

Mutation authority:

- none;
- no actor field writes;
- no `BattleEventStream` writes;
- no `BattleGroupTacticalStateStore` reads or writes;
- no plan-state emission.

Allowed diagnostics:

- may record target-scoring elapsed ticks in the same branch that currently records it;
- must not add new logs, counters, or reason codes in slice A.

Non-authority:

- does not decide whether a request becomes hold, attack, advance-to-objective, advance-to-region, join-local-combat, hold-support, or return-to-objective;
- does not set `TargetActorId`;
- does not emit `target_locked`;
- does not own local combat-region refresh or engagement state.

Implementation rule: copy current branch structure and tie-breakers directly. Do not "simplify" focus-fire, hold-line, move-first, objective, retained-target, route-blocking, or fastest-assault ordering while extracting.

### `BattleAiActionRequestBuilder` - Slice B

Responsibility: convert the selected target, local combat situation, region movement goal, and command facts into a typed `BattleRuntimeAiActionRequest`.

Contract shape:

```text
BuildCommandScopedRequest(
    BattleRuntimeTickStartActorFact actorFact,
    BattleRuntimeTickStartActorFact? targetFact,
    LocalCombatSituation localCombatSituation,
    BattleRegionMovementGoal regionMovementGoal,
    IBattleRuntimeAiExecutor aiExecutor,
    RecordAdvanceFailureCallback recordAdvanceFailure)
    -> BattleRuntimeAiActionRequest

BuildDecisionFacts(
    BattleRuntimeTickStartActorFact actorFact,
    BattleRuntimeTickStartActorFact? targetFact,
    LocalCombatSituation localCombatSituation)
    -> BattleRuntimeAiDecisionFacts
```

Mutation authority:

- `BuildDecisionFacts` is pure;
- `BuildCommandScopedRequest` may preserve the existing outside-leash `RecordAdvanceFailure(actor, RejectOutsideLeash)` side effect only through an explicit failure-recorder boundary;
- no target-lock writes;
- no tactical-state-store writes;
- no event writes.

Non-authority:

- does not choose the target candidate;
- does not build local combat regions;
- does not validate movement, attack, or pathing;
- does not append diagnostics beyond the existing failure-state mutation.

Slice B is optional because leaving this builder in the resolver does not block slice A. If extracted, keep it as a separate PR / slice so selection behavior and request-shaping behavior can be reviewed independently.

### `BattleTacticalObservationUpdater` - Slice C

Responsibility: own tactical observation refresh and engagement / tactical-region mutation choreography for Runtime.

Proposed contract shape:

```text
RefreshAtTickStart(
    BattleRuntimeState state,
    BattleEventStream stream,
    string battleId,
    int tick,
    double currentTimeSeconds)
    -> BattleTacticalObservationUpdateResult

ApplyPostAttackEngagementTriggers(
    BattleRuntimeState state,
    IReadOnlyList<BattleEvent> attackEvents,
    BattleEventStream stream,
    string battleId,
    int tick,
    double currentTimeSeconds)
```

`BattleTacticalObservationUpdateResult` should at minimum expose the ordered `LivingCorps` array that the resolver currently receives from `CaptureLivingCorpsAndRefreshPerceptionSummaries`. It may also expose the engagement events appended during the refresh for diagnostics / tests, but the original stream remains the authoritative event sink.

Mutation authority:

- may set `state.GroupPerceptionSummaryStore`;
- may call `BattleGroupTacticalStateStore` internal write methods for engagement state, local combat region, temporary target region, and related trigger tick counters;
- may append tactical observation / engagement / region events to the passed `BattleEventStream`;
- may clear actor target locks only by delegating to the target-lock lifecycle boundary described below.

Store write boundary:

- keep `BattleGroupTacticalStateStore.TrySetLocalCombatRegion`, `TrySetTemporaryRegion`, `TryApplyEngagementState`, `RecordNoPerceivedHostileTick`, `ResetNoPerceivedHostileTicks`, `RecordMemberDamageTriggerTick`, and `RecordMemberAttackTriggerTick` as `internal`;
- place the updater in the same Runtime assembly so it can call those internal methods without making tactical-state writes public;
- do not add public tactical-state mutators just to make extraction easy;
- tests should observe through Runtime session output, captured tactical snapshots, and event streams, not by becoming a second writer.

Event emission ownership:

- the updater appends to the original `BattleEventStream` at the same logical call sites and in the same order as today;
- the updater must not return unordered event lists for the resolver to append later;
- `BattleGroupTacticalStateStore` and `BattleGroupEngagementStateMachine` may still construct event objects, but the updater owns when those events are appended to the Runtime stream;
- no event may be inserted between TD-003 attack resolution and the post-attack engagement slice except the current attack events.

Target-lock exit coupling:

Current code clears `actor.TargetActorId` when engagement exits with `EngagementExitNoGroupPerception`. That is a reverse coupling from engagement to targeting.

Proposed ownership decision for review:

- `BattleGroupEngagementStateMachine` must not mutate actors;
- `BattleTargetSelectionService` must not mutate actors;
- introduce or reuse a narrow Runtime target-lock lifecycle helper, for example `BattleTargetLockLifecycle.ClearForEngagementExits(livingCorps, engagementEvents)`;
- `BattleTacticalObservationUpdater` calls that helper immediately after perception transition events are produced and before those events are appended, preserving current mutation / event order;
- the helper owns the direct `actor.TargetActorId = ""` writes for engagement exits only;
- ordinary target lock assignment and `target_locked` plan events remain in the resolver orchestration layer until a separate target-lock lifecycle extraction is proposed.

This is the slice C ownership decision that needs review before implementation. If reviewers reject it, slice C must stop and return to a design proposal / ownership repair path instead of moving code locally.

## Orchestration Flow

### After Slice A Only

`BuildTickContext` remains in the resolver and delegates only candidate selection:

```text
ResolveRegionMovementGoal / ResolveEngagedLocalCombatRegion
-> optionally filter facts to local combat region
-> BattleTargetSelectionService.SelectEnemyForCommand or SelectRegionScopedEnemy
-> LocalCombatSituationBuilder.Build
-> BuildCommandScopedAiActionRequest
-> ResolveRequestedTarget
-> Build movement / attack / hold context
```

Target-lock mutation remains after context construction:

```text
context.TargetFact != null
-> resolver writes actor.TargetActorId
-> resolver emits target_locked through BattlePlanStateEmitter when target changed
```

### After Slice B

Only request construction changes:

```text
selected target + local combat situation + region movement goal
-> BattleAiActionRequestBuilder.BuildCommandScopedRequest
-> ResolveRequestedTarget
```

The resolver still owns context assembly, target-lock assignment, plan-state emission, and final action-result logging.

### After Slice C

The resolver becomes a coordinator for observation and phase services:

```text
advance movement-completion boundaries
-> BattleTacticalObservationUpdater.RefreshAtTickStart(...)
-> build tickStartFacts from returned living corps
-> BuildTickContext
-> pre-resolution hold / target-lock / objective-plan handling
-> ResolveAttackProposalsAndEngagementTriggers
   -> record firstAttackEventIndex
   -> BattleAttackResolver.Resolve(...)
   -> slice stream[firstAttackEventIndex..] for DamageApplied
   -> BattleTacticalObservationUpdater.ApplyPostAttackEngagementTriggers(...)
-> BattleMovementCommitResolver.Resolve(...)
-> unresolved-action fill and action-result diagnostics
```

The TD-003 attack-before-movement order stays unchanged. Post-attack engagement trigger application remains between attack and movement, but direct calls to `BattleGroupEngagementStateMachine` move out of the resolver.

## Event And Mutation Order Locks

Slice A and B must not append events.

Slice C must preserve the current tick-start observation order:

1. Build ordered living corps.
2. Build group perception summaries.
3. Assign `state.GroupPerceptionSummaryStore`.
4. Apply perception engagement transitions through the engagement state machine.
5. Clear target locks for engagement exits through the target-lock lifecycle helper.
6. Append perception engagement events.
7. Build and apply local combat region changes for engaged groups in battle-group id order.
8. Append local combat region change events at the current call site.
9. Build and apply enemy temporary target-region changes in battle-group id order.
10. Append temporary target-region events at the current call site.

Slice C must also preserve the post-attack order:

```text
BattleAttackResolver appends damage / attacking / defeated events
-> resolver slices new DamageApplied events
-> BattleTacticalObservationUpdater applies member-action engagement transitions
-> updater appends resulting engagement events
-> BattleMovementCommitResolver may append movement events
```

This order is important because reports, diagnostics, and existing goldens treat stream insertion order as the semantic order.

## Suggested Cut Sequence

Do not extract all of TD-002 in one PR.

1. Add focused tests for slice A before moving target selection code.
2. Extract `BattleTargetSelectionService` mechanically. Preserve branch order, tie-breakers, retained-target priority, route-blocking behavior, and target-scoring counters.
3. Optionally add focused tests for slice B and extract `BattleAiActionRequestBuilder`. Keep its failure-state mutation explicit and keep it separate from target selection.
4. Stop for explicit slice C ownership review. The review must decide whether `BattleTacticalObservationUpdater` becomes the Runtime-owned mutator for engagement / tactical-region store writes and whether engagement-exit target-lock clearing is routed through the proposed target-lock lifecycle helper.
5. Add slice C goldens and source guards before moving perception / engagement / region update code.
6. Extract `BattleTacticalObservationUpdater` in small steps:
   - tick-start perception summary and engagement transition append;
   - engagement-exit target-lock lifecycle helper;
   - local combat region refresh;
   - temporary target-region refresh;
   - post-attack member-action engagement transition entry point.
7. Extend the decomposition guard so the resolver no longer directly calls tactical observation builders / state machine collaborators.

Why A and C must not share a PR:

- slice A is a behavior-equivalent pure selection extraction; slice C changes mutation ownership and event append ownership;
- slice A failures are usually "selected the wrong target"; slice C failures can be event-order, store-version, target-lock, and engagement-state failures;
- rollback for A should be a simple service extraction revert, while rollback for C may need ownership-boundary repair;
- mixing them would make golden failures ambiguous and could hide a target-selection behavior change behind a tactical-state mutation change.

## Test Strategy

Do not rely only on the existing event-order goldens. They are valuable TD-003 safety nets, but TD-002 needs tests that name target-selection and tactical-observation outcomes directly.

### Slice A Tests Before Extraction

Add deterministic tests that assert which target is selected or locked:

- fastest-assault scoring: arrange at least two valid enemies where nearest / lexicographic choice differs from lowest travel-cost attack opportunity; assert the selected target id and that target-scoring elapsed ticks are recorded.
- plan-scoped selection: with an objective anchor not reached, assert ordinary movement does not global-score far attack slots and selects only immediate / retained / planned-local candidates according to the current branch.
- route-blocking selection: under move-first objective movement, arrange a blocker on the objective corridor and a non-blocking enemy with a tempting distance; assert the blocker is selected.
- retained target stickiness: while marching with a live retained target, assert the retained target survives incidental nearest-enemy changes unless immediate opportunity or rule branch allows reacquisition.
- focus-fire and hold-line guard coverage may reuse existing tests if they already assert the correct selected target, but the TD-002 slice should name them in the test list so future maintainers see the full command-policy matrix.

### Slice B Tests Before Optional Extraction

Add or isolate tests for request shaping:

- outside-leash local combat produces `Hold` with `RejectOutsideLeash` and records the same advance-failure state.
- move-first objective branch returns `AdvanceTowardObjective` when the target is absent or out of range and the objective is not reached.
- region movement branch returns `AdvanceTowardRegion` when no target is selected and a region movement goal exists.
- hold-line out-of-range branch returns `Hold` with `hold_line_out_of_range`.
- decision facts expose local combat situation ids, region ids, local target id, reachable slot flags, route-blocking flag, leash flag, version, and reason codes unchanged.

### Slice C Tests Before Extraction

Add goldens or focused regression tests before moving any updater code:

- region refresh event golden: a tick that enters engagement, builds a local combat region, and appends `BattleGroupLocalCombatRegionChanged` in the current order.
- temporary target-region event golden: fixed target region is empty, enemy offense / active defense builds a temporary target region, and the selected-region event precedes region movement use.
- engagement enter / exit golden: group perception enters engagement, no-perception grace exits engagement with `EngagementExitNoGroupPerception`, and event ids / stable projections match current order.
- engagement exit clears target locks: an engaged group with member `TargetActorId` values exits due no perception; assert target locks are cleared and no unrelated group target locks are changed.
- post-attack hold-defense activation golden: damage / attack events from the TD-003 attack slice activate hold defense before movement events.
- no-perception grace test: recent member damage / attack trigger prevents immediate exit until the current grace rule allows it.

### Architecture Guards

Extend `TargetBattleTickResolverDecompositionGuard` or add a TD-002-specific guard after slice C:

- `BattleTacticalObservationUpdater` source file exists.
- `BattleRuntimeTickResolver*.cs` does not contain direct calls to:
  - `BattleGroupPerceptionSummaryBuilder.BuildForGroups`;
  - `BattleGroupEngagementStateMachine.ApplyPerceptionTransitions`;
  - `BattleGroupEngagementStateMachine.ApplyMemberActionTransitions`;
  - `BattleLocalCombatRegionBuilder.BuildForGroup`;
  - `BattleTemporaryTargetRegionBuilder.BuildForGroup`.
- `BattleRuntimeTickResolver*.cs` no longer contains `CaptureLivingCorpsAndRefreshPerceptionSummaries`, `RefreshEngagedLocalCombatRegions`, `RefreshEnemyTemporaryTargetRegions`, or `ClearTargetLocksForEngagementExits`.
- direct engagement-exit target-lock clearing is confined to the target-lock lifecycle helper, not the engagement state machine and not target selection.

Keep the TD-003 guard intact. TD-002 must not move attack or movement mutation back into the resolver.

## Diagnostics And Manual QA

Diagnostics:

- preserve existing target-scoring counters;
- preserve existing action-result diagnostics after context resolution;
- preserve existing event ids, reason codes, runtime tick values, runtime time values, and stream insertion order;
- do not add per-frame or high-frequency logs.

Manual QA is not the primary proof for TD-002 because target selection and event order are better locked by headless deterministic tests. If reviewers request a manual pass after implementation, use a small battle with:

- one move-first objective group;
- one route blocker;
- one farther but faster assault target;
- one hold-defense enemy group that enters and exits engagement;
- one fixed region that becomes empty and causes temporary-region movement.

Confirm that visible movement still follows the event stream and that engagement exit stops stale target pursuit.

No build, test, or manual QA is run for this design draft.

## Non-Goals

- Do not change target selection policy. Copy command strategy branches and tie-breakers; do not reorganize them.
- Do not change engagement rules, local perception range, disengage grace, temporary-region refresh interval, or local-combat region scoring.
- Do not change damage, attack cadence, movement cadence, defeat, victory / loss, settlement, or report generation.
- Do not touch TD-003 extracted attack / movement services except to call them in the same order.
- Do not make `BattleGroupTacticalStateStore` write APIs public as a shortcut.
- Do not introduce duplicate authoritative implementations, compatibility branches, or temporary fallback target policies.
- Do not move behavior-tree / LimboAI ownership or presentation debug overlay behavior.

## Risks And Rollback

Risk: slice A changes selected targets because branch order or tie-breakers are "cleaned up."

Rollback: revert the target-selection service extraction and keep the focused tests. Reattempt by direct method-body movement without policy edits.

Risk: target-scoring performance counters stop recording or record in extra branches.

Rollback: restore the counter call inside the fastest-assault scoring `finally` path and rerun the targeted performance-counter test.

Risk: slice B hides an advance-failure mutation inside target selection or loses the outside-leash failure state.

Rollback: keep B in the resolver or pass the existing failure recorder explicitly. Do not let selection services mutate failure state.

Risk: slice C changes event order by buffering events and appending them later.

Rollback: make the updater append to the original stream at the current call sites, or revert the C slice. Do not add event sorting.

Risk: slice C weakens `BattleGroupTacticalStateStore` ownership by making internal writes public.

Rollback: return the updater to the Runtime assembly / namespace that can use existing internal methods. Public writes require a separate accepted architecture change.

Risk: engagement-exit target-lock clearing remains a hidden engagement-to-targeting mutation.

Rollback: isolate the direct `TargetActorId` writes in the target-lock lifecycle helper and assert that the engagement state machine only returns events / transition facts.

Risk: slice C ownership review rejects the proposed updater boundary.

Rollback: do not implement C. Keep A and B if already accepted and behavior-equivalent, then open a design proposal for tactical observation / engagement ownership.

## Review Checklist

- Status remains `Draft - pending review`.
- The document splits TD-002 into slices A, B, and C.
- Slice A is marked as behavior-equivalent target selection extraction with no mutation or events.
- Slice B is marked as optional behavior-equivalent AI request construction extraction, separate from target selection.
- Slice C is marked as an architecture-ownership change requiring explicit ownership approval before implementation.
- Slice C defines updater responsibility, store internal write access, event append ownership, and engagement-exit target-lock ownership.
- Target-lock assignment and `target_locked` plan events remain outside target selection.
- Test strategy lists fastest-assault scoring, plan-scoped selection, route-blocking selection, region refresh events, engagement enter / exit, and target-lock clearing before implementation.
- The cut sequence runs A first, B optional, C last after ownership review.
- The design explains why A and C must not be mixed in one PR.
- Non-goals exclude target-policy changes, engagement-rule changes, damage / victory changes, and TD-003 attack / movement service changes.
- No code, build, or test execution is part of this design draft.

## Reviewer Verification (2026-06-01)

Per `.codex/collaboration.md`, the design is reviewed by Claude before implementation. A sub-agent fact-checked all 11 claims this design makes about current code against the real source (line-by-line). All 11 are accurate; no false assumption that would invalidate the plan. Load-bearing facts confirmed:

- All 11 `Find*`/scoring helpers exist as `private static` pure functions in `Targeting.cs` (no actor write, no event, no store write); `TargetActorId` writes and `target_locked` emission live in the resolver orchestration layer (`BattleRuntimeTickResolver.cs:122,142,155,173` and `:159-166`), not in the helpers — so slice A's "extract pure selection, keep side effects in the orchestrator" split is real.
- **Slice C's ownership decision rests on a verified fact**: the seven `BattleGroupTacticalStateStore` write methods (`TrySetLocalCombatRegion:127`, `TrySetTemporaryRegion:105`, `TryApplyEngagementState:251`, `RecordNoPerceivedHostileTick:282`, `ResetNoPerceivedHostileTicks:294`, `RecordMemberDamageTriggerTick:303`, `RecordMemberAttackTriggerTick:314`) are all `internal`. So placing the updater in the same Runtime assembly to call them — without making writes public — is sound.
- The engagement→targeting reverse coupling is real: `ClearTargetLocksForEngagementExits` (`Perception.cs:178-198`) writes `actor.TargetActorId = ""` filtered by `EngagementExitNoGroupPerception` (`:184,:196`). Routing this through the proposed `BattleTargetLockLifecycle` boundary is a genuine ownership cleanup, not a cosmetic move.
- The four tactical builders and the post-attack `ApplyMemberActionTransitions` are all currently called directly from the resolver, so the proposed decomposition guard will be meaningful after extraction.

One harmless wording fix for the implementer (no plan impact): in slice A's command-dispatch order, the `objective not reached → objective reached` sub-branches are written in the reverse textual order from the code (the `IsObjectiveReached` branch is the `if`, not-reached is the `else`, `Targeting.cs:40-54`). These are mutually exclusive sub-branches under one `HasObjectiveAnchor` condition — no behavior difference. Implement by following the real code structure; the design's "copy branches verbatim, do not reorganize" rule already governs this.

Decision:
- **Slices A and B are approved for phased implementation** (A first, then optionally B), each preceded by the focused selection / request-shaping tests listed in the Test Strategy. They are behavior-equivalent extractions guarded by new targeted tests plus the existing event-order goldens.
- **Slice C is approved as a design** but is an architecture-ownership change. Before any slice C code, it needs an explicit ownership sign-off confirming: the updater becomes the Runtime-owned mutator for engagement / tactical-region store writes; store write APIs stay `internal`; engagement-exit target-lock clearing routes through `BattleTargetLockLifecycle`; and the slice-C goldens (region refresh, temporary region, engagement enter/exit, target-lock clearing, post-attack hold-defense activation) land before the code moves.

## Slice C Ownership Sign-Off (2026-06-01)

The user signed off on the slice-C ownership decision. Approved for implementation under these confirmed ownership rules:

- `BattleTacticalObservationUpdater` becomes the single Runtime-owned mutator for tick-start tactical observation: group perception summary refresh, perception-driven engagement enter/exit transitions, post-attack member-action engagement transitions, local-combat-region refresh, and enemy temporary-target-region refresh.
- `BattleGroupTacticalStateStore` write methods stay `internal`. The updater lives in the same Runtime assembly and calls them directly; no public tactical-state mutators are added as an extraction shortcut.
- Engagement-exit target-lock clearing (currently `ClearTargetLocksForEngagementExits`) moves behind a narrow `BattleTargetLockLifecycle` boundary that owns the `actor.TargetActorId = ""` writes for engagement exits only. The engagement state machine returns events/transition facts and must not mutate actors; target selection must not mutate actors. Ordinary target-lock assignment and `target_locked` emission stay in the resolver orchestration layer (a later target-lock lifecycle extraction may revisit that).
- The updater appends events to the original `BattleEventStream` at the current call sites and in the current order; no event buffering/reordering; nothing writes the stream between TD-003 attack resolution and the post-attack engagement slice except the current attack events.

Execution order (per the design's cut sequence):
1. Land the slice-C goldens first (region refresh, temporary region, engagement enter/exit, target-lock clearing, post-attack hold-defense activation).
2. Extract `BattleTacticalObservationUpdater` in small steps, byte-for-byte equivalent, verified per step.
3. Extend the decomposition guard so the resolver no longer directly calls the four tactical builders / engagement state machine, and no longer contains `CaptureLivingCorpsAndRefreshPerceptionSummaries`, `RefreshEngagedLocalCombatRegions`, `RefreshEnemyTemporaryTargetRegions`, or `ClearTargetLocksForEngagementExits`. That guard marks TD-002 Closed.
