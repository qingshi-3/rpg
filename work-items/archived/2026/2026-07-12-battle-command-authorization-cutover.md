# Battle Command Authorization Cutover

Status: Completed
Executor: executor-xhigh (gpt-5.6-sol/xhigh)
Verifier: Fresh independent root context (model/reasoning configuration not exposed by this Codex surface)
Created: 2026-07-12
Updated: 2026-07-12

## Objective

Make the production battle-command path enforce the accepted Presentation -> Application authorization -> Runtime acceptance lifecycle for destination-beacon and hero active-skill commands. Player input must not command enemy groups, spoof source actors, use unavailable/reserve groups, or submit a command channel that contradicts the compiled skill snapshot.

## Confirmed Discussion Result

The user authorized the accepted code-review remediation sequence to continue batch by batch and excluded `scenes/world/preview/` while user testing is active. This task is batch A3 after independently completed A1 transaction safety and A2 actor-cardinality/casualty repair.

The confirmed contract is:

1. Presentation creates command intent and local availability feedback only. It does not submit directly to `BattleRuntimeSessionController` as the production authorization boundary.
2. A focused Application command-submission service invokes `BattleCommandApplicationValidator` with the active battle snapshot/context before Runtime submission.
3. Application validates battle identity, player ownership, deployed/available battle-group identity, source actor identity/kind, and hero/corps/combined command channel.
4. Hero active-skill commands must match a skill compiled for the source hero/group and the command channel authored in that skill snapshot. Request fields cannot override the snapshot contract.
5. Runtime remains the final battle-context defense. Direct Runtime submission of an enemy-controlled player command, spoofed source actor, mismatched group, or mismatched skill channel fails explicitly and leaves command/tactical state unchanged.
6. Destination-beacon multi-selection keeps its existing atomic reachability behavior after Application permission succeeds.
7. Application rejection produces a typed failure/disabled reason for Presentation and diagnostics. It does not emit a false Runtime accepted event or mutate Runtime state.

## Authority Impact

No player-facing gameplay rule changes. Existing gameplay and `system-design/battle-command-architecture.md` already require Application permission and Runtime revalidation.

Before implementation, update only `system-design/battle-command-architecture.md` if needed to make these durable boundaries explicit:

- production Presentation routes all live/pause battle commands through one Application submission boundary;
- Application permission includes player ownership, deployed group, source actor, and snapshot-authored channel/skill eligibility;
- Runtime rejects forged or stale ownership/channel identity even when called outside Presentation;
- Application rejection is typed feedback and must not become an accepted Runtime event.

If implementation requires changing which factions the player may control, adding mind-control/shared-control rules, changing skill channels, or redefining accepted command timing, set this task to `Needs Discussion` and stop.

## Execution Scope

### Application Submission Boundary

- Reuse and extend `BattleCommandApplicationValidator`; it is explicitly not a deletion candidate.
- Add the smallest focused Application orchestration API that accepts active battle context/snapshot, player faction identity, command request, and Runtime controller.
- On success, pass the unchanged authorized intent to Runtime and return the Runtime acceptance/rejection result.
- On Application failure, return a typed rejection without calling Runtime or mutating battle state.

### Production Presentation Cutover

- Route the existing destination-beacon and hero-skill production submissions through the Application boundary.
- Preserve existing HUD targeting, pause behavior, local invalid-input feedback, destination preview, mark selection, and player-facing result handling.
- Remove only direct Runtime submission from those production routes; do not redesign the HUD or input gestures.

### Runtime Defense

- Revalidate battle/group/source identity and player ownership at Runtime submission.
- Validate hero skill definition and compiled command channel against the source battle group/hero snapshot.
- Reject enemy group, enemy actor, spoofed actor/group combinations, reserve/unavailable group, wrong battle, and mismatched authored channel without state/event mutation.
- Preserve valid player destination-beacon, hero skill, pause, supersession, and effect execution behavior.

### Behavior-Level Tests

