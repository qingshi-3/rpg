# Battle Combat-Zone Overlap Engagement Implementation Proposal

Status: Implemented - Automated Verification Passed

## Authority

- Implements `system-design/battle-runtime-architecture.md` for battle-group commander state as the owner of tactical intent.
- Implements `system-design/battle-group-tactical-region-architecture.md` for combat zones as global facts and group action zones as commander-owned intent.
- Implements `system-design/battle-ai-boundary-architecture.md` for player-scoped local combat entry from combat-zone overlap.

## Scope

- Treat membership in an active global combat zone as a battle-group engagement observation.
- Keep player-commanded groups in player-scoped engagement when their members are inside an active combat zone.
- Rebuild group action zones from the commander engagement state so company intent and actor movement consume the same combat-zone target.

## Non-Goals

- No new player commands or battle-preparation UI.
- No change to combat-zone clustering rules, slot selection, damage, or settlement.
- No enemy policy rewrite beyond consuming the same engagement observation path.

## Touched Systems

- Battle tactical observation refresh order.
- Battle-group engagement state machine.
- Battle tactical reason codes.
- Target battle architecture regression tests.

## Tests

- Add a regression where a player-commanded group has no local perception but one member is linked into an active combat zone; the group must enter scoped engagement and publish a `CombatJoin` action zone.
- Run `tests\TargetBattleArchitectureRegression`.

## Diagnostics And QA

- Reuse existing low-noise engagement transition logs and tactical area snapshots.
- Manual QA: deploy companies to separate target regions, let side companies join the surviving middle fight, and confirm they stop alternating between the original target region and the active fight while the combat zone exists.

## Acceptance Evidence

- `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal` passed on 2026-06-09 after adding combat-zone overlap engagement.
- `git diff --check` passed on 2026-06-09.
