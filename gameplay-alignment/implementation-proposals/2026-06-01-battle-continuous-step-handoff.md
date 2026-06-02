# Battle Continuous Step Handoff Implementation Proposal

Status: Reviewed - approved for phased implementation (reviewer: Claude, 2026-06-01)
Created: 2026-06-01

## Requirement / Authority

Requirement: remove the fixed-tick idle hole between adjacent grid movement segments by letting Runtime commit a completed cell boundary and, when the existing movement intent is still valid, hand off to the next movement segment in the same simulation tick.

Authority and routing:

- `gameplay-design/content-systems-long-term-design.md`: hero-led light RTS combat should read as low-frequency spatial realtime combat, not one-cell tactical turns.
- `system-design/hero-led-light-rts-system-architecture.md`: Runtime owns live combat truth and emits reportable facts.
- `system-design/battle-runtime-architecture.md`: Runtime tick rules, actor phases, movement authority, and the Presentation boundary.
- `system-design/battle-navigation-topology-architecture.md`: topology, occupancy, reservations, path legality, and movement diagnostics remain Runtime-owned.
- `gameplay-alignment/implementation-proposals/archived/2026-05-22-continuous-rts-movement.md`: historical implementation record for fixed-clock movement and rejected Presentation-side lookahead.
- `gameplay-alignment/implementation-proposals/archived/2026-06-01-td-003-attack-movement-resolver-extraction.md`: current attack-before-movement resolver boundary and event-order invariants.

This proposal is an implementation proposal only. It does not change accepted gameplay or architecture authority. `system-design/battle-runtime-architecture.md` already requires this behavior: line 94 says movement may continue into the next neighbor when intent, topology, occupancy, and reservations allow it; line 191 says a committed movement boundary must either continue toward the next valid neighbor or return to an anchored decision, hold, or attack boundary. The current implementation only realizes the return branch.

## Current Verified Shape

The diagnosed idle hole is real and comes from the current Runtime orchestration:

- `BattleRuntimeActorStateMachine.AdvanceMovementBoundary` updates movement progress from `currentTimeSeconds - MovementStartedAtSeconds` over the locked movement duration, then commits the actor to `MovementToGrid*`.
- After the commit, `BattleRuntimeActorStateMachine.cs:169-170` unconditionally sets the actor back to `AnchoredDecision`.
- `BattleRuntimeTickResolver.cs:71-74` then excludes actors in `movementCompletedActorIds` from this tick's decision-ready corps, so a just-completed mover cannot author the next step until the next fixed tick.
- `BattleRuntimeSessionController.cs:141-146` advances live runtime by the fixed `DefaultSimulationTickSeconds` of 0.04 seconds. `DefaultMoveStepSeconds` is 0.16 seconds, so a default mover visually traverses one cell for 0.16 seconds and then waits one 0.04-second decision tick before the next `MovementStarted`.
- `BattleRuntimeEventFactory.cs:70` currently publishes movement event duration from `MoveStepSeconds`, so Presentation receives a 0.16-second segment even though same-actor `MovementStarted` events arrive every 0.20 seconds.

This is not a Presentation smoothing problem. `BattleUnitRoot` already stitches movement lanes:

- a new lane starts from `entity.GlobalPosition`, preserving geometric continuity when the old lane has drained;
- an existing lane enqueues from `_queuedEndPoint`, preserving continuity when the next segment arrives before the lane drains;
- the idle grace window is 0.04 seconds, matching one fixed Runtime tick.

The historical rejection in `2026-05-22-continuous-rts-movement.md:54` rejected same-tick continuation because the tested approach could push Presentation ahead of the visible committed step. The same document rejected Presentation trailing buffers and Runtime lookahead visual corridors at lines 82-90 because they created a visual/logic offset or a future-corridor correction. This proposal is different: Runtime itself commits B as authoritative truth, then may author B->C from that committed truth. Presentation still consumes only emitted Runtime events and never chooses a path, matching `battle-runtime-architecture.md:70`, `:96`, and `:251`.

## Architecture Judgement

Subsystems:

