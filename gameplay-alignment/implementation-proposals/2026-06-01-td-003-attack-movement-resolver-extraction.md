# TD-003 Attack And Movement Resolver Extraction

Status: Reviewed - approved for phased implementation (reviewer: Claude, 2026-06-01)

## Requirement / Authority

Requirement: finish the remaining TD-003 runtime extraction by moving `ResolveAttackProposals` and `ResolveMovementProposals` out of `BattleRuntimeTickResolver` into explicit phase services while preserving byte-for-byte runtime behavior.

Authority and routing:

- `gameplay-design/content-systems-long-term-design.md`: hero-led light RTS combat, medium-frequency command, automatic local behavior, and reportable battle facts.
- `system-design/hero-led-light-rts-system-architecture.md`: Runtime owns live combat truth and emits the facts consumed by settlement and reports.
- `system-design/battle-runtime-architecture.md`: same-tick damage batches resolve before movement mutations; defeated actors cannot move in the same tick; Runtime owns actor phases, attack, damage, movement, defeat, and event emission.
- `system-design/battle-navigation-topology-architecture.md`: Runtime pathfinding owns occupancy, reservations, next-step validation, footprint legality, and movement failure diagnostics.
- `system-design/battle-ai-boundary-architecture.md`: AI may choose typed intent, but Runtime remains final validator for movement, attacks, damage, events, and outcome.
- `gameplay-alignment/tech-debt-register.md` row `TD-003`: plan-state emission and event construction are already extracted; attack/movement resolver extraction remains open and must preserve semantic events and state transitions exactly.

This proposal is an implementation proposal only. It does not change accepted gameplay or system authority.

## Scope

Extract these current responsibilities from `BattleRuntimeTickResolver`:

- attack proposal validation, same-tick damage batching, attack recovery transition, attack result mutation, and attack/defeat event emission;
- movement proposal validation, stale-target retarget hook, same-tick reservation selection, movement state transition, movement result mutation, movement event emission, and movement diagnostics.

Keep these responsibilities in the main resolver for this TD-003 slice:

- tick orchestration order;
- tick-start fact capture;
- AI request/context construction;
- target selection, tactical observation, perception, and engagement ownership;
- `ResolveAttackProposalsAndEngagementTriggers` stream slicing and engagement-trigger application.

## Proposed Service Boundary

### Shared Tick DTOs

Move the current private nested tick DTOs to internal Runtime/Battle DTOs that both services can consume:

- `BattleRuntimeTickStartActorFact`: current `TickStartActorFact` shape. It stores the live `BattleRuntimeActor` reference plus tick-start anchor, HP, charge, target id, and command id.
- `BattleRuntimeTickContext`: current `TickContext` shape. It remains a mutable reference type. `Request`, `TargetFact`, `Proposal`, and `Result` must be the same object instance observed by attack resolution, movement resolution, and final action-result logging.

The DTO extraction must not copy actors or clone context lists. The tick resolver builds one `Dictionary<string, BattleRuntimeTickStartActorFact>` and one `List<BattleRuntimeTickContext>` per tick, then passes those same instances through the phase services.

`MoveCandidate`, `PendingAttack`, and `AttackApplication` should remain private implementation details inside the movement or attack service unless a later slice proves they need to be shared.

### `BattleAttackResolver`

Responsibility: resolve already-built attack contexts. It does not choose targets and does not build AI requests.

Contract:

```text
Resolve(
    List<BattleRuntimeTickContext> contexts,
    IReadOnlyDictionary<string, BattleRuntimeTickStartActorFact> tickStartFacts,
    BattleEventStream stream,
    string battleId,
    int tick,
    double currentTimeSeconds)
```

Mutation authority:

- may set `context.Result`;
- may update live actor `AttackCharge`;
- may update target live `HitPoints`;
- may call `BattleRuntimeActorStateMachine.MarkHolding`, `MarkWaitingForCharge`, `MarkAttackRecovery`, and `MarkDefeated`;
- may call `BattlePlanStateEmitter.SetPlanState`;
- may append events to the passed `BattleEventStream`;
- may reset advance-failure state through the same helper used today.

