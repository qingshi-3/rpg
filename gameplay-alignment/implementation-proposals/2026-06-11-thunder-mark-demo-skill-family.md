# Thunder Mark Demo Skill Family Implementation Proposal

Status: In Progress

## Authority

- Originating design proposal: `design-proposals/archived/2026-06-11-thunder-mark-skill-family/proposal.md`.
- Amendment proposal: `design-proposals/archived/2026-06-13-thunder-fold-two-stage-targeting/proposal.md`.
- Implements `gameplay-design/details/combat-command/README.md`.
- Implements `system-design/battle-content-progression-architecture.md`.
- Implements `system-design/battle-runtime-architecture.md`.
- Follows `system-design/battle-command-architecture.md` for command acceptance and action locks.

## Goal

Build a first visible Runtime-backed thunder-mark demo kit for one hero:

```text
雷签飞投 -> 雷印折跃 -> 雷旋破
```

The first implementation must demonstrate mark creation, legal teleport placement near a mark, and a channeled melee damage window that can continue after teleport.

## Scope

- Add content ids and snapshots for three thunder-mark skills.
- Add Runtime battle-only mark state.
- Add Runtime effect primitives for mark creation, teleport to mark, and channeled area damage.
- Let the existing hero skill command path submit the three skills.
- Add semantic Runtime events for mark creation, teleport, and channeled damage so Presentation and reports can later consume them.
- Route teleport through the shared Runtime displacement commit boundary so post-teleport movement, perception, target choice, and local-combat facts rebuild from the new anchor.
- Change Thunder Mark Fold to the accepted two-stage command flow: first select a live owned mark, then render and select an empty legal landing anchor within radius 3 around that selected mark before submitting Runtime command intent.
- Carry the selected mark reference and landing anchor into Runtime command validation so Runtime does not reinterpret the command as "use any live mark."
- Replace the battle-runtime pause HUD's single-skill surface with a reusable hero switch row and selected-hero skill list.
- Align tactical-pause skill submission with the accepted UI rule: switchable heroes, one submitted skill intent at a time, idle-caster pending intent replacement, active-skill queueing, and hidden hero/skill controls after pause ends.
- Add focused regression coverage before production code.

## Non-Goals

- Do not implement `雷印转界` yet.
- Do not transfer units, projectiles, cast impact areas, or skill events.
- Do not add full projectile collision simulation; `雷签飞投` may resolve against the submitted target or selected ground cell through Runtime command facts.
- Do not add high-frequency individual soldier controls.
- Do not rebalance all first-slice hero skills.

## Touched Systems

- Gameplay and system authority docs.
- First-slice skill content definitions.
- Battle skill snapshot contracts.
- Runtime hero skill command resolver.
- Runtime effect resolver and battle-only state.
- Target battle architecture regression tests.
- World-site hero skill HUD only if required for multiple skills to be visible and submit through the existing path.

## Tests

- RED: thunder tag skill creates an attached mark on the targeted unit and emits a mark event.
- RED: thunder fold skill teleports the caster to a legal adjacent cell near the live mark and emits a teleport event.
- RED: thunder spiral skill applies repeated damage, and a fold during the channel moves the ongoing damage source instead of refreshing duration.
- Existing target battle architecture regression suite must remain green.
- Existing world-site deployment cache regression suite must remain green if HUD/content binding changes.
- Battle-runtime HUD regression must prove that hero switch buttons refresh the selected hero company before the selected hero's skill list is rebuilt.
- Runtime regression must prove that a newer accepted skill intent replaces an idle caster's older unstarted pending skill intent, while the existing active-skill waiting regression proves commands still queue behind casting or recovery.
- Battle-runtime HUD regression must prove that ending tactical pause hides the hero switch and skill command bar.
- Runtime regression must prove that Thunder Mark Fold clears stale movement, target lock, and local steering state instead of continuing a pre-teleport movement chain.
- Runtime regression must prove that Thunder Tag Throw is an offhand release: it applies damage/mark effects while the caster is moving without clearing the active movement segment, movement intent, or local steering.
- Runtime regression must prove that Thunder Mark Fold rejects before acceptance when the selected mark is missing, expired, belongs to another battle group, or the landing anchor is outside radius 3, occupied, or topology-illegal.
- Presentation/HUD regression must prove that Thunder Mark Fold uses two-stage targeting: clicking a marked unit or ground mark enters landing selection without submitting, landing candidates render around the selected mark, and only an empty legal landing cell submits the command.
- Runtime regression must prove that the accepted fold command uses the selected mark reference instead of silently resolving a different newer mark.

## Diagnostics And QA

- Runtime rejection/failure reasons must distinguish no selected live mark, wrong-owner mark, expired mark, no legal teleport anchor, occupied destination, invalid caster, invalid target, and unsupported transfer target.
- Teleport diagnostics must preserve source command attribution while making post-displacement state observable from Runtime facts, not Presentation-only motion.
- Manual QA after automated checks: launch battle with the thunder hero, use `雷签飞投`, `雷印折跃`, and `雷旋破`; confirm visible Runtime events drive damage/teleport rather than Presentation-only movement.

