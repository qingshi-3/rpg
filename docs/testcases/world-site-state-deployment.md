# WorldSite State Deployment Manual Checks

These checks cover the site-state-driven deployment path.

## East-Side Bonefield Entry

Setup:
- Start from the strategic world.
- Move a player assault army so it approaches Bonefield from the east side.

Expected:
- The resulting `BattleStartRequest` has `AttackDirection = East`.
- `WorldSiteRoot` logs `SiteDeploymentCacheBuilt`.
- `WorldSiteDeploymentService` logs `BattlePlacementsEnsured` for the player force with `direction=East`.
- Player units spawn on east-side deployment candidates, not the old fixed fallback cells.

## Raid Entry

Setup:
- Let the Graveyard Raid army reach Bonefield.

Expected:
- The target `WorldSiteState.UnitPlacements` receives non-garrison enemy battle placements.
- Resident garrison placement records remain intact.
- Enemy Raid units do not spawn on occupied resident garrison cells.

## Missing Placement Failure

Setup:
- Use a broken site map or deployment cache with no walkable deployment surfaces.

Expected:
- `WorldSiteRoot` logs deployment preparation failure.
- The battle launch is cancelled.
- No battle units are spawned from hardcoded fallback coordinates.
- The UI returns to a non-battle state with a clear failure notice.

## Mixed Forces

Setup:
- Enter a battle with resident garrison plus an incoming attacker.

Expected:
- Resident forces read existing garrison placements.
- Incoming forces receive `Attacker`, `Defender`, or `FieldArmy` placement kinds.
- No two units share the same deployment cell.

## Post-Battle Site Management

Setup:
- Finish a site battle with surviving units.
- Stay in the site management view.
- Drag one surviving unit to another valid walkable cell.

Expected:
- Surviving units display their configured animated unit visuals, not circular fallback markers.
- Dragging starts from the unit/cell and writes the matching `WorldSiteState.UnitPlacements` cell.
- Invalid drop cells still show invalid placement feedback and do not update state.

## Re-Enter Resolved Site

Setup:
- Finish a site battle.
- Return to the strategic world.
- Enter the same `WorldSite` again.

Expected:
- Unit count matches `WorldSiteState.UnitPlacements`.
- Units keep animated visuals and idle animation.
- No stale `Attacker`, `Defender`, or `FieldArmy` placement markers appear in a peacetime or aftermath site.
- Units appear at the last persisted placement cells, including attacker units converted into garrison after capture.

## Test Speed Settings

Setup:
- Open a battle with the current test units.
- Open the strategic world.

Expected:
- `militia` move range is 6.
- `player_knight` move range is 8.
- `skeleton_warrior` move range is 6.
- `skeleton_archer` move range is 4.
- The strategic world clock starts at 4x.