Purity:

- not pure. It is a deterministic mutation service over shared runtime state and stream order.
- event construction remains pure in `BattleRuntimeEventFactory`.

Non-authority:

- no target selection;
- no engagement-state transition application;
- no movement reservation or commit;
- no separate event buffering or sorting outside the current loops.

### `BattleMovementCommitResolver`

Responsibility: resolve already-built movement contexts after attack resolution has mutated live HP.

Contract:

```text
int Resolve(
    List<BattleRuntimeTickContext> contexts,
    IReadOnlyDictionary<string, BattleRuntimeTickStartActorFact> tickStartFacts,
    BattleDynamicOccupancy occupancy,
    BattleEventStream stream,
    string battleId,
    int tick,
    double currentTimeSeconds,
    BattleNavigationGraph navigationGraph,
    HashSet<string> navigationFailureDiagnostics,
    BattlePerformanceCounters performanceCounters,
    TryRetargetStaleAdvanceContextCallback retargetStaleAdvanceContext)
```

Mutation authority:

- may set `context.Result`;
- may set live actor reservation fields;
- may call `BattleRuntimeActorStateMachine.MarkHolding` and `MarkMovementCommitted`;
- may call `BattlePlanStateEmitter.SetPlanState`;
- may append movement events to the passed `BattleEventStream`;
- may record advance failure/reset state through the same helper used today;
- may record movement/reservation performance counters;
- may emit the same low-noise movement failure diagnostic text used today.

Purity:

- not pure. It is a deterministic mutation service over shared runtime state, occupancy, reservations, counters, diagnostics, and stream order.

Non-authority:

- no AI request construction;
- no target selection;
- no flow-field cache ownership except through the retarget callback owned by the orchestrator;
- no attack, damage, defeat, settlement, or engagement ownership.

### Existing Extracted Collaborators

- `BattleRuntimeEventFactory`: remains pure event construction only. It must not append to streams.
- `BattlePlanStateEmitter`: remains the only helper that mutates `actor.PlanState` and appends plan-state events.
- `BattleRuntimeActorStateMachine`: remains the only mutation boundary for phase, movement timing, attack recovery, holding, waiting, and defeated state. New services must continue to use it instead of directly writing phase/movement/timing fields.

## Orchestration Flow

`BattleRuntimeTickResolver.ResolveTick` remains the phase orchestrator:

```text
advance time boundaries and emit MovementCompleted
-> capture living corps and refresh perception summaries
-> build tickStartFacts
-> build one TickContext list from AI/target/navigation facts
-> apply existing pre-resolution hold/target-lock/objective-plan handling
-> ResolveAttackProposalsAndEngagementTriggers
   -> record firstAttackEventIndex
   -> BattleAttackResolver.Resolve(...)
   -> immediately slice stream[firstAttackEventIndex..] for DamageApplied
   -> apply engagement triggers and append engagement events
-> BattleMovementCommitResolver.Resolve(...)
-> final unresolved-action fill and action-result diagnostics
```

The hard order stays attack before movement. Movement must observe live HP changes written by attack resolution through `context.ActorFact.Actor` and `context.TargetFact.Value.Actor`, while still using the same tick-start anchors and scalar facts for deterministic same-tick decisions.

## S-Level Equivalence Risks

### 1. Event Order Is `stream.Add` Order

There is no sequence counter. The extraction must preserve every relative `stream.Add` position.

Design rule:

- services receive the original `BattleEventStream` instance and append at the same logical call sites as the current resolver;
- services must not return unordered event lists for the caller to append later;
- the main resolver call order remains attack service, engagement slice/application, movement service;
- no diagnostics, plan state, engagement update, or movement write may be inserted between `firstAttackEventIndex` capture and the post-attack slice except the exact events currently emitted by attack resolution.

