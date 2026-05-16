# Subagent Handoff Rules

## Purpose

This document defines how future main agents should delegate auto-tactics migration work without losing the main direction.

## Main Agent Reading Set

Main agents should start with:

- `docs/README.md`
- `docs/50-production/technical-changes/2026-05-16-auto-tactics-migration.md`
- `docs/30-technical-design/architecture/global-system-decomposition.md`
- `docs/30-technical-design/world/strategic-world-v1-battle-contract.md`
- `docs/50-production/roadmap/development-priority.md`

The main agent should not load every detailed child document unless assigning or reviewing that workstream.

## Worker Reading Set

Each worker should receive:

- the main migration document;
- exactly one focused child document;
- the local code files named by that child document;
- relevant QA document entries.

Do not ask a worker to solve deployment extraction, runtime split, battle runtime, playback UI, and legacy retirement in one task.

## Delegation Shape

Good worker task:

```text
Extract deployment cache construction from WorldSiteRoot according to 03-deployment-cache-extraction.md. Do not change battle runtime behavior.
```

Bad worker task:

```text
Migrate battles to auto tactics.
```

## Review Checklist

The main agent must verify:

- the worker stayed inside the assigned workstream;
- no new authority was created for `WorldSiteState.UnitPlacements`;
- `BattleStartRequest` / `BattleResult` boundaries were preserved;
- no new feature was added to the legacy manual command loop;
- docs and QA entries were updated with the same change;
- logs are low-noise and useful for state transitions or failures.

## Handoff Between Workstreams

A workstream is ready for the next worker when:

- its acceptance checks pass or are explicitly marked unverified with reason;
- changed file paths are listed;
- new public contracts are documented;
- known risks are recorded in the relevant child document or QA entry.

## Mainline Drift Guard

If a worker proposes a change that requires battle runtime to mutate strategic world state, make `BattleStartRequest` own site-local placement state, or expand AP/manual command play as the target, stop the task and update the plan before coding.
