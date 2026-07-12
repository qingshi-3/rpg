# Enemy Tactical Intent First Slice

Status: Accepted - Archived

## Relationship Metadata

| Field | Value |
|---|---|
| Requirement Id | BTIA-001-ENEMY-FIRST-SLICE |
| Originating Design Proposal | `design-proposals/archived/2026-06-16-battle-tactical-intent-architecture/` |
| Authority Documents | `system-design/battle-tactical-intent-architecture.md`; `system-design/battle-ai-boundary-architecture.md`; `system-design/battle-group-tactical-region-architecture.md`; `system-design/battle-runtime-architecture.md`; `system-design/battle-navigation-topology-architecture.md`; `gameplay-design/content-systems-long-term-design.md`; `gameplay-design/details/combat-command/README.md` |
| Amendment Proposals | None |
| Blocking Issues | None known |
| Verification Records | `dotnet build tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -maxcpucount:2 -v:minimal` and `dotnet run --no-build --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj` passed on 2026-06-16. User requested archive on 2026-06-16 after follow-up Presentation input-boundary bug fix was verified. |

## Goal

Connect enemy-controlled battle groups to the new Tactical Intent boundary so non-engaged enemy movement uses stable intent-owned targets instead of repeatedly replacing its long-term movement target with volatile player-cluster temporary regions.

## Scope

In scope:

- introduce first-slice runtime/config DTOs for enemy AI intent plans;
- build a minimal battle target object catalog from existing deployment/objective regions and runtime group/cluster facts;
- resolve enemy group intent through the priority chain: explicit group plan, enemy group default, scenario default, safe fallback;
- route enemy non-engaged movement through a stable resolved target object or target region;
- keep player-cluster temporary regions as volatile tactical observations unless the active enemy intent explicitly allows them;
- add low-noise diagnostics for selected intent, resolved target, retained target, rejected retarget, and fallback;
- keep existing local combat and attack-slot behavior after engagement.

## Non-Goals

- Do not migrate player-commanded movement, player battle plans, or player self-calculated fallback logic.
- Do not change Presentation movement interpolation, animation, or entity movement lanes.
- Do not add new Godot editor UI for authoring AI intents.
- Do not replace the local combat slot solver.
- Do not add campaign-persistent strategic enemy AI.
- Do not hardcode scenario-specific movement behavior for siege, defense, field battle, or enemy fantasy.

## Touched Systems

- `src/Application/Battle/` snapshot/request models: carry enemy group intent input when available and provide safe scenario defaults.
- `src/Runtime/Battle/Tactics/`: target object catalog, intent plan resolution, target retention, and temporary-region gating.
- `src/Runtime/Battle/`: commander/tick integration so enemy region movement consumes the resolved intent target.
- Existing battle diagnostics: add low-noise intent and target resolution logs.
- Regression tests under battle-focused test projects.

## Design Constraints

- Business layers may choose intent ids, target selectors, style ids, leash selectors, retarget policies, and fallback ids. They must not implement per-tick movement strategy.
- Runtime-observed player clusters are volatile. They cannot replace stable non-engaged enemy targets unless the active intent explicitly authorizes cluster pursuit.
- Player-commanded groups must keep their current movement behavior.
- Runtime remains the final validator for topology, footprint, occupancy, reservations, movement, attacks, damage, and outcome.

## Proposed First-Slice Behavior

For the current bonefield battle, enemy groups should receive a stable fallback intent equivalent to:

```text
IntentId = AssaultTarget
PrimaryTargetSelector = PlayerDeploymentRegion
RetargetPolicy = StableUntilInvalid
FallbackIntent = HoldOrAdvanceDefault
```

This preserves the existing broad enemy behavior, but prevents the player-cluster temporary region from replacing the group's non-engaged movement target every few runtime ticks. Temporary clusters may still help local combat once engagement has started.

## Tests

Automated verification should cover:

- enemy intent resolution uses explicit/default/fallback priority;
- non-engaged enemy target remains stable while player clusters move;
- temporary cluster retarget is rejected when active intent does not authorize it;
- player-commanded group movement output remains unchanged for existing player plan fixtures;
- local combat can still select actor targets after engagement.

## Diagnostics

Expected new or updated low-noise logs:

- enemy intent selected, including source;
- target selector resolved to target object or region;
- target retained across temporary cluster refresh;
- volatile cluster retarget rejected because active intent does not allow it;
- fallback intent used because selector resolved no valid target.

## Manual QA

Manual QA should use the current battle scene and compare the latest runtime log against the pre-change symptom:

- enemy action-zone target changes before engagement should drop sharply;
- enemy actor direction changes before engagement should drop;
- player movement should look and behave unchanged;
- enemy movement should appear visually continuous without Presentation changes.

## Acceptance Evidence

Automated verification:

- `dotnet build tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -maxcpucount:2 -v:minimal` passed on 2026-06-16 with 0 warnings and 0 errors.
- `dotnet run --no-build --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj` passed on 2026-06-16.
- Added regression coverage for enemy AI intent config entering the battle snapshot by group key.
- Added regression coverage that default enemy intent retains a stable fixed region instead of replacing it with a volatile player-cluster temporary region.
- Kept explicit volatile-cluster pursuit available through `RetargetPolicyId = AllowVolatileObservation`.
- Existing player-commanded movement and player autonomous temporary-region tests still pass.

Implementation notes:

- Enemy intent input now travels through `BattleStartRequest.EnemyAiIntentPlan`, `BattleStartRequest.EnemyAiIntentPlans`, `BattleForceRequest.AiIntentPlan`, and `BattleGroupSnapshot.AiIntentPlan`.
- Runtime tactical state normalizes enemy intent to a stable safe fallback when no authored intent exists.
- Player-commanded movement, player battle plans, player self-calculated fallback targeting, and Presentation interpolation were not migrated in this slice.

Manual QA / archive acceptance:

- User requested archive on 2026-06-16.
- Enemy Tactical Intent first slice remains enemy-only; player movement migration is still intentionally deferred.
- Follow-up Presentation input-boundary regression was fixed and verified separately after the archive request context exposed a strategic-map UI camera leak.