- Use RED-GREEN-REFACTOR in the existing affected runners; add no framework or runner.
- Replace the existing positive expectation that an enemy-group skill command is accepted with the accepted rejection behavior.
- Cover player/enemy group, player/enemy source actor, hero/corps/combined channel, valid/forged source actor, missing/unavailable group, wrong battle identity, unknown skill, and authored-channel mismatch.
- Prove Application rejection does not call Runtime, emit accepted events, consume resources/cooldowns, or alter pending command/tactical state.
- Prove valid destination-beacon and each currently supported hero-skill command still reaches Runtime and preserves existing behavior in live and tactical-pause entry paths.
- Exercise the production-facing submission boundary rather than only calling the validator or inspecting source strings.

## Non-Goals

- Do not implement Regroup, Retreat, Move, Attack, Hold, Protect, reinforcement, mind control, allied shared control, or new command types.
- Do not redesign command queues, supersession timing, actor/group commander state, targeting UX, skill effects, cooldowns, costs, AI, navigation, or reports.
- Do not perform broader Bridge Draft/Snapshot cutover, main-map Strategic Management cutover, consequence-summary expansion, or save/persistence changes.
- Do not consolidate runners, split roots, delete compatibility code, remove AutoBattle, merge resources, or perform general code slimming.
- Do not modify `scenes/world/preview/`, the standalone Strategic Region Preview chain/tests, territory masks/artifacts, workbench, city-map authoring, hero-corps manual QA, or other active-task files.
- Do not stage, commit, switch branches, clean, restore, or normalize the shared dirty worktree.

## Constraints And Risks

- Command authorization is a high-correctness cross-layer boundary. Use `executor-xhigh (gpt-5.6-sol/xhigh)`.
- Work on `main` and preserve the uncommitted A1/A2 changes and all unrelated user changes. Record scoped hashes before editing.
- Never read or modify `scenes/world/preview/`; the user is actively testing it. Do not start, stop, or interact with the user's Godot process.
- Do not read other active task bodies or touch their scoped files. Exclude territory/workbench and preview artifacts from checks and conclusions.
- Use existing `CommandRequest`, snapshot, validator, Runtime controller, and result contracts. Do not add a generic middleware/event-bus framework or duplicate permission model.
- Keep Application permission and Runtime battle-context validation separate and explicit. Do not hide failure behind silent no-op or compatibility fallback.
- Tests observe public behavior and state/event invariants. Source-shape assertions alone are insufficient.
- Run focused validation first using `-maxcpucount:2 -v:minimal`. Do not launch Godot; existing C# behavior seams are expected to be sufficient.
- Add concise nearby comments only where ownership, defense-in-depth, channel authority, or failure semantics are otherwise non-obvious.

## Acceptance Criteria

- [x] Accepted command authority explicitly records the production Application boundary and Runtime defense-in-depth contract before implementation changes.
- [x] Production destination-beacon and hero-skill submissions no longer call Runtime directly as their authorization boundary.
- [x] A valid player-owned deployed group/source/skill/channel command passes Application validation and retains existing Runtime behavior.
- [x] Enemy-controlled group or source actor commands are rejected by both the production Application path and Runtime defense.
- [x] Forged actor/group, wrong battle, missing/unavailable group, unknown skill, and snapshot-authored channel mismatch fail explicitly.
- [x] Application rejection does not call Runtime, emit accepted Runtime events, consume skill resources/cooldowns, or mutate pending/tactical command state.
- [x] Runtime rejection of a forged/stale request leaves Runtime state and event stream unchanged except for accepted diagnostic semantics.
- [x] Destination-beacon multi-select atomic reachability, tactical-pause command behavior, skill supersession, and all supported player skill effects remain green.
- [x] A1 settlement/rollback/persistence/replay and A2 cardinality/casualty suites remain green.
- [x] Focused affected runners, `dotnet build rpg.sln -maxcpucount:2 -v:minimal`, and scoped whitespace checks pass, or unrelated failures are recorded precisely.
- [x] `scenes/world/preview/`, preview/territory/workbench chains, other active tasks, branch, and index remain untouched.
- [x] Executor hands off at `Awaiting Verification`; a fresh independent verifier checks every criterion before completion/archive.

## Skills Used

- `input-handling`: Presentation input emits intent after UI handling and remains separate from command authority.
- `csharp-godot`: typed C# boundary conventions and Godot object/lifecycle safety.
- `godot-testing`: RED-GREEN-REFACTOR and public behavior/state-event verification.
  - Task-relevant references read in full: `references/tdd-workflow.md`, `references/running-tests.md`, and `references/testing-patterns.md`.