The current golden `TargetBattleEventOrderGoldenRegressionCases` remains the primary safety net for relative event order.

### 2. Defeated Event Order Depends On `postAttackHitPoints` Dictionary Enumeration

Current attack resolution builds `postAttackHitPoints` from `tickStartFacts.Values.ToDictionary(...)` and later iterates that dictionary without explicit sorting. This is a hidden ordering dependency.

Design decision: keep the same dictionary construction and enumerate the same dictionary instance. Do not replace it with `SortedDictionary`, `ImmutableDictionary`, `OrderBy`, a rebuilt dictionary, or a list grouped by target. Explicitly sorting defeated events would be more readable, but it would not be byte-for-byte equivalent to the current behavior.

If reviewers later want explicit defeated ordering, that must be a separate behavior-change proposal with updated golden expectations. It is not part of TD-003 extraction.

### 3. `firstAttackEventIndex` Stream Slice For Engagement Triggers

`ResolveAttackProposalsAndEngagementTriggers` currently identifies this tick's newly-added attack events by recording the stream count, running attack resolution, and slicing the stream tail. This requires attack events to be appended at the stream tail and requires nobody else to write to the stream before the slice is read.

Design rule:

- keep `ResolveAttackProposalsAndEngagementTriggers` in `BattleRuntimeTickResolver` for this slice;
- replace only the internal attack method call with `BattleAttackResolver.Resolve(...)`;
- after that service call returns, immediately compute the slice and filter `DamageApplied`;
- do not move engagement trigger application into `BattleAttackResolver`;
- do not let `BattleMovementCommitResolver`, action-result logging, or any future service run before the slice.

This preserves the existing engagement ownership and avoids pulling TD-002 target/engagement policy into TD-003.

### 4. Retarget Reverse Dependency On `BuildTickContext`

`TryRetargetStaleAdvanceContext` currently re-invokes `BuildTickContext`, which can involve AI request construction, target selection, local combat facts, pathfinding, flow fields, occupancy, and diagnostics. Movement resolution therefore has a reverse dependency on context building.

Design decision: keep `BuildTickContext` and stale-advance retargeting ownership in the main resolver for this TD-003 slice. `BattleMovementCommitResolver` receives a narrow callback:

```text
TryRetargetStaleAdvanceContextCallback(context, tickStartFacts, occupancy, navigationGraph, battleId, tick, currentTimeSeconds, navigationFailureDiagnostics, performanceCounters) -> bool
```

The callback mutates the existing context in place exactly as today. The resolver-owned callback must:

- create `BattleFlowFieldCache` with the same `performanceCounters`;
- call `BuildTickContext(...)` with the same arguments and `tacticalStateStore: null`, matching current retarget behavior;
- accept only a refreshed `AdvanceTowardTarget` context with live target, `HasMoveTo`, and blank failure reason;
- copy `Request`, `TargetFact`, and `Proposal` back onto the same context object;
- update live `Actor.TargetActorId`;
- reset advance-failure state;
- never write to `BattleEventStream`.

Reasoning: TD-003 is a phase-service extraction. Moving `BuildTickContext` or target-selection policy into movement would silently expand TD-003 into TD-002 and blur AI/navigation ownership. The callback keeps movement commit responsible for detecting the stale target, while the resolver/future context-builder remains responsible for rebuilding action context.

Future TD-002 can replace the resolver-owned callback with an extracted context-builder service without changing the movement resolver contract.

### 5. Shared State Must Be One Instance, Not Copies

The four coordination objects must be shared:

- one `BattleEventStream`;
- one `tickStartFacts` dictionary;
- one `List<BattleRuntimeTickContext>`;
- one live actor graph referenced by the facts and contexts.

Design rule:

- attack and movement services receive these objects by reference;
- no service clones the context list, clones actors, rebuilds `tickStartFacts`, or creates a private event stream;
- attack writes target live HP before movement runs;
- movement checks live `Actor.HitPoints`, not tick-start HP, when filtering actors killed by same-tick damage;
- `context.Result == null` remains the cross-phase coordination bit. Attack marks resolved attack contexts; movement only processes unresolved movement contexts; final logging sees the same mutated context objects.

## Attack Event And Mutation Order

`BattleAttackResolver` must preserve the current internal grouping:

1. Enumerate unresolved `AttackTarget` contexts in current context-list order.
2. For invalid target, out-of-range target, or empty charge, mark the actor holding/waiting and set failure result exactly as today.
3. Add valid attacks to `pendingAttacks` in current context-list order.
4. Build `postAttackHitPoints` from `tickStartFacts.Values.ToDictionary(...)` with ordinal comparer.
5. Group pending attacks by target actor id with ordinal comparer.
6. Within each target group, apply damage caps by attacker actor id ordinal and append `AttackApplication` records.
7. Emit all `DamageApplied` events ordered by attacker actor id ordinal.
8. Iterate `pendingAttacks` in original pending order to deduct attack charge, reset advance-failure state, emit `Attacking` plan state through `BattlePlanStateEmitter`, mark attack recovery, and set success result.
9. Iterate `postAttackHitPoints` in its current dictionary enumeration order to write live HP; for actors at zero HP, emit `Defeated` plan state through `BattlePlanStateEmitter` and mark defeated.

This preserves the visible order:

```text
all DamageApplied
-> all Attacking plan-state changes
-> all Defeated plan-state changes
```

Plan-state events may still be skipped by `BattlePlanStateEmitter` when the actor is already in that state. That idempotence is part of the existing behavior.

## Movement Event And Mutation Order

`BattleMovementCommitResolver` must preserve the current internal grouping:

1. Enumerate unresolved movement-like contexts in current context-list order.
2. Reject invalid non-objective targets.
3. Reject missing movement proposals by setting `advance_failed`, recording advance failure, and marking holding.
4. Reject actors whose live HP is already zero after attack resolution with `actor_defeated_before_move`.
5. If a non-objective target's live HP is zero, call the resolver-owned stale-retarget callback. If it fails, set `target_defeated_before_move` and do not mark holding.
6. Build move candidates with the same ordered move list fallback.
7. Create one local `BattleMovementReservationMap`.
8. Process candidates by the exact current ordering:
   - gap to target or objective anchor;
   - source height;
   - source Y;
   - source X;
   - battle-group id ordinal.
9. Preserve LINQ stable ordering for ties. Do not add actor id or target id tie-breakers.
10. For each candidate, try ordered moves in order. On rejection, record reservation rejection and continue.
11. If all reservations fail, set `reservation_rejected`, record advance failure, record hold-due-reservation, mark holding, and log the same advance diagnostic.
12. On success, write live reserved-cell fields, emit movement plan-state through `BattlePlanStateEmitter`, call `BattleRuntimeActorStateMachine.MarkMovementCommitted`, reset advance-failure state, set success result, append `MovementStarted`, increment movement count, and record movement counter.

The movement service returns only the movement event count used by the existing performance counter path.

## Suggested Cut Sequence

Do not extract both resolvers in one implementation slice.

1. Add or widen regression coverage before production extraction. Focus on event order, multi-defeat dictionary order, stale-target retarget, and reservation conflict. Existing behavior should be captured before service seams move.
2. Extract shared tick DTOs and tiny helper boundaries needed by both services. This should be a mechanical compile-only slice with no business logic movement.
3. Extract `BattleMovementCommitResolver` first, with the resolver-owned retarget callback. This validates the hardest architecture seam while leaving attack batching, defeated ordering, and engagement stream slicing untouched.
4. Extract `BattleAttackResolver` second, but keep `ResolveAttackProposalsAndEngagementTriggers` in the main resolver. This limits the stream-slice risk to one call replacement.
5. Add source-architecture guards after both services exist: resolver no longer directly contains `ResolveAttackProposals` or `ResolveMovementProposals`, and resolver does not call `BattleRuntimeActorStateMachine.MarkMovementCommitted` or `MarkAttackRecovery` outside the phase services.