- Runtime State: actor phase, movement target, movement duration, command/target intent, and action boundaries.
- Navigation: next-neighbor candidates, topology legality, occupancy, reservations, and diagnostics.
- Tick Orchestration: ordering of boundary commits, tactical refresh, decision contexts, attack resolution, engagement slicing, and movement commits.
- Presentation: interpolation only; no code changes are proposed.

The clean cut is not to put continuation inside `BattleRuntimeActorStateMachine.AdvanceMovementBoundary`. The state machine should remain the phase/timing mutation boundary. It should commit the completed cell, clear the active movement target/reservation, and expose a completion record, but it must not choose the next neighbor, reserve cells, emit `MovementStarted`, or call pathfinding.

The clean cut is also not to simply remove `movementCompletedActorIds` from the decision-ready filter. That would let a completed mover run full target selection and AI action choice in the same tick, which would turn continuous movement into high-frequency redecision.

Recommended cut:

```text
Advance movement boundaries and emit MovementCompleted
-> build tick-start facts from the committed anchors
-> build normal full-decision contexts for actors that did not complete movement this tick
-> apply normal decision outcomes
-> build movement-only continuation contexts for completed movers whose stored movement intent still matches
-> run attack resolver and post-attack engagement slicing exactly as TD-003 requires
-> run BattleMovementCommitResolver once over normal movement contexts plus continuation contexts
```

The continuation contexts should reuse the existing movement proposal and commit path:

- use the existing target/objective/region next-step helpers, including `BattleCrowdMovementPlanner` and `BattleObjectiveAdvancePlanner`, rather than introducing a second pathfinder;
- let `BattleMovementCommitResolver` keep owning same-tick reservation selection, `MarkMovementCommitted`, movement plan-state emission, `MovementStarted`, performance counters, and movement diagnostics;
- keep continuation as a movement-only context, not a full AI decision context.

## Movement Intent Snapshot

Same-tick handoff needs a stable definition of "same movement intent". Future implementation should store a lightweight movement intent snapshot when `MarkMovementCommitted` starts a segment. The snapshot should be Runtime state, not Presentation state, and should be cleared when the movement chain ends.

Minimum snapshot facts:

- movement request kind: target advance, objective advance, region advance, join local combat, hold support, or return to objective;
- target actor id for target-scoped movement;
- objective zone id or region id for objective/region movement;
- command id or movement intent revision present at segment start;
- movement reason code and local-combat situation id when the existing context already has them;
- the locked segment duration used for the current cell.

This matches the architecture statement that Runtime stores movement intent revision. It also lets continuation reject command changes without running a full new decision.

## Continuation Conditions

Continuation may happen only when all conditions below pass:

1. The actor completed a movement boundary in this tick and is still alive before movement resolution.
2. The actor's current command id or movement intent revision still matches the snapshot captured when the completed segment started.
3. The actor still has the same target actor, objective zone, or region goal that the completed segment was pursuing.
4. For target-scoped movement, the target still exists and is alive in current live state after attack resolution; continuation must not retarget to another target.
5. For objective/region movement, the objective/region is not reached from the committed anchor.
6. The actor has not reached an anchored attack/hold boundary under the same movement intent.
7. Topology can step from the committed anchor to a valid neighbor.
8. Occupancy and same-tick reservation authority accept the next step.
9. The actor was not defeated by same-tick damage before movement commit resolution.

Continuation must return to anchored decision instead of starting another segment when:

- the actor reached its objective, region, attack slot, support slot, leash boundary, or another movement stop condition;
- the target died, became invalid, left the scoped facts, or no longer matches the stored intent;
- the command id, objective zone, region goal, engagement rule, or movement intent revision changed while the actor was moving;
- current engagement/local-combat state requires a hold, attack, support, return, or target reconsideration rather than the same movement intent;
- topology/pathing fails;
- reservation is rejected;
- the actor is defeated by same-tick damage;
- diagnostics indicate stale or conflicting movement facts.

When continuation fails, the actor remains at the committed anchor and waits for the normal next decision tick. This proposal intentionally does not enable same-tick full redecision or same-tick post-movement attack as a side effect.

## Timing Decisions

