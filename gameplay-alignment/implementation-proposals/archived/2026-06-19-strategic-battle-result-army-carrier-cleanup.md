# Strategic Battle Result Army Carrier Cleanup

Status: Accepted And Archived

## Authority

- `gameplay-design/content-systems-long-term-design.md`
- `system-design/strategic-management-system-architecture.md`
- `system-design/strategic-battle-bridge-architecture.md`
- `system-design/battle-result-settlement-architecture.md`

## Scope

Fix Strategic Management battle result return so a resolved expedition cannot keep its temporary world-map army carrier in `Attacking` / `AssaultSite` state after the battle result is applied.

The Strategic Management result command remains the authority for expedition, hero, corps, location, reward, and feedback facts. The world-map `WorldArmyState` for `strategic:<expedition>` is only the current movement and battle-entry adapter, so it must be retired after the strategic result writeback succeeds.

## Non-Goals

- Do not change battle Runtime outcome rules, settlement formulas, rewards, or AI.
- Do not make the Strategic Battle Bridge own durable strategic mutation.
- Do not revive legacy world result application for Strategic Management battles.

## Touched Systems

- Strategic Battle Bridge session validation.
- World army command Application boundary.
- World-site strategic battle result cleanup.
- Strategic Management and world-site regression tests.

## GodotPrompter Skills

- `godot-debugging`
- `godot-testing`

## Tests

- Add a regression proving the bridge rejects sessions for already resolved expeditions.
- Add a regression proving the world army command boundary removes resolved strategic expedition carriers.
- Add a source guard proving world-site strategic result cleanup routes carrier removal through the Application boundary after Strategic Management writeback.

## Diagnostics

Carrier cleanup should emit a low-noise log with army id, expedition id, previous status, previous intent, and cleanup reason.

## Manual QA

Start a Bonefield assault, finish the battle, return to the strategic map, then confirm the old attacking expedition no longer reopens the battle gate and the map can continue normally.

Status: Closed by user archive confirmation on 2026-06-20.

## Acceptance

- A resolved expedition cannot create a new strategic battle session.
- The resolved strategic world-army carrier is removed from the large-map state after successful result writeback.
- No direct legacy result applier handles Strategic Management battle consequences.

## Verification

- 2026-06-19: `dotnet run --project tests\StrategicManagementRegression\StrategicManagementRegression.csproj -v:minimal`
- 2026-06-19: `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal`
- 2026-06-19: `dotnet build rpg.csproj -maxcpucount:2 -v:minimal`
