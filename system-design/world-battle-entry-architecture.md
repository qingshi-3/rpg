# World Battle Entry Architecture

Status: Retired Architecture

## Retirement

This document is retired as the long-term strategic-to-battle authority.

The old world battle entry architecture was built around legacy strategic-world state, `BattleStartRequest`, `BattleSessionHandoff`, `BattleResult`, `WorldBattleRequestBuilder`, and `WorldBattleResultApplier`.

New Strategic Management to battle integration is owned by:

```text
system-design/strategic-battle-bridge-architecture.md
```

## Reason

The legacy entry path mixed multiple responsibilities in one request/result package:

- strategic source IDs;
- old world army, garrison, and site state;
- battle participants;
- battle-preparation draft facts;
- map and navigation snapshots;
- scene paths and return routing;
- battle result writeback.

That shape is not compatible with the accepted clean Strategic Management architecture, where strategic state mutates only through Strategic Management commands and battle Runtime consumes immutable `BattleStartSnapshot` values.

## Legacy Constraint

Existing code may still contain this path during migration, but it must not receive new Strategic Management behavior.

If a temporary adapter is needed, it must be explicitly scoped by the confirmed active work item, convert old facts into the accepted Strategic Battle Bridge and battle snapshot/result contracts, and remain removable.