Reasoning: movement-first isolates the retarget dependency without touching the attack stream slice. Attack extraction then happens with the service pattern already proven and with the engagement wrapper still protecting `firstAttackEventIndex`.

## Test Strategy

Do not rely only on event presence/count tests. The new seam is order-sensitive.

Existing safety nets to keep:

- `TargetBattleEventOrderGoldenRegressionCases`: relative event stream order for attack, plan, defeat, and movement.
- same-tick attack cadence tests for simultaneous damage and defeated actor movement discard.
- movement intent retarget test where a mover retargets when its target dies before movement resolves.
- congestion/reservation tests for alternate same-tick reservation candidates.
- performance counter tests for reservation and movement counters.

Recommended additions before or alongside extraction:

- multi-target same-tick defeat order golden: two or more targets defeated in one tick, with actor ids arranged so alphabetical order differs from tick-start insertion order, to detect accidental sorting or dictionary rebuilds.
- attack-stream-slice golden: one tick that emits damage, attacking plan changes, defeated plan changes, engagement-trigger events, and movement, asserting engagement-trigger events are based only on the attack slice and movement starts afterward.
- retarget order golden: target A dies from same-tick damage, mover retargets target B, and the stable projection proves no movement to dead target A and no stream write occurs during retarget before engagement slicing.
- reservation tie-stability test: two candidates with equal gap/source/battle-group ordering, asserting current stable context-list tie behavior remains unchanged.
- failed attack contexts test: invalid target, out-of-range target, and empty charge still set the same result/status and do not append damage or movement events.
- architecture guard: `BattleRuntimeTickResolver*.cs` must not directly contain `ResolveAttackProposals` or `ResolveMovementProposals` after extraction, and direct `MarkMovementCommitted` / `MarkAttackRecovery` calls are confined to the phase services.

## Diagnostics And Manual QA

Diagnostics:

- keep existing `BattleRuntimeAdvanceDiagnostic` message shape and low-noise dedupe key;
- keep runtime action-result logging after both services, using the shared context result mutations;
- keep performance counter increments in the same success/failure branches.

Manual QA is not expected to reveal byte-level runtime equivalence better than headless regression for this refactor. If reviewer requests a manual pass after implementation, use a small battle with adjacent attackers, a same-tick defeat, and a blocked/reserved movement lane, then confirm visible movement/attack order still matches the event stream.

No build, test, or manual QA has been run for this design draft.

## Non-Goals

- Do not change target selection strategy. That belongs to TD-002.
- Do not change tactical observation, perception refresh, local combat region ownership, temporary target region policy, or engagement ownership.
- Do not change damage formulas, attack cadence, attack range, footprint rules, victory/defeat rules, or settlement.
- Do not change event payloads, event ids, reason codes, runtime tick/time values, or plan-state idempotence.
- Do not replace current dictionary-dependent defeated ordering with explicit sorting in this TD-003 extraction.
- Do not introduce compatibility fallbacks, alternate runtime paths, or duplicate authoritative implementations.
- Do not move `BuildTickContext` into movement resolution in this slice.

## Risks And Rollback

Risk: event order changes even when event counts match.

Rollback: revert the latest extraction slice and restore the previous in-resolver method body. Keep newly added tests only if they describe current accepted behavior; otherwise correct or remove the faulty expectation before reattempting.

Risk: defeated event order changes because the attack service rebuilds or sorts `postAttackHitPoints`.

Rollback: restore the current dictionary construction/enumeration exactly, then rerun the multi-defeat order golden before continuing.

Risk: movement service accidentally owns AI/context rebuild behavior.