## Acceptance Evidence

- 2026-06-13: Fixed the remaining Thunder Mark Fold visible back-and-forth after teleport. Root cause was Presentation scheduling: `ThunderMarkTeleported` still entered the ordinary actor movement tail, so the snap could wait behind old movement/action backlog even though Runtime had already committed the displacement. Added a teleport hard-barrier path that advances the actor presentation generation, clears actor-local movement tail, movement start gate, and action tail, observes the teleport snap immediately, and skips stale queued actor actions after the barrier. RED/GREEN coverage added to `BattleHitFeedbackRegression`: the RED first failed because teleport still used `TrackActorMovement`, then failed because queued stale actions could still run after the barrier; GREEN passed after the hard-barrier implementation. Verification passed: `dotnet run --project tests\BattleHitFeedbackRegression\BattleHitFeedbackRegression.csproj -v:minimal`, `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal`, `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal`, and `dotnet build rpg.csproj -maxcpucount:2 -v:minimal`. `git diff --check` passed with only the existing CRLF warning for `WorldSiteRoot.BattleRuntimeCommandHud.cs`; `WorldSiteRoot*.cs` is 7308 lines and `BattleRuntimeHeroSkillCommandResolver.cs` is 902 lines.
- 2026-06-13: Latest Thunder Mark Fold repro logs showed Runtime resumed ordinary objective movement from the post-teleport anchor, but the teleport boundary did not expose displacement cleanup or stale presentation movement facts. Added an actor-local Presentation movement generation barrier so queued pre-teleport movement observers no-op after teleport, and added low-noise `BattleRuntimeThunderFoldDisplacementCommitted`, `BattleRuntimeTeleportMovementBarrier`, `BattleRuntimeStaleMovementSkipped`, and `BattleRuntimeTeleportPresentation` diagnostics. `dotnet run --project tests\BattleHitFeedbackRegression\BattleHitFeedbackRegression.csproj -v:minimal`, `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal`, `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal`, and `dotnet build rpg.csproj -maxcpucount:2 -v:minimal` passed. `git diff --check` passed with only the existing line-ending warning for `WorldSiteRoot.BattleRuntimeCommandHud.cs`; `WorldSiteRoot*.cs` is 8196 lines and `BattleRuntimeHeroSkillCommandResolver.cs` remains 995 lines.
- 2026-06-13: Implemented Thunder Mark Fold two-stage targeting. HUD selection now first accepts only a live owned thunder mark, then renders empty legal landing anchors within radius 3 around that selected mark, and submits the selected Runtime spatial mark id with the landing cell.
- 2026-06-13: `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal` passed. Runtime regressions now cover required selected mark payload, selected-mark reference use, occupied landing rejection, legal radius validation, and displacement cleanup after teleport.
- 2026-06-13: `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal` passed. Presentation/HUD regressions now cover explicit mark-selection and landing-selection stages, landing preview rendering, request payload forwarding, and the `WorldSiteRoot*.cs` anti-rot budget.
- 2026-06-13: `dotnet run --project tests\BattleHitFeedbackRegression\BattleHitFeedbackRegression.csproj -v:minimal` passed after confirming thunder skill presentation and runtime playback remain stable.
- 2026-06-13: `dotnet build rpg.csproj -maxcpucount:2 -v:minimal` passed with 0 warnings and 0 errors. `git diff --check` passed with only the existing Git line-ending warning for `WorldSiteRoot.BattleRuntimeCommandHud.cs`.
- 2026-06-13: `BattleRuntimeHeroSkillCommandResolver.cs` is 995 lines after moving thunder-mark validation into a focused partial; `WorldSiteRoot*.cs` total line count is 8195, below the 8200 anti-rot budget.
- 2026-06-12: Added and verified the `runtime thunder tag preserves moving caster state` RED/GREEN regression. RED first failed because skill snapshots had no explicit `ReleasesWithoutOccupyingCaster` trait, then failed because Runtime still waited behind movement; GREEN adds the trait to the definition/snapshot contract and resolves immediate offhand skills without entering skill casting, clearing movement intent, or consuming the actor's movement decision slice.
- 2026-06-12: `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal` passed after the offhand thunder tag fix.
- 2026-06-12: `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal` passed after confirming the production thunder tag snapshot carries the offhand release trait.
- 2026-06-12: `dotnet build rpg.csproj -maxcpucount:2 -v:minimal` passed with 0 warnings and 0 errors after the offhand thunder tag fix.
- 2026-06-12: `git diff --check` passed, and `WorldSiteRoot*.cs` total line count is 7222.
- 2026-06-12: Added and verified the `runtime thunder fold clears stale displacement context` RED/GREEN regression. RED failed because Thunder Mark Fold left the old retained target on the displaced hero; GREEN routes teleport through the shared Runtime displacement commit boundary, clearing stale target lock, reservations, movement segment, movement intent snapshot, local steering, and backtrack guards.
- 2026-06-12: `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal` passed after the displacement-boundary fix.
- 2026-06-12: `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal` passed after the displacement-boundary fix.
- 2026-06-12: `dotnet build rpg.csproj -maxcpucount:2 -v:minimal` passed with 0 warnings and 0 errors after the displacement-boundary fix.
- 2026-06-12: Runtime log inspection for the one-then-three skill repro showed the thunder hero continued to receive ordinary `movement_started` events after `first_slice_skill_thunder_spiral_break` was accepted. The fix treats `StartChanneledAreaDamage` duration as a Runtime skill lock, keeps ordinary movement blocked while the channel is live, and still allows `TeleportToThunderMark` to interrupt the active channel without refreshing the channel duration.
- 2026-06-12: `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal` passed after adding `runtime thunder spiral channel blocks ordinary movement`.
- 2026-06-12: `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal` passed after the channel-lock fix. Existing nullable and Godot generator warnings remain in the regression project.
- 2026-06-12: `dotnet build rpg.csproj -maxcpucount:2 -v:minimal` passed with 0 warnings and 0 errors.
- 2026-06-12: `git diff --check` passed, and `WorldSiteRoot*.cs` total line count is 8192, below the anti-rot budget of 8200.