- `godot-code-review`: final cross-layer ownership, lifecycle, failure, and hot-path review.

## Current Progress Snapshot

### Completed

- User authorized continuous batch-by-batch remediation and explicitly excluded `scenes/world/preview/` during active testing.
- A1 and A2 completed independent verification and archive.
- Read current gameplay command detail, accepted command architecture, relevant Godot skills, and located the validator, direct Presentation submissions, Runtime resolvers, and the existing enemy-command positive regression.
- Recorded the A3 scope, non-goals, authority impact, failure behavior, and acceptance contract.
- Execution started on `main` at HEAD `212c69161fabe375d524adf72c13cfe9cf290d79`; impact is Medium because the confirmed behavior crosses Presentation, Application, and Runtime and requires one focused architecture clarification.
- Scoped baseline recorded before edits. The index tree was `e88edcc7d3485c1210f24c0d195c9fa314940ceb`; only this work item was already untracked within the exact A3 scope.
- Synchronized only `system-design/battle-command-architecture.md` before implementation: production live/pause commands now explicitly route through one Application submission boundary; snapshot permission, typed no-side-effect rejection, and Runtime defense-in-depth are explicit.
- Added public-behavior authorization cases to the existing `TargetBattleArchitectureRegression` runner and captured the expected RED before production implementation.
- Implemented the focused Application submission service, extended snapshot permission validation, cut over both production Presentation routes, and added Runtime defense for battle/group/source/player/channel/skill identity.
- GREEN behavior now covers Application no-side-effect rejection, valid live and tactical-pause hero skill submission, valid destination beacon submission, enemy/spoofed/stale/unavailable/unknown-skill/wrong-channel rejection at Application and Runtime, and the A1/A2 opposing-channel effect cases.

### Remaining

- Fresh independent verification against every acceptance criterion. The executor must not mark this task `Completed` or archive it.

## Pause Or Blocker

- Current blocker: None. Scoped execution and executor validation are complete.
- Resume condition: Satisfied on 2026-07-12.
- Resume entry: A fresh verifier should use the exact A3 paths and final hashes below, rerun the focused commands without entering excluded preview/territory/workbench chains, and independently decide every acceptance criterion.
- Stop condition: Any new player-control, faction-sharing, command-channel, or command-timing rule requires `Needs Discussion`.

## Latest Verification

- `dotnet build tests/TargetBattleArchitectureRegression/TargetBattleArchitectureRegression.csproj -maxcpucount:2 -v:minimal`: 0 errors. A clean rebuild surfaced 25 pre-existing warnings, all outside exact A3 paths; the final solution build was clean.
- TargetBattle runner: 368 PASS / 1 unrelated FAIL. All A3 authorization, valid live/pause skill, destination-beacon atomicity, opposing-channel casualty, A1 settlement/report/result-adapter, A2 commander-cardinality/visible-strength/survival cases passed. The only failure remains the pre-existing oversized-file guard for `WorldSiteDeploymentCacheRegressionCases.PresentationResourceAuthoring.cs` and `StrategicManagementRules.cs`, both outside A3 and not read or changed.
- `dotnet build tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegression.csproj -maxcpucount:2 -v:minimal`: 0 warnings / 0 errors. Exact A3 method-level execution passed `WorldSiteBattleRuntimeHeroSkillSubmitsHeroCastCommand`, `BattleRuntimeRightClickSubmitsDestinationBeaconCommand`, and `BattleRuntimePauseTargetClickSubmitsIntentWithoutAdvancingRuntime`.
- Exact A1 method-level execution passed settlement ordering, activation-failure rollback, strategic-state persistence before feedback, presentation-only cleanup, legacy-applier exclusion, and stale-handoff replay cleanup: `StrategicBattlePostResultShowsSettlementModalBeforeRouting`, `BattleLauncherCancelsHandoffAndRestoresSiteOnActivationFailure`, `StrategicBattleResultPersistsStrategicStateBeforeFeedback`, `StrategicBattleResultKeepsPresentationCleanupOnly`, `StrategicBattleResultSkipsLegacyWorldApplier`, and `StrategicBattleResultClearsStaleLegacyHandoff`. The first temporary harness attempt omitted `RPG_GAMELOG_DIR` and hit Godot native logging initialization before the rollback assertion; after matching the formal runner's test-log isolation, all six passed. This was harness setup failure, not a product/test assertion failure.
- `dotnet build rpg.sln -maxcpucount:2 -v:minimal`: 0 warnings / 0 errors. The solution contains no excluded preview/territory/workbench project.
- Exact A3 `git diff --check`: pass. Branch/HEAD/index tree remain `main` / `212c69161fabe375d524adf72c13cfe9cf290d79` / `e88edcc7d3485c1210f24c0d195c9fa314940ceb`.
- Exact source inspection finds one Application submission call in each production Presentation route and no direct `RuntimeController.SubmitCommand` call in either route.
- Full `godot-code-review` checklist result: no remaining critical or improvement findings in the exact A3 diff. The test-only reflection seam remains because it injects an opposing Runtime effect fact only after the public boundary rejection is proved; assertions remain on public tick behavior, and exposing a production bypass would weaken the accepted boundary. A rejected refactor experiment was fully reverted after the existing shared-skill-definition regression proved hero-owned grant precedence is intentional.