Rollback: move retarget rebuild back behind the resolver-owned callback. The movement service may detect stale targets, but must not construct flow fields or call `BuildTickContext` directly in this slice.

Risk: services receive copied facts or contexts, breaking live HP/result coordination.

Rollback: remove copied DTOs and pass the original list/dictionary/object references through a request object. The acceptance criterion is that attack HP mutation is visible to movement in the same tick and final logging sees service-written `Result` values.

Risk: a source guard fails because the resolver still contains direct phase mutation.

Rollback: either finish moving that exact phase mutation into the appropriate service or defer the guard until the extraction slice that actually moves it. Do not loosen the guard to permit duplicate authority.

Each implementation slice should be revertible independently. If one slice fails equivalence, revert that slice rather than adding compatibility branches or hidden alternate behavior.

## Review Checklist

- Status remains `Draft - pending review`.
- The design preserves attack-before-movement ordering.
- The design preserves `stream.Add` relative order and keeps engagement slicing immediately after attack resolution.
- The design keeps defeated ordering on the current `postAttackHitPoints` dictionary enumeration.
- The design keeps retarget context rebuild ownership in the resolver through a callback.
- The design passes one shared stream, one tick-start fact dictionary, and one mutable context list through services.
- The design keeps phase/timing mutation behind `BattleRuntimeActorStateMachine`.
- The design does not implement TD-002 target-selection or engagement policy changes.

## Reviewer Verification (2026-06-01)

Per `.codex/collaboration.md`, the design is reviewed by Claude before implementation. A sub-agent fact-checked every factual claim this design makes about the current code against the real source (line-by-line). All 14 checked claims are accurate; no false assumption that would invalidate the plan was found. Key load-bearing facts confirmed:

- `postAttackHitPoints` is built via `tickStartFacts.Values.ToDictionary(..., StringComparer.Ordinal)` and iterated without explicit sort (`BattleRuntimeTickResolver.cs:626-629`, `:684-702`) — the "keep dictionary enumeration order, do not sort" decision rests on a real hidden dependency.
- `TryRetargetStaleAdvanceContext` re-invokes `BuildTickContext(...)` with `tacticalStateStore: null` and has no `BattleEventStream` parameter / no `stream.Add` (`Retargeting.cs:29-57`) — the "retarget never writes the stream, ownership stays in resolver via callback" decision is sound.
- Movement candidate ordering is `gap → From.Height → From.Y → From.X → BattleGroupId(ordinal)` with no actor/target id tie-break, relying on stable LINQ ordering (`:784-793`) — the "do not add id tie-breakers" rule is correct.
- Visible event order is three sequential loops: all DamageApplied → all Attacking plan → all Defeated plan (`:651-666`, `:668-682`, `:684-702`).
- `firstAttackEventIndex` slice is taken immediately after attack resolution returns, before any other stream write (`Engagement.cs:19-47`).

Approved with one implementation note that the design's wording under-specifies:

- The "phase/timing mutation stays behind `BattleRuntimeActorStateMachine`" rule covers phase/motion/timing/recovery/holding/defeated fields only. Two writes already bypass the state machine in the extracted scope and must move WITH their service, not be forced behind the state machine: reserved-cell fields (`HasReservedGridCell`/`ReservedGridX/Y/Height`, `:828-831`) belong to `BattleMovementCommitResolver`; `TargetActorId` (`Retargeting.cs:56`) is written by the retarget callback. The design already lists "set live actor reservation fields" as a movement-service authority, so this is consistent — but the implementer must not be misled by the "only via state machine" phrasing into routing these through the state machine.

Decision: proceed to implementation following the Suggested Cut Sequence. Start with widening the golden net (multi-defeat dictionary order, retarget order, attack-stream-slice), then the mechanical shared-DTO slice, then `BattleMovementCommitResolver`, then `BattleAttackResolver`. Each slice is independently verified (build + regression + golden) and reverted on any equivalence failure.