- 2026-06-12: Latest runtime log inspection showed `雷签飞投` target picking started but no submit event on empty-ground clicks, while `雷印折跃` submitted successfully without any prior `ThunderMarkCreated` event. The follow-up fix treats thunder tag as actor-or-cell targeted, gates thunder fold on live Runtime marks, and snaps teleport presentation instead of letting old movement lanes continue.
- 2026-06-12: `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal` passed. The suite now covers empty-ground or attached thunder tag targeting, thunder fold HUD unavailability without a live mark, HUD pointer gating during target picking, teleport snap presentation, hidden hero controls after pause ends, and the `WorldSiteRoot*.cs` anti-rot budget. Existing test warnings remain in the regression project.
- 2026-06-12: `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal` passed. Runtime regressions cover ground mark creation, attached mark creation, fold rejection without a live mark, legal fold near a live mark, and spiral continuation after fold.
- 2026-06-12: `dotnet build rpg.csproj -maxcpucount:2 -v:minimal` passed with 0 warnings and 0 errors.
- 2026-06-12: `WorldSiteRoot*.cs` total line count is 8192, below the anti-rot budget of 8200, after moving teleport presentation and command HUD helpers out of the root partials.
- 2026-06-11: `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal` passed. The battle-runtime HUD regression now verifies the authored hero switch row, reusable hero switch button scene, selected-group presenter, and multi-slot skill list.
- 2026-06-11: `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal` passed. The thunder tag, thunder fold, thunder spiral channel, and oversized-file guard regression cases passed.
- 2026-06-11: `dotnet build rpg.csproj -maxcpucount:2 -v:minimal` passed with 0 warnings and 0 errors.
- 2026-06-11: `dotnet run --project tests\TargetBattleArchitectureRegression\TargetBattleArchitectureRegression.csproj -v:minimal` passed after adding the idle-caster replacement regression. Runtime now accepts the newer skill intent, emits `skill_intent_superseded` for the older unstarted pending command, and releases only the latest intent when the caster is not already casting or recovering.
- 2026-06-11: `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal` passed after adding the pause-off HUD regression. Ending tactical pause now hides the hero switch and skill command bar while retaining battle observation input.
- 2026-06-11: `dotnet build rpg.csproj -maxcpucount:2 -v:minimal` passed with 0 warnings and 0 errors after the tactical-pause queue/UI correction.
- 2026-06-11: `WorldSiteRoot*.cs` total line count is 8199, below the anti-rot budget of 8200. `WorldSiteRoot.SiteManagementHud.cs` is 1032 lines, matching its accepted per-file budget after binding the hero switch presenter.
- 2026-06-11: Replaced the assault hero company's old `first_slice_skill_whirling_break` placeholder with `first_slice_skill_thunder_tag_throw` as the advertised starter skill, and removed the old whirling skill definition from the active first-slice snapshot. The thunder demo hero now exposes the three-skill kit without requiring tactical-pause hero switching.
- 2026-06-11: Launched the project with `C:\Users\qs\Desktop\Godot_v4.5.1-stable_mono_win64\Godot_v4.5.1-stable_mono_win64.exe --path D:\godot\rpg`. Startup logs reached strategic world initialization and world ticks without startup errors. Existing Godot UI anchor-size warnings remain unrelated to this skill work.
- Manual battle QA is still pending: enter a battle with the thunder hero and cast the three skills in order to confirm visible damage, mark/fold behavior, and continuing channel presentation in the live window.