Single-cell speed is locked for the duration of that cell. `MarkMovementCommitted` should resolve the segment's duration once, store it in `MovementDurationSeconds`, and the actor should finish that segment using that stored duration even if buffs, debuffs, terrain, or data changes alter `MoveStepSeconds` mid-cell. Those changes affect only the next `MarkMovementCommitted`.

The 0.04-second fixed simulation tick remains the Runtime heartbeat. Movement progress continues to be accumulated as `delta / MovementDurationSeconds`, which is already the current state-machine shape.

`MoveStepSeconds` must have a movement-specific lower bound of one fixed tick: `BattleActionTimingPolicy.DefaultSimulationTickSeconds`, currently 0.04 seconds. Without this floor, a unit could legally require less than one tick per cell; the current boundary model would either quantize it unpredictably or need one tick to cross multiple cells, which would exceed the current "one boundary commit, then one optional next segment" model and risk duplicate same-actor `MovementStarted` ids in one tick.

`MoveStepSeconds` does not need to be an integer multiple of the fixed tick. A 0.10-second cell will complete on the first fixed tick at or after the boundary, approximately 0.12 seconds. That is a speed precision/quantization issue, not a fluidity issue: each segment is independently quantized, and same-tick handoff removes the empty 0.04-second gap between completed and next-start events.

Movement event duration should use the locked segment duration for `MovementStarted`, not a mutable live `MoveStepSeconds` value that might have changed after the segment started.

## TD-003 Invariants

TD-003's hard order remains:

```text
decision/context construction
-> attack resolution
-> immediate firstAttackEventIndex slice and engagement-trigger application
-> movement resolution
```

Continuation contexts are movement contexts. They must not emit stream events while they are built, and they must not run before attack resolution. The attack resolver still runs before `BattleMovementCommitResolver`, so same-tick damage can mutate live hit points first.

`BattleMovementCommitResolver` must continue checking live actor HP before movement commit. A completed mover that is killed by same-tick attack damage may have already emitted `MovementCompleted(A->B)` because B is the committed boundary at tick start, but it must not emit `MovementStarted(B->C)`.

`firstAttackEventIndex` slicing must remain immediately around attack resolution. Continuation building cannot append plan, diagnostic, or movement events between attack resolution and the engagement slice. Movement handoff events must be appended only after the engagement slice has been consumed.

The existing defeated-unit movement invariant remains: actors defeated by same-tick damage cannot move in the movement phase. This is protected by live HP checks, not tick-start HP.

## Event Stream Changes

After the change, a continuing actor can produce this same-tick sequence:

```text
tick N: MovementCompleted(A->B)
tick N: attack / damage / attacking plan / defeated plan / engagement events for the tick
tick N: optional idempotent movement plan state for the continuation
tick N: MovementStarted(B->C)
```

This is correct because B is already committed Runtime truth before B->C is authored. The new `MovementStarted` does not describe future lookahead; it describes the next committed Runtime segment starting from B.

The `MovementCompleted` and `MovementStarted` event ids use different suffixes, so the same actor may safely emit one completion and one start in the same tick. The one-tick minimum movement duration prevents the same actor from emitting more than one start for multiple new cells in the same tick.

Presentation does not need a code change. The acceptance condition is that `BattleUnitRoot` continues to consume a same-tick `MovementCompleted` + `MovementStarted` pair smoothly through the existing `MovementLane` enqueue path, and does not flash idle between cells.

## Golden Expectation Impact

This is a behavior change, not a byte-for-byte refactor. Event-order goldens must be treated as expected to change when their snapshots contain a completed mover with valid continuing movement intent.

Known likely changes:

- `TargetBattleEventOrderGoldenRegressionCases.RuntimeEventStreamOrderGoldenLocksAttackMovementPlanAndDefeat`: currently locks a movement completion followed by a later-tick action path; if the mover still has valid movement intent, the stable projection should gain a same-tick continuation start after the attack/engagement phase.
- `TargetBattleEventOrderGoldenRegressionCases.Td002SliceC.RuntimeTd002EngagementExitGoldenClearsOnlyExitedGroupTargetLocks`: currently locks two same-tick `MovementCompleted` events followed by engagement/local-region events; movers whose objective/region intent remains valid may now add same-tick continuation starts after the TD-003 attack/engagement window.