## Execution Record

### 2026-07-12 - Task Initialization

- Main discussion context created the self-contained A3 task under the user's standing remediation authorization.
- No production, authority, resource, test, config, branch, index, preview, or external state changed during initialization.

### 2026-07-12 - Executor Baseline

- Branch/HEAD: `main` / `212c69161fabe375d524adf72c13cfe9cf290d79`.
- Index tree: `e88edcc7d3485c1210f24c0d195c9fa314940ceb`.
- Scoped tracked files and pre-edit SHA256:
  - `system-design/battle-command-architecture.md`: `7bd9f9a3a028a611bcc5cecccee662c107565c774ed85e11b3f00f889c4e8443`.
  - `src/Application/Battle/Commands/BattleCommandApplicationValidator.cs`: `e2864a75876e7f5257a5e4612d85d5d077f0ef94c10b8ec662876eb666ef74cb`.
  - `src/Runtime/Battle/BattleRuntimeSessionController.cs`: `5c984a9bbd9a78e531973ef62633e1fe499f45187f486d184a9412ae14be6e87`.
  - `src/Presentation/World/Sites/WorldSiteRoot.BattleRuntimeDestinationBeacon.cs`: `6314d50da406d161fcfcf55a144db32594e3329b078d87f885e928f26080802f`.
  - `src/Presentation/World/Sites/WorldSiteRoot.BattleRuntimeCommandHud.cs`: `2fed28079d7ec2c5fa22528be66e907f4185524b4bc9d8edc1e3630c1a1279fa`.
  - `tests/TargetBattleArchitectureRegression/Program.cs`: `b3d5f2ca11edb2e9e6b1c6fcf7fc67d28bf9a46694ae9d7fce88c548af324aa9`.
  - `tests/TargetBattleArchitectureRegression/TargetBattleEffectCommitBufferRegressionCases.cs`: `a92ff0a56a5becd3232e160d07d153d22945f3c1bd0f58f26293efe8bf513a7b`.
  - `tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegressionCases.HeroCorps.cs`: `b0db3ade00b1e2d179abe1ba73c19792c4b8c3eb92479f5acc01d812714962d5`.
  - `tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegressionCases.BattleRuntimeHud.cs`: `c816fcdf22c41da2ad22baecf0b11a4323c5832d500e49ae9d1bf144292e8bd1`.
  - `tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegressionCases.HeroSkillRuntime.cs`: `c09f90588dcedc714b9aa1e780445bec77954e0191bde7266c147f6999b6a5f7`.
- Scoped pre-existing untracked file: this work item, SHA256 `2a63660eae3682227261a6e26246f17b8cbe90435a6197d70a1df04aae4841f6`.
- Planned new untracked files were absent: `src/Application/Battle/Commands/BattleCommandSubmissionService.cs` and `tests/TargetBattleArchitectureRegression/TargetBattleCommandAuthorizationRegressionCases.cs`.
- No forbidden preview/territory/workbench paths, other active-task bodies, branch, index, or Godot process were inspected or changed.

### 2026-07-12 - Authority Synchronization

