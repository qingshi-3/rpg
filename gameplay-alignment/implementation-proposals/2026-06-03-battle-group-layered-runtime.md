# Battle Group Layered Runtime Implementation Proposal

Status: Implemented - Pending Manual QA

## Requirement Id

BG-LAYERED-RUNTIME-2026-06-03

## Originating Design Proposal

`design-proposals/archived/2026-06-03-battle-group-layered-runtime/`

## Accepted Authority

- `system-design/hero-led-light-rts-system-architecture.md`
- `system-design/battle-runtime-architecture.md`
- `system-design/battle-group-tactical-region-architecture.md`
- `system-design/battle-ai-boundary-architecture.md`
- `system-design/battle-command-architecture.md`

## Goal

Make live battle runtime follow the layered hero-led model: continuous observation facts, one battle-group commander state per command group, actor action states for execution only, and runtime validation as final legality authority.

## Scope

- Preserve migration adapters, but make expanded force-count rows carry a shared runtime commander id when they belong to the same hero company or source command group.
- Add runtime group commander state separate from actor action phase and actor cached diagnostics.
- Make tactical observation, perception summaries, tactical regions, local combat regions, and engagement transitions use the commander id.
- Allow player-commanded groups to enter player-scoped engagement from perception or combat facts without enemy policy overwriting player intent.
- Distinguish static attack-slot reachability from executable next-step reachability. When attack entry is dynamically blocked, prefer named support or queue behavior before terminal path failure.
- Keep state-transition diagnostics low-noise and focused on meaningful group state changes, target locks, engagement, local-combat entry or exit, regroup, retreat, defeat, and important degradation reasons.

## Non-Goals

- Do not add the full hero skill command system.
- Do not rebuild persistent `BattleGroupState` or campaign save schema.
- Do not replace the whole snapshot contract with a final multi-member battle-group model in this slice.
- Do not introduce LimboAI behavior trees in this slice.
- Do not redesign UI command surfaces beyond preserving command-group identity.

## Touched Systems

- Application battle snapshot preparation: derive runtime commander identity from force source or accepted plan key.
- Runtime battle state: store commander state independently from actors.
- Runtime tactical observation: group by commander id, not force-count row id.
- Tactical region and engagement state: support player-scoped engagement and shared group ownership.
- AI target/local-combat selection: use executable next-step reachability for attack/support fallback.
- Diagnostics: keep logs low-noise and state-oriented.

## Tests

- Snapshot preparation regression: one player source with hero and multiple corps force-count rows produces actors sharing one runtime commander id.
- Tactical perception regression: multiple actors in the same commander group contribute to one perception summary and one local combat region.
- Player-scoped engagement regression: a player-commanded group enters engaged local combat from valid perception without changing to enemy tactical mode or losing objective intent.
- Local join regression: when an attack slot is statically reachable but all improving first steps are occupied, the actor requests support or queue behavior instead of generic `path_not_found`.
- Architecture regression: actor phases remain separate from group commander state; state transition events are keyed by group commander identity.

## Diagnostics

- Log commander state transitions only when the state or meaningful target/local-combat assignment changes.
- Log player-scoped engagement entry with a reason that distinguishes it from enemy policy activation.
- Log blocked local-combat entry as a named degradation reason instead of a generic path failure when static reachability exists.

## Manual QA

- Start the bonefield battle with the V0 hero/corps setup.
- Confirm all player units from the same hero company react as one command group when contact starts.
- Confirm enemy units from the same defender group join the same local fight instead of some continuing past the fight as independent groups.
- Confirm rear units blocked by front units move to support or queue positions instead of standing still with repeated path failure.
- Confirm deployment footprint overlap does not regress.

## Acceptance Evidence

- 2026-06-03: `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj --no-restore` passed. This covers shared runtime commander identity, commander-keyed perception, player-scoped engagement, local-combat join/support fallback, deployment footprint overlap, continuous movement handoff, runtime diagnostics, event goldens, and architecture guards.
- 2026-06-03: `dotnet build rpg.csproj -maxcpucount:2 -v:minimal` passed with 0 warnings and 0 errors.
- 2026-06-03: Added and passed the rear blocked-ingress regression for the bonefield-style 2x2 enemy formation. When a retained local-combat target has statically reachable slots but no executable next step because live footprints block ingress, Runtime now records and logs `reject_no_reachable_slot` with `attemptedNext=none` instead of generic `path_not_found`.
- 2026-06-03: `git diff --check` passed; only existing CRLF/LF normalization warnings were reported for touched system-design markdown files.
- Manual QA remains pending for the bonefield battle scenario and should confirm the hero company reacts as one command group, enemy defenders join the same local fight, rear units support or queue instead of idle path failure, and deployment footprints stay non-overlapping.
