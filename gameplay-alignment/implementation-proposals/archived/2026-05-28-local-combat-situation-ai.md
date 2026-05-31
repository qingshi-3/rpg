# Local Combat Situation AI Implementation Proposal

Status: Archived - historical local-combat slice; final enemy AI acceptance superseded by 2026-05-29 tactical-region authority
Created: 2026-05-28

Originating Design Proposal: `design-proposals/archived/2026-05-27-local-combat-situation-ai`
Requirement Id: `battle-ai-local-combat-situation-v1`
Related Design Proposals: `design-proposals/archived/2026-05-29-enemy-region-directed-combat-ai`
Amendment Proposals: `design-proposals/archived/2026-05-29-enemy-region-directed-combat-ai`
Blocking Issues: 2026-05-29 tactical-region authority requires battle-group-owned target regions, temporary regions, local combat regions, and player/enemy policy separation before final enemy AI acceptance
Verification Records: 2026-05-28 headless verification passed; 2026-05-29 manual QA superseded by accepted tactical-region authority amendment

Authority Documents:
- `gameplay-design/content-systems-long-term-design.md`
- `system-design/battle-ai-boundary-architecture.md`
- `system-design/battle-group-tactical-region-architecture.md`
- `system-design/battle-runtime-architecture.md`
- `system-design/battle-navigation-topology-architecture.md`

## 2026-05-29 Authority Amendment Note

This implementation proposal remains useful as evidence for the first local-combat slice, but it is no longer sufficient final acceptance authority for enemy AI behavior. The accepted `design-proposals/archived/2026-05-29-enemy-region-directed-combat-ai` proposal requires battle-group-owned target regions, temporary regions, local combat regions, group engagement state, enemy-only region policy, and player-command separation. Do not mark this proposal accepted or use its manual QA list as final enemy AI acceptance until a follow-up implementation proposal covers the new authority.

## Goal

Implement the first playable Runtime-owned local combat situation slice so units can join nearby active fights, prefer open attack slots, use deterministic support slots when attack slots are full, respect engagement rules and defensive leashes, and emit explainable low-noise diagnostics.

## Architecture Judgment

This is Battle Runtime, AI, and Navigation work. Runtime owns live actor facts, temporary `LocalCombatSituation` observations, reservations, movement validation, damage, events, and outcome. AI consumes typed decision facts and returns typed intent only. Navigation owns footprint-aware attack/support slot legality. Presentation and LimboAI may expose the same branches through the facade, but they must not create movement, occupancy, damage, or settlement truth.

The implementation should extend the current battle-plan and attack-slot runtime rather than introduce a parallel tactical director. If implementation shows that current authority lacks a required gameplay rule, stop coding and open an amendment design proposal instead of inventing local behavior.

## Scope

1. Add Runtime tactical facts for `LocalCombatSituation` built from recent attacks/damage, close threat, route blocking, actor anchors, target facts, engagement rules, occupancy, reservations, attack slots, and support slots.
2. Extend Runtime AI decision facts and action kinds with join-local-combat, hold-support, and return-to-objective branches while keeping Runtime as final validator.
3. Replace generic support-slot behavior with named first-slice roles: `MeleeQueue`, `LineHold`, and `RangedHold`, ordered deterministically and rejected when they block occupied attack slots or violate scope/leash rules.
4. Track one active local-combat assignment per battle group plus anti-jitter locks for join, return, slot claim, and de-aggro transitions.
5. Apply engagement-rule predicates for `MoveFirst`, `AttackFirst`, `FireOnTheMove`, `Hold`, `ProtectHero`, and `RetreatFirst` local response.
6. Emit low-noise reason codes such as `join_recent_damage`, `join_blocks_objective_route`, `hold_support_attack_slots_full`, `return_objective_threat_clear`, `reject_outside_leash`, `reject_join_budget_full`, and `reject_no_reachable_slot`.
7. Keep LimboAI and the C# Runtime executor on a narrow shared facade so the same decision branches are visible from both integration surfaces.

## Non-Goals

- No full battle tactical snapshot cache unless local decision-boundary queries prove insufficient.
- No global best-target or global best-attack-position scan for every unit every tick.
- No generic pressure-score tactical director in this slice.
- No complex multi-front director, morale, reinforcement director, or advanced formation behavior.
- No Presentation-owned movement, damage, occupancy, reservation, or battle-outcome state.
- No per-frame behavior-tree execution or per-node/per-edge pathfinding logs.
- No new persistent campaign state for local combat situations.