- Impact classification: Medium.
- Updated only `system-design/battle-command-architecture.md` before implementation. No gameplay authority or other system authority changed.

### 2026-07-12 - RED

- Command: `dotnet build tests/TargetBattleArchitectureRegression/TargetBattleArchitectureRegression.csproj -maxcpucount:2 -v:minimal`.
- Expected failure: 10 `CS0246` errors in `TargetBattleCommandAuthorizationRegressionCases.cs` because `BattleCommandSubmissionService` and `BattleCommandSubmissionResult` did not exist. The referenced `rpg` project compiled first, so the RED precisely identified the missing Application submission boundary.
- A prior `dotnet msbuild ... -target:Run` reused the old executable and therefore did not exercise the new cases; it separately reported pre-existing oversized-file guard failures for `tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegressionCases.PresentationResourceAuthoring.cs` and `src/Application/StrategicManagement/StrategicManagementRules.cs`. These files are outside A3 and were not read or changed.

### 2026-07-12 - GREEN Implementation Milestone

- Added `BattleCommandSubmissionService` and typed `BattleCommandSubmissionResult`; Application rejection returns no Runtime events and does not invoke Runtime.
- Extended `BattleCommandApplicationValidator` to use active snapshot battle, faction, deployed commander group, source actor, skill ownership, and snapshot-authored command channel facts.
- `BattleRuntimeSessionController.SubmitCommand` now independently rejects forged or stale player-command identity before resolver mutation while emitting one Runtime rejection diagnostic event.
- Production destination-beacon and hero-skill submissions now pass snapshot, player faction, unchanged request, and Runtime controller through the Application service. Tactical pause and existing Presentation feedback remain unchanged.
- Focused Target build passed with 0 errors. The runner passed all new authorization cases and affected existing command/skill/effect cases; only the unrelated oversized-file guard remains red.
- WorldSite runner build passed with 0 errors. All three changed production-route guards passed; only the unrelated missing legacy path `design-proposals/active/README.md` remains red.

### 2026-07-12 - Fixed Executor Quota Block

- The first fixed `gpt-5.6-sol/xhigh` process reached the outer 20-minute command limit after safely recording the GREEN milestone; the worktree remained intact.
- Resuming the same session confirmed `gpt-5.6-sol`, `xhigh`, `approval: never`, and `danger-full-access`, but the Codex service rejected further execution because the account usage limit had been reached. The reported retry time was 19:18 local time.
- A main-context read-only inspection found no scoped `git diff --check` errors and confirmed both production routes now call `BattleCommandSubmissionService`; no implementation, test, build, branch, index, preview, territory, workbench, or other active-task mutation was performed after the fixed executor stopped.
- Read-only review noted one remaining REFACTOR decision for the fixed executor: opposing-effect tests currently use a test-only reflection call into the internal skill resolver after first proving the public Runtime boundary rejects the enemy request. Production code does not use this seam.

### 2026-07-12 - Executor Resume

- The user confirmed the quota recovery condition and resumed the exact A3 task with the same sole-writing executor constraint.
- Reconfirmed `main`, HEAD `212c69161fabe375d524adf72c13cfe9cf290d79`, and index tree `e88edcc7d3485c1210f24c0d195c9fa314940ceb` without enumerating excluded paths.
- Reconciled the exact A3 path hashes with the recorded GREEN milestone. No authority conflict was found; final affected/A1/A2 validation and scoped review are in progress.

### 2026-07-12 - Final Executor Validation And Review

- Completed TargetBattle, exact WorldSite production-route, A1/A2 affected, solution-build, scoped whitespace, branch/HEAD/index, route-source, and final hash validation.
- Explicitly reran the six named A1 settlement/rollback/persistence/replay guards in isolation; all passed after correcting the temporary harness's test-log environment.
- Applied every `godot-code-review` checklist section to the exact diff. No scoped production or test fix remains. A candidate stricter group check was tested, correctly rejected by the shared hero-owned skill-definition regression, and fully reverted to the prior recorded hashes.
- Explicit file inspection, diff, hash, and method-level WorldSite validation stayed on the exact A3 paths and three named production guards; no excluded preview/territory/workbench path or other active-task body was targeted, and no user process was inspected or changed. Temporary focused reflection helpers were created only under the system temp directory and removed completely.
- `dotnet build-server shutdown` successfully stopped only the idle MSBuild and VB/C# compiler servers after validation.

