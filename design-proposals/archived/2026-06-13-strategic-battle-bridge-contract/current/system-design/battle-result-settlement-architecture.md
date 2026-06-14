# Battle Result And Settlement Architecture

Status: Accepted Architecture

## Gameplay Authority

This document supports hero-led light RTS combat by defining how battle runtime facts become campaign consequences and player-readable reports.

## Responsibility

Report and Settlement consume Runtime outputs and produce long-term consequences:

- battle outcome interpretation;
- settlement plan creation;
- long-term state deltas;
- battle report records;
- recovery, loss, reward, and failure attribution.

## Does Not Own

Report and Settlement do not own:

- simulating combat;
- redefining content or ability effects;
- recomputing movement, damage, or target facts independently from Runtime;
- UI animation or playback;
- raw map topology parsing.

## Snapshot And Result Contracts

| Contract | Owner | Purpose |
|---|---|---|
| `BattleStartSnapshot` | Application | Frozen battle input from long-term state and definitions. |
| `BattleGroupSnapshot` | Application | Initial facts for one battle group in one battle. |
| `LocationBattleContext` | Application | City/location inputs such as defense, facilities, entrances, terrain context, and compiled navigation topology. |
| `CommandRequest` | Presentation/Application | Player intent before full runtime execution. |
| `RuntimeOrder` | Runtime | Accepted command converted into executable runtime order. |
| `BattleEventStream` | Runtime | Semantic event source for report, settlement, UI feedback, and diagnostics. |
| `BattleOutcomeResult` | Runtime | Battle termination, outcome, losses, rewards, and result summary. |
| `SettlementPlan` | Application | Proposed long-term changes derived from result and events. |
| `StateDeltaSet` | Application | Applied long-term mutations. |
| `BattleReportRecord` | Application/Presentation | Player-readable explanation of already-settled facts. |

## Shared Source Contract

Report and Settlement share the same event source. Report explains; Settlement writes. They must not derive separate truths.

- Runtime emits `BattleEventStream` and `BattleOutcomeResult`.
- Settlement derives state changes from the same event/result source.
- Report derives explanation from the same event/result source.
- UI may display report facts but must not compute new report truth.
- Any loss, reward, resource change, or recovery requirement used by Settlement must be traceable to result or event facts.

Events with settlement or explanation value should express, when applicable:

```text
actor
source command
source action
source definition
target
effect type
resource delta
failure reason candidate
```

`resource delta` is optional for non-resource events. Do not fill fake values just to satisfy a shape.

If the source is environment, city facility, equipment, passive effect, AI fallback, or system interruption, the event must state that source explicitly.

Skill, basic-attack, equipment, relic, terrain, and support effects should enter reports through the same Runtime effect-result events. Reports may group these facts into player-readable summaries, but they must not recompute whether a skill hit, whether damage happened, or whether an effect failed.

Skill failure events should preserve the reason needed for report attribution, such as target dead before release, caster defeated, cost unavailable, cooldown unavailable, interrupted cast, or blocked by action-lock rules. A targeted skill whose locked target moved out of range after command acceptance is not a range-failure event by itself.

## Settlement Flow

```text
Runtime emits EventStream + BattleOutcomeResult
-> Settlement builds SettlementPlan
-> Application validates and applies StateDeltaSet
-> Report builds BattleReportRecord from the same facts
-> UI shows settled result and explanation
```

Settlement writes rewards, losses, experience, recovery entry points, and city/location changes. Runtime does not write long-term resources.

## Loss And Recovery

`CorpsStrength` is the long-term corps strength value and uses the accepted 0-100 range. Visible soldiers are Runtime/Presentation mapping only. Long-term state does not track individual soldier identity, individual soldier experience, individual soldier equipment, or individual soldier casualty records.

`CorpsStrength` loss is applied after battle through Settlement. Recovery depends on battle outcome, retreat state, city support, available resources, and facility capacity. If resources are insufficient, the system leaves an explicit unrecovered state instead of silently restoring strength.

Hero down in ordinary battle is a battle-state consequence unless a future accepted proposal defines campaign-level death or injury rules. V0 should communicate loss of contribution, not permanent hero deletion.

## Failure Attribution

Failure candidates are created where the event happens and ranked during report generation. Typical candidates include:

- frontline collapsed;
- hero overextended during assault;
- ranged corps lacked protection;
- cavalry was countered by spear, terrain, or chokepoint pressure;
- key skill failed because of mana, cooldown, or target state;
- corps equipment level was too low;
- city defense or recovery support was insufficient;
- retreat was ordered too late, causing high corps loss.

## Failure Rules

- Settlement must not accept incomplete runtime output.
- Runtime exception, player retreat, battle interruption, normal victory, and normal defeat remain distinct termination reasons.
- Incomplete or failed battle handoff must enter explicit rollback, pending manual resolution, or failed handoff state.
- Settlement cannot fabricate victory, defeat, rewards, or losses to hide missing runtime facts.

## Acceptance

This architecture is acceptable when:

- Settlement and Report consume the same runtime facts;
- campaign state is mutated only after complete runtime output is available;
- report explanations can trace command, build, skill, terrain, city support, recovery, and AI fallback causes;
- recovery and loss states are explicit instead of hidden by automatic restoration.