Must be re-run and reviewed even if they do not change:

- `TargetBattleEventOrderGoldenRegressionCases.Td002SliceC` region refresh, temporary region, engagement enter/exit, and post-attack hold-defense goldens.
- `TargetBattleEventOrderGoldenRegressionCases.Td003` multi-defeat, attack-stream-slice, retarget, reservation tiebreak, and failed-attack-context goldens.

How to re-solidify:

1. Add or update a narrow handoff regression that explicitly expects `MovementCompleted(A->B)` and `MovementStarted(B->C)` in the same runtime tick for one actor with an unchanged target/objective intent.
2. Implement the handoff.
3. Run the event-order golden suite and inspect diffs in both event ids and stable projections.
4. Accept only diffs that are explained by same-tick completion/start handoff or by the intentional absence of handoff when the actor reached a stop boundary.
5. Manually confirm that attack events, defeated plan events, and post-attack engagement events still appear before movement starts.
6. Update the golden arrays from the reviewed actual sequence. Do not blanket-update goldens before the narrow handoff regression proves the new behavior.

## Decision Frequency

Same-tick continuation is movement continuity, not permission for every actor to choose a new action every tick. `battle-runtime-architecture.md:79` remains binding: a simulation tick alone does not authorize a full new action decision.

Completed movers should not be merged into the normal `decisionReadyCorps` full-decision list. Instead, they get a constrained movement-only continuation context if the stored movement intent still matches current Runtime facts. Target selection services and AI action selection may still run for normal decision-ready actors, but not as a general same-tick redecision path for completed movers.

If a continuation cannot be proven to be the same movement intent, the actor returns to anchored decision for the next tick.

## Suggested Cut Sequence

1. Add a focused failing regression for continuous step handoff. The test should use a simple line or corridor with a mover that needs at least two cells, `MoveStepSeconds = 0.16`, fixed tick 0.04, and assert same-tick `MovementCompleted(A->B)` then `MovementStarted(B->C)` at the boundary tick.
2. Add negative focused tests before or with the implementation: target died, command changed, objective reached, reservation rejected, and same-tick damage defeated the mover.
3. Add movement intent snapshot fields and duration-lock payload behavior. This is a runtime-state change only; do not alter Presentation.
4. Update `AdvanceMovementBoundary` to return enough completion information for the resolver while keeping the state machine out of pathfinding and reservation.
5. Add resolver-owned continuation-context construction after normal decision outcomes and before attack resolution. Keep it stream-silent.
6. Pass normal contexts plus continuation contexts to `BattleMovementCommitResolver`. Use the existing candidate ordering and reservation map. Do not special-case a direct `MarkMovementCommitted` call in the resolver.
7. Update `BattleMovementCommitResolver` to reject stale targets for continuation instead of invoking stale-target retargeting. A small proposal/context flag such as `AllowStaleTargetRetarget = false` for continuation contexts is acceptable; default behavior for normal movement must remain unchanged.
8. Run the narrow regressions, then the full event-order golden suite. Review the stable projection diffs and update broad goldens only after the behavior is understood.
9. Run manual Presentation QA only after headless Runtime tests pass: confirm same-tick completion/start pairs are consumed smoothly and no idle flash appears between cells.

## Touched Systems

Expected implementation touch points:

- `BattleRuntimeActor`: movement intent snapshot fields and comments.
- `BattleRuntimeActorStateMachine`: movement boundary completion record, locked duration handling, and move-step floor resolution.
- `BattleRuntimeTickResolver`: continuation context orchestration.
- `BattleMovementCommitResolver`: optional stale-retarget guard for continuation contexts; otherwise reuse existing commit flow.
- `BattleRuntimeEventFactory`: movement duration should reflect locked segment duration for movement start events.
- `BattleActionTimingPolicy` or the move-duration resolver: codify the one-fixed-tick movement floor.
- `TargetBattleArchitectureRegression` event-order and movement-cadence tests.

Presentation touch points are not expected.

## Test Strategy

Focused Runtime regressions:

- continuous target handoff: actor starts A->B, completes at tick N, and starts B->C in tick N with no extra target-lock event and no full target reselection;
- continuous objective handoff: objective movement continues while objective is not reached;
- target reached / attack boundary: actor completes A->B, target is now in legal attack range, and no B->C movement start is emitted in the same tick;
- target died before movement resolution: continuation context does not retarget and does not start movement;
- same-tick damage defeated mover: completion can stand, but continuation start is absent;
- command or movement intent revision changed during movement: no same-tick continuation;
- reservation rejected: no continuation start, existing reservation diagnostics and hold/failure counters still work;
- move-step floor: values below one fixed tick are clamped to at least one tick;
- non-integral duration: a 0.10-second step completes on approximately 0.12 seconds with no additional idle gap before the next start.

Existing safety nets to keep:

- event-order goldens for attack, plan, defeat, engagement, and movement ordering;
- same-tick defeated actor cannot move tests;
- stale-target retarget tests for normal movement;
- same-tick reservation and alternate candidate tests;
- movement cadence tests;
- performance counter tests for movement events, reservation rejection, and hold due reservation.

Manual QA:

- use a small authored battle lane where one unit traverses at least three cells without contact;
- confirm visual movement reads as continuous cell-to-cell motion;
- confirm a unit that reaches attack range stops movement and attacks only through the normal Runtime attack path;
- confirm same-tick `MovementCompleted` + `MovementStarted` event pairs do not flash idle in `BattleUnitRoot`.

No build, test, or manual QA should be run while this document is still in design review.

## Diagnostics

Keep diagnostics low-noise and boundary-oriented:

- reuse existing movement failure diagnostics for topology/path/reservation failures;
- add a small reason only if needed to distinguish continuation rejection from normal movement failure, such as `continuation_intent_changed` or `continuation_target_invalid`;
- do not log per-frame or per-progress updates;
- keep final Runtime action-result logging after attack and movement resolution so shared context results are visible.

## Non-Goals

- Do not change the speed stat system or add new speed formulas.
- Do not change the fixed 0.04-second simulation tick.
- Do not require `MoveStepSeconds` to be an integer multiple of the fixed tick.
- Do not change pathfinding algorithms, topology compilation, footprint rules, or reservation authority.
- Do not change Presentation movement interpolation, movement lanes, idle grace, or animation state code.
- Do not introduce Presentation trailing buffers, visual lookahead corridors, or any Presentation-side path prediction.
- Do not change attack-before-movement orchestration.
- Do not let completed movers run full same-tick AI target selection or action redecision.
- Do not change campaign settlement, reports, or battle result writeback.

## Risks And Rollback

Risk: continuation accidentally runs before same-tick attack damage, allowing a unit killed by attack to move.

Rollback: restore the previous resolver order and keep continuation contexts out of movement resolution until live HP checks prove the same-tick defeated case.

Risk: completed movers are allowed into full decision-ready processing, creating same-tick target reselection or same-tick attacks outside this proposal.

Rollback: reintroduce the completed-actor exclusion for full decisions and keep only the constrained continuation-context builder.

Risk: continuation retargets a dead target through the existing stale-target callback, violating the same-intent rule.

Rollback: add or restore the continuation flag that disables stale-target retargeting for continuation contexts.

Risk: occupancy treats the cell released by A->B as open to other same-tick actors.

Rollback: keep using the pre-boundary occupancy snapshot for same-tick movement resolution. The completed actor can ignore its own old occupancy, but other actors must not treat released cells as open in the same resolver pass.

Risk: event-order goldens are updated blindly and hide an attack/movement invariant break.

Rollback: revert the broad golden update, keep the narrow handoff test, inspect stable projections, and reapply only explained event-order changes.

Risk: lowering or changing movement duration normalization changes more speed behavior than intended.

Rollback: preserve current normalized durations except for the explicit lower-bound guarantee. If exact 0.04-second minimum causes unrelated changes, document whether the existing 0.05 generic minimum remains the effective floor and keep the one-tick invariant.

Each implementation slice should be revertible independently. Do not add compatibility branches or duplicate authoritative movement paths.