### Final Scoped SHA256

- `system-design/battle-command-architecture.md`: `a10c5afca3e9720918c4ac6b8db1f9a712a347a85423a3ec83d2f13063e661dd`.
- `src/Application/Battle/Commands/BattleCommandApplicationValidator.cs`: `b8efd254016b1f4731d988931d263f9af3c3eebbf1cad13baf9b02264737433a`.
- `src/Application/Battle/Commands/BattleCommandSubmissionService.cs`: `49856a6c4029a6fd54be48ce7ef597dddbe70126b17f03030d01a150f0b140cb`.
- `src/Runtime/Battle/BattleRuntimeSessionController.cs`: `28a279e414a578ccd885eb76439dd53c79bc98313256760d5029335d35cae1b7`.
- `src/Presentation/World/Sites/WorldSiteRoot.BattleRuntimeDestinationBeacon.cs`: `d030d10756c54faf3f2db607ef993d1f95762bf702ffc024be2f0afd35ebeb2d`.
- `src/Presentation/World/Sites/WorldSiteRoot.BattleRuntimeCommandHud.cs`: `cf0dabb6c49e55b749f8aa067aeb1ee6fe8c9ca0a4b9ad9a1b52d5032a475696`.
- `tests/TargetBattleArchitectureRegression/Program.cs`: `c3b046d9352c9025a434ba983621fb0e8e796fd18589d0ced239e259b70bfd99`.
- `tests/TargetBattleArchitectureRegression/TargetBattleEffectCommitBufferRegressionCases.cs`: `7a1ee6f2762c81e862cc58c6cb0642fe8c746b03db8884ce8fdd449dfc5d01ea`.
- `tests/TargetBattleArchitectureRegression/TargetBattleCommandAuthorizationRegressionCases.cs`: `9f15ec93037afb4583b231f7fbef1a841d299e1222f800e452a161fb37ed644b`.
- `tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegressionCases.HeroCorps.cs`: `b4c495811c54a48cb348ed930be1c44bd3f9944a56207ce483ab12aee15b702d`.
- `tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegressionCases.BattleRuntimeHud.cs`: `3f55102bff58f8a3709503ea8a67ca4e6dcc807397e9b87f8ce8edc5131bc68e`.
- `tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegressionCases.HeroSkillRuntime.cs`: `1667d8c8798877af07015bf3d33f20d0cab3cd4f64bb8ffcb186bfc20e76cd4e`.

## Verification Handoff

Ready for a fresh independent verifier. Verifier is unassigned.

Verify only the exact A3 paths and hashes above. Rebuild and run TargetBattle at low concurrency; treat only the two named oversized-file guard findings as recorded unrelated failures. Build WorldSite, then use a temporary `net8.0` reflection harness outside the repository with `RPG_GAMELOG_DIR` set to a temp path to invoke only the three named A3 production-route methods and six named A1 methods above; remove the harness afterward and do not run or inspect excluded preview/territory/workbench cases. Recheck both Presentation files for Application-only submission, Application and Runtime rejection no-side-effect behavior, valid live/pause skill and beacon behavior, A1/A2 regressions, solution build, exact-path `git diff --check`, and unchanged branch/HEAD/index tree. Do not complete or archive unless every criterion is independently satisfied.

## Independent Verification - 2026-07-12

