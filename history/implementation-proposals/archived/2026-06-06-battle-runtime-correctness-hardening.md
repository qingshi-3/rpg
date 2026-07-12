# Battle Runtime Correctness Hardening

Status: Archived - aligned with current Runtime correctness hardening implementation; manual QA not retained as active work per user cleanup request on 2026-06-07

## Relationship Metadata

Requirement Id: `REQ-BATTLE-RUNTIME-CORRECTNESS-HARDENING`

Originating Design Proposal: None. This is a focused implementation slice against already accepted battle Runtime, navigation, tactical-region, and settlement architecture.

Parent Implementation Proposal: None

Related Implementation Proposals:

- `gameplay-alignment/implementation-proposals/archived/2026-06-06-battle-local-combat-stuck-recovery.md`
- `gameplay-alignment/implementation-proposals/archived/2026-06-06-battle-navigation-soft-zone-cache-optimization.md`
- `gameplay-alignment/implementation-proposals/archived/2026-06-06-zone-first-unit-decision-flow.md`

Supersedes: None

Superseded By: None

Amends: None

Amended By: None

Blocking Issues: The source review in `C:\Users\qs\Desktop\系统优化建议.txt` was not written against the latest tree. Each accepted item in this slice must be verified against current code before implementation.

Verification Records:

- 2026-06-06: `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal` passed after adding the runtime correctness regressions. Existing test-project warnings remain in that run.
- 2026-06-06: `dotnet build rpg.sln -maxcpucount:2 -v:minimal` passed with 0 warnings and 0 errors.

## Authority

- `gameplay-design/content-systems-long-term-design.md`
- `system-design/battle-runtime-architecture.md`
- `system-design/battle-navigation-topology-architecture.md`
- `system-design/battle-group-tactical-region-architecture.md`
- `system-design/battle-result-settlement-architecture.md`

No gameplay authority change is intended. The accepted architecture already requires deterministic Runtime event boundaries, explicit actor phase ownership, footprint-aware navigation legality, tactical-region identity preservation, and settlement-safe incomplete results.

## Architectural Judgment

This is a Runtime correctness slice, not a new combat design. Runtime remains the owner of actor HP, phases, movement boundaries, attack damage, tactical context consumption, navigation legality, and battle outcome consistency. The implementation must fix authoritative paths rather than hiding invalid state behind broad fallback behavior.

External review items are treated as findings to verify, not instructions to copy. Items that overlap active navigation optimization proposals stay out of scope unless they are a narrow correctness bug already covered by current accepted architecture.

## Scope

- Fix fixed-target region containment so tactical observation treats region centers as centers, not minimum cells.
- Preserve movement event boundaries when a moving actor is defeated, so `MovementStarted` cannot remain orphaned.
- Replace high-risk direct dictionary indexing in tick, attack, and tactical observation paths with explicit validated handling.
- Normalize faction comparisons in local-combat situation counts and local-fight detection.
- Keep stale-target retargeting inside the current tactical context instead of rebuilding with null tactical stores and zones.
- Fix diagonal side-footprint validation so same-level diagonal checks use the source-step height for side anchors.
- Make invalid completed battle controllers expose an incomplete outcome consistently without a controller/outcome completion mismatch.

## Non-Goals

- Do not implement `RetreatFirst`, `ProtectHero`, or `FireOnTheMove` gameplay behavior in this slice.
- Do not redesign target acquisition, combat-zone clustering, local-region soft constraints, flow-field cache keys, or reservation windows.
- Do not migrate `BattleRuntimeTickStartActorFact` to a full immutable snapshot.
- Do not rewrite event id formatting or settle the colon-delimiter cleanup.
- Do not add broad null-tolerant fallbacks for corrupted Runtime state. Prefer explicit validation, named failure, or fail-fast behavior.
- Do not run Godot editor/import workflows.

## Touched Systems And Files

Runtime correctness:

- Modify `src/Runtime/Battle/BattleTacticalObservationUpdater.cs`
- Modify `src/Runtime/Battle/BattleRuntimeTickResolver.cs`
- Modify `src/Runtime/Battle/BattleRuntimeActorStateMachine.cs`
- Modify `src/Runtime/Battle/BattleAttackResolver.cs`
- Modify `src/Runtime/Battle/BattleStaleAdvanceRetargeting.cs`
- Modify `src/Runtime/Battle/BattleRuntimeSessionController.cs`
- Modify `src/Runtime/Battle/Tactics/LocalCombatSituationBuilder.cs`
- Modify `src/Runtime/Battle/Navigation/BattlePathStepRules.cs`

Regression tests:

- Modify `tests/TargetBattleArchitectureRegression/Program.cs`
- Modify or create focused cases under `tests/TargetBattleArchitectureRegression/`

## Implementation Plan

1. Add RED regression coverage before production code changes.
   - Fixed-target region containment must not rebuild a temporary target region while a hostile is inside the centered fixed target region.
   - A moving actor defeated before its movement boundary must emit a movement completion or cancellation boundary event so presentation can clear movement state.
   - Local-combat faction counts must treat null or blank faction ids consistently with `NormalizeFaction`.
   - Same-tick stale-target retargeting must preserve tactical context and target a live in-zone enemy instead of falling back to global target scope.
   - Diagonal movement with a height-changing target must not validate side anchors at the target height.
   - `CompletedInvalid` must expose an incomplete controller state that matches its incomplete outcome.

2. Implement the minimal code needed to pass each failing test.
   - Keep changes local to the verified ownership path.
   - Add concise comments only where they document Runtime event or authority boundaries.
   - Avoid broad fallbacks that keep bad state moving silently.

3. Run focused regression after each fix group.
   - `dotnet run --project tests/TargetBattleArchitectureRegression/TargetBattleArchitectureRegression.csproj -v:minimal`

4. Run final low-concurrency verification.
   - `dotnet build rpg.sln -maxcpucount:2 -v:minimal`
   - `dotnet build-server shutdown`

## Tests

Target battle architecture regression must cover:

- centered fixed-target region containment;
- defeated moving actor movement-boundary cleanup;
- normalized faction comparisons in local combat;
- tactical-context-preserving stale-target retarget;
- source-height diagonal side validation;
- invalid battle handoff completion consistency;
- existing event-order goldens and navigation guards remain green.

## Diagnostics

- Keep logs low-noise.
- Do not log per-frame, per-node, or per-search diagnostics.
- If invalid actor ids or missing tick facts are encountered, emit a named Runtime action failure or throw a clear invariant exception at the authoritative boundary instead of allowing an unlabelled `KeyNotFoundException`.

## Manual QA

Bonefield runtime:

1. Let first contact and local combat form.
2. Confirm units that are killed while moving do not remain visually in a moving/interpolating state.
3. Confirm enemy target regions do not churn while player units remain inside authored fixed-target bounds.
4. Confirm local-combat retargeting stays inside the active combat area after same-tick target death.
5. Confirm no new overlap, same-tick swap, or unexplained global target jump appears.

## Acceptance Evidence

Automated regression and build evidence is recorded above. Manual QA is still pending, so this proposal is not yet accepted.