## Acceptance Criteria

The implementation can be accepted only when:

- `Status` has been reviewed and explicitly moved out of draft by the reviewer;
- a default 0.16-second mover can emit same-actor `MovementStarted` events at approximately 0.16-second intervals across continuous legal cells, not 0.20-second intervals;
- a completed mover that still has the same valid movement intent emits `MovementCompleted(A->B)` and `MovementStarted(B->C)` in the same runtime tick;
- a completed mover that reached a stop/attack/hold/redecision boundary does not continue in the same tick;
- same-tick attack, damage, defeated events, and engagement-trigger slicing still precede movement starts;
- a mover defeated by same-tick damage does not start a continuation segment;
- stale-target retarget remains available for normal movement but is not used to justify continuation;
- event-order goldens are updated only after stable-projection review;
- Presentation consumes same-tick completion/start pairs smoothly without code changes.

## Review Checklist

- Status remains `Draft - pending review`.
- This proposal implements accepted `battle-runtime-architecture.md:94` and `:191`; it does not create new architecture authority.
- The 2026-05-22 Presentation lookahead rejection is distinguished from Runtime-authoritative continuation.
- StateMachine does not own pathfinding, reservation, or `MovementStarted` emission.
- Resolver orchestration does not add completed movers to full same-tick AI redecision.
- Continuation contexts are stream-silent until `BattleMovementCommitResolver`.
- Attack-before-movement and `firstAttackEventIndex` slicing remain TD-003 compliant.
- Same-tick defeated actors cannot continue movement.
- The same-target/same-objective/same-region condition blocks retarget-based continuation.
- Move-step duration is locked per cell and has at least a one-fixed-tick floor.
- Non-integral move durations are accepted as quantized speed precision, not a fluidity blocker.
- Presentation remains a consumer of emitted Runtime events only.
- Golden updates are treated as an intentional behavior change and reviewed through stable projections.

## Reviewer Verification (2026-06-01)

Per `.codex/collaboration.md`, reviewed by Claude before implementation. A sub-agent fact-checked all factual claims against the real source; all 10 checked claims hold, no false assumption that would invalidate the plan. Load-bearing facts confirmed:

- Root cause (`BattleRuntimeActorStateMachine.cs:168-176` unconditionally returns to AnchoredDecision with no continuation branch; `BattleRuntimeTickResolver.cs:71-74` excludes completed movers from `decisionReadyCorps`; `BattleRuntimeEventFactory.cs:70` duration from `MoveStepSeconds`) — accurate.
- Cut point exists: ResolveTick orchestration matches the design's skeleton; the continuation-context build slot between `ApplyDecisionOutcomes` (`:95`) and attack resolution (`:99`) is real, and completed movers retain a tickStartFacts entry anchored at B.
- **Occupancy concurrency (the riskiest point) — design is correct.** Occupancy is a pre-boundary snapshot (`BattleRuntimeTickResolver.cs:127-131`). A continuing mover is not blocked by its own old cell/reservation: reservation checks only the destination (`BattleMovementReservationMap.cs:40`) and `IsOccupiedByOther` filters self by actorId (`BattleDynamicOccupancy.cs:91`). A released cell is conservatively treated as occupied by other same-tick movers (safe, no collision). The design's Risks section judged this correctly.
- TD-003 invariants hold: attack precedes movement (`:99` before `:101`); `BattleMovementCommitResolver.cs:76-80` checks live HP, so a same-tick-killed mover cannot continue.

One harmless wording nit (no plan impact): §Timing says progress is "accumulated as delta/MovementDurationSeconds"; the code actually computes elapsed/duration (`StateMachine.cs:150-154`) — equivalent result.

Decision: approved for phased implementation following the Suggested Cut Sequence. The must-honor item flagged by review: continuation reusing the `AdvanceTowardTarget` path must bypass the stale-target retarget (`BattleMovementCommitResolver.cs:82-98`) via the proposed `AllowStaleTargetRetarget=false` flag, so continuation never silently retargets. Golden updates only after the narrow handoff regression proves the new same-tick MovementCompleted+MovementStarted behavior and stable-projection diffs are reviewed.