- Verifier configuration: fresh independent root context; the Codex surface did not expose a selectable model or reasoning-strength value, so none is claimed. Approval mode was `never`; filesystem access was unrestricted. Applied `godot-testing` and `godot-code-review` in full.
- Authority and scope: read the required governance, gameplay command detail, system-design route, accepted battle-command architecture, and the full exact task. No authority conflict was found. Inspected only the 12 exact A3 paths and task-named methods.
- Baseline and final scope proof: branch `main`, HEAD `212c69161fabe375d524adf72c13cfe9cf290d79`, index tree `e88edcc7d3485c1210f24c0d195c9fa314940ceb`. All remained unchanged before and after verification. Both production Presentation routes contained exactly one Application submission and zero direct Runtime submissions.
- Reconfirmed SHA256 set: authority `a10c5afca3e9720918c4ac6b8db1f9a712a347a85423a3ec83d2f13063e661dd`; Application validator `b8efd254016b1f4731d988931d263f9af3c3eebbf1cad13baf9b02264737433a`; submission service `49856a6c4029a6fd54be48ce7ef597dddbe70126b17f03030d01a150f0b140cb`; Runtime controller `28a279e414a578ccd885eb76439dd53c79bc98313256760d5029335d35cae1b7`; destination Presentation `d030d10756c54faf3f2db607ef993d1f95762bf702ffc024be2f0afd35ebeb2d`; skill Presentation `cf0dabb6c49e55b749f8aa067aeb1ee6fe8c9ca0a4b9ad9a1b52d5032a475696`; Target Program `c3b046d9352c9025a434ba983621fb0e8e796fd18589d0ced239e259b70bfd99`; effect cases `7a1ee6f2762c81e862cc58c6cb0642fe8c746b03db8884ce8fdd449dfc5d01ea`; authorization cases `9f15ec93037afb4583b231f7fbef1a841d299e1222f800e452a161fb37ed644b`; WorldSite HeroCorps `b4c495811c54a48cb348ed930be1c44bd3f9944a56207ce483ab12aee15b702d`; BattleRuntimeHud `3f55102bff58f8a3709503ea8a67ca4e6dcc807397e9b87f8ce8edc5131bc68e`; HeroSkillRuntime `1667d8c8798877af07015bf3d33f20d0cab3cd4f64bb8ffcb186bfc20e76cd4e`.
- `dotnet build tests/TargetBattleArchitectureRegression/TargetBattleArchitectureRegression.csproj -maxcpucount:2 -v:minimal`: 0 warnings / 0 errors.
- TargetBattle runner with isolated `RPG_GAMELOG_DIR`: 368 PASS / 1 unrelated FAIL. Every A3 authorization case and the affected A1/A2 behavior passed. The sole failure was the recorded oversized-file guard for `WorldSiteDeploymentCacheRegressionCases.PresentationResourceAuthoring.cs:1634` and `StrategicManagementRules.cs:1088`; neither file was read or changed.
- `dotnet build tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegression.csproj -maxcpucount:2 -v:minimal`: 0 warnings / 0 errors. An external temporary `net8.0` reflection harness then passed the three named A3 production-route methods and six named A1 methods, 9 PASS / 0 FAIL, with an isolated `RPG_GAMELOG_DIR`. The first harness attempt had 8 project-root lookup failures because its external BaseDirectory was not set; after setting BaseDirectory to the built test output, the same nine methods passed. This was harness configuration, not a product or test assertion failure. The harness was deleted completely.
- `dotnet build rpg.sln -maxcpucount:2 -v:minimal`: 0 warnings / 0 errors.
- Exact tracked-path `git diff --check`: pass. Both new A3 files had zero whitespace errors under `git diff --no-index --check`; its exit code `1` represented expected file differences from `NUL`. Three existing exact-path CRLF normalization warnings were non-failing and no files were normalized.
- The previously recorded deleted legacy README guard was not repaired or rerun because the handoff required only the nine named WorldSite methods and prohibited running unrelated chains.
- Full `godot-code-review` result for the exact A3 diff: Critical `None`; Improvements `None`. The command path keeps Presentation, Application permission, and Runtime defense separate; rejection precedes resolver mutation; command-time LINQ is not a per-frame hot path; no new node, signal, input, resource, or lifecycle anti-pattern was introduced. `godot-testing` verification confirmed public authorization behavior and state/event invariants rather than relying only on source shape.
- Exclusions: did not enter or enumerate `scenes/world/preview/`; did not inspect or run standalone preview, territory, workbench, or other active-task chains; did not inspect, launch, or stop Godot or any user process; did not stage, commit, switch, restore, clean, or use global status/whole-repository scans. Only the external temporary harness was created and deleted. No production or test code was changed by verification.

## Final Result

PASS. Every A3 acceptance criterion independently passed. The battle-command authorization cutover is complete and may be archived. Remaining scoped risks: None. Known unrelated guards remain the two out-of-scope oversized files and the deleted legacy README guard; neither was repaired or brought into scope.