## Touched Systems

Runtime battle AI:
- Modify `src/Runtime/Battle/AI/BattleRuntimeAiDecisionFacts.cs` to carry local-combat situation facts, support-slot availability, leash/scope status, route-blocking status, and diagnostic candidate reasons.
- Modify `src/Runtime/Battle/AI/BattleRuntimeAiActionKind.cs` and `src/Runtime/Battle/AI/BattleRuntimeAiActionRequest.cs` to add typed join, hold-support, and return-to-objective requests.
- Modify `src/Runtime/Battle/AI/DefaultBattleRuntimeAiExecutor.cs` to choose local response only when the Runtime-built facts satisfy the accepted engagement-rule predicates.
- Modify `src/Runtime/Battle/AI/IBattleRuntimeAiExecutor.cs` only if the existing request/result contract cannot carry the new typed facts.

Runtime tactical facts:
- Create `src/Runtime/Battle/Tactics/LocalCombatSituation.cs` for immutable situation id, center cell, participants, nearby candidates, target facts, slot summaries, imbalance facts, dirty reason, version, and last-built runtime time.
- Create `src/Runtime/Battle/Tactics/LocalCombatSituationBuilder.cs` for bounded local queries at actor decision boundaries and event-driven dirty rebuilds.
- Create `src/Runtime/Battle/Tactics/LocalCombatDecisionReason.cs` for shared reason-code constants used by Runtime, AI, diagnostics, tests, and Limbo facade code.
- Create `src/Runtime/Battle/Tactics/LocalCombatSupportSlotRole.cs` for `MeleeQueue`, `LineHold`, and `RangedHold` roles.

Runtime state and execution:
- Modify `src/Runtime/Battle/BattleRuntimeActor.cs` only for actor-local anti-jitter lock timestamps or selected support-slot claims that must survive across decision boundaries.
- Modify `src/Runtime/Battle/BattleRuntimeSession.cs` to hold local-combat dirty/version state if session-level facts are required.
- Modify `src/Runtime/Battle/BattleRuntimeTickResolver.cs` and partials `BattleRuntimeTickResolver.Plan.cs`, `BattleRuntimeTickResolver.Targeting.cs`, and `BattleRuntimeTickResolver.Diagnostics.cs` to build facts, choose local response at anchored decision boundaries, validate movement, reserve slots, and log one reason per actor/situation transition.
- Modify `src/Runtime/Battle/BattleRuntimeActionProposal.cs` if proposals need to carry support-slot role, local situation id, or return-to-objective reason.

Navigation and slots:
- Modify `src/Runtime/Battle/Navigation/BattleCombatSlot.cs`, `BattleCombatSlotKind.cs`, and `BattleCombatSlotAllocator.cs` so support slots are named and deterministic rather than generic `gap == attackRange + 1` placeholders.
- Modify `src/Runtime/Battle/Navigation/BattleCrowdMovementPlanner.cs`, `BattleFlowFieldBuilder.cs`, and `BattleFlowFieldCache.cs` only where local support-slot routing must reuse existing footprint-aware path and reservation authority.
- Keep support-slot movement rejected when the committed next step overlaps occupancy/reservation facts, blocks an occupied attack slot, or violates objective, hold, protect, retreat, or leash constraints.

Events, diagnostics, and facade:
- Modify `src/Runtime/Battle/Events/BattleEventKind.cs` and `src/Runtime/Battle/Events/BattleEvent.cs` only if existing `ReasonCode`, actor, group, target, and movement fields cannot explain local-combat transitions.
- Modify `src/Presentation/Battle/AI/BattleAiDecisionFacts.cs`, `BattleAiDecisionResult.cs`, `BattleAiFacade.cs`, and `BattleAiFacadeCore.cs` to expose the same typed branches without granting Presentation authority.
- Modify `scripts/ai/limbo_tasks/battle/*.gd` only through the facade boundary, not by reading or mutating Runtime internals directly.

Tests:
- Add `tests/TargetBattleArchitectureRegression/TargetBattleLocalCombatSituationRegressionCases.cs` for local-combat unit and integration regressions.
- Modify `tests/TargetBattleArchitectureRegression/Program.cs` to register the new regression cases.
- Extend existing movement, congestion, and AI runtime regression cases only when a scenario already owns the same behavior.

## Test Plan

Run focused regressions after implementation:

```powershell
dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal
```

Run broader battle/runtime regressions before acceptance:

```powershell
dotnet run --project tests\AutoBattleRuntimeRegression\AutoBattleRuntimeRegression.csproj -v:minimal
dotnet run --project tests\BattleHitFeedbackRegression\BattleHitFeedbackRegression.csproj -v:minimal
dotnet build rpg.csproj -maxcpucount:2 -v:minimal
dotnet build-server shutdown
```

## Required Regression Cases

- Narrow bridge fight: front line engages, a second melee unit takes an open attack slot, and a third melee unit takes a non-blocking `MeleeQueue` or `LineHold` support slot.
- Full attack slots: a nearby unit does not idle forever, does not reserve a duplicate destination, and fills an attack slot after it opens.
- Hold leash: defenders reinforce inside leash, reject pursuit outside leash with `reject_outside_leash`, and return to held area after threat clear.
- Move-first column: distant incidental combat is ignored, but route-blocking local combat causes a short local response with `join_blocks_objective_route`.
- Protect hero: a corps unit joins a threat inside hero protect scope, then returns after the threat clears.
- Path failure diagnostics: narrow-path failures emit one stable reason per actor/situation state, distinguishing occupancy, reservation, leash, and no-route failures.

## Diagnostics

Local-combat diagnostics must be low-noise and attributable:

- Emit a reason when an actor joins, holds support, returns, or rejects a local situation.
- Include actor id, battle-group id, target id when present, local situation id, support-slot role when present, and Runtime tick/time.
- Log one reason per actor/situation transition or stable failure state, not every frame or every search node.
- Prefer existing `BattleEvent.ReasonCode` and `BattleRuntimeAdvanceDiagnostic` patterns before adding new event fields.

## Manual QA

Use a battle scene with objective plans and enough units to form a visible line. Validate these scenarios in Godot after headless tests pass:

- Narrow bridge: front unit engages, second unit enters an attack slot, third unit visibly holds a second-line support slot without blocking the front.
- Full slots: extra melee units wait in readable support positions and fill attack slots when opened.
- Defender leash: defenders stop pursuit beyond the held area/leash and return instead of chasing indefinitely.
- Move-first: units keep objective movement unless local combat blocks the route or threatens the company.
- Protect hero: corps response is visible near the hero and ends after the threat clears.
- Diagnostics: logs explain join/support/return/reject decisions without repeated per-frame spam.

## Acceptance

This implementation proposal is acceptable when:

- Runtime owns `LocalCombatSituation` facts and remains the only movement, reservation, damage, defeat, event, and outcome authority.
- AI and Limbo facade branches expose attack, advance, join local combat, hold support, return to objective, retreat, and regroup decisions through typed requests.
- Open attack slots are preferred over support slots.
- Valid support slots prevent nearby eligible units from idling when attack slots are full.
- Defensive, hold, protect, retreat, and objective constraints reject local response outside accepted scope.
- Same-tick reservations prevent multiple actors from claiming the same attack or support destination.
- Headless tests cover the required regression cases and pass.
- Manual QA confirms readable battle behavior in the listed scenarios.

## Implementation Evidence

Implemented directly on `main` without creating a branch or committing:

- Added Runtime-owned `LocalCombatSituation` facts, reason codes, and support-slot role types under `src/Runtime/Battle/Tactics/`.
- Extended Runtime AI facts and typed action requests for `JoinLocalCombat`, `HoldSupport`, and `ReturnToObjective` while keeping Runtime movement, reservations, damage, and events authoritative.
- Routed local-combat movement through existing tick resolver, target acquisition, navigation topology, occupancy, and same-tick reservation paths.
- Added deterministic support-slot role tagging in navigation and preserved attack-slot preference before support movement.
- Added regression coverage for route-blocking MoveFirst response, full attack-slot support movement, and Hold leash rejection.
- Split resolver AI decision code and movement-intent registration to keep oversized-file guard passing.

## Verification Evidence

2026-05-28 headless verification:

- `dotnet run --project tests\AutoBattleRuntimeRegression\AutoBattleRuntimeRegression.csproj -v:minimal` passed.
- `dotnet run --project tests\BattleHitFeedbackRegression\BattleHitFeedbackRegression.csproj -v:minimal` passed.
- `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal` passed, including local-combat regression cases and oversized-file guard.
- `dotnet build rpg.csproj -maxcpucount:2 -v:minimal` passed with `0` warnings and `0` errors.
- `dotnet build-server shutdown` completed and closed MSBuild / compiler servers.

Manual QA remains pending for the authored-scene readability checks listed above. Do not mark this proposal accepted or archive it until manual QA is completed or explicitly waived.
