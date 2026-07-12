# Strategic World Control Authority Cutover

Status: Completed

Executor: Codex (`gpt-5.6-sol`, `high`)

Verifier: Codex (fresh independent context; requested `gpt-5.6-sol` / `high`, runtime configuration not exposed for verification)

Created: 2026-07-12

Updated: 2026-07-12

## Objective

Complete remediation batch A4 so main-map control visuals and target classification consume Strategic Management state and presentation view models as their sole authority.

## Confirmed Discussion Result

The user authorized all current-system review remediations in planned order and explicitly confirmed A4. Main-map color, control text, friendly/hostile classification, reinforce-versus-assault classification, attackability, command eligibility, and the next submitted command must agree with Strategic Management for the same stable strategic-location id. Legacy `WorldSiteState` may remain only for subordinate presentation facts such as garrison and damaged-site visuals; it must not own control or strategic decisions.

Map target decisions remain enforced by Strategic Management rules and commands. The implementation must use the stable strategic-location-to-map-site mapping already present in the project, without double writes, fallback authority, or a second strategic state owner.

## Authority Impact

Impact classification: Medium. This changes a cross-file runtime/presentation flow but does not change accepted gameplay, persistent-state shape, scene/resource taxonomy, or architecture.

No durable authority update is required. `system-design/strategic-management-system-architecture.md` already establishes Strategic Management as the sole owner of location control, derived availability, rules, commands, and strategic presentation view models, while legacy world/site state is presentation-only. Execution implements that accepted boundary.

## Scope

- Add public-behavior regression coverage for victory, defeat, and persisted reload using one stable location id.
- Make main-map control color and control text consume Strategic Management view data.
- Make map target classification, reinforce-versus-assault choice, attackability, command eligibility, and subsequent command selection consume Strategic Management rules/view data.
- Preserve the existing stable strategic-location-to-map-site mapping.
- Preserve strictly non-authoritative legacy presentation facts, navigation, selected-site behavior, garrison presentation, damaged-site visuals, world timeflow, expedition carriers, and existing Chinese player feedback.
- Run focused Strategic Management and affected WorldSite regressions, then the required low-concurrency solution build.
- Apply the installed `csharp-godot`, `godot-testing`, and `godot-code-review` skills.

## Non-Goals

- Battle Bridge Snapshot work.
- Command-state consolidation.
- UI redesign.
- Scene/resource taxonomy changes.
- Code slimming or unrelated remediation.
- Any work on territory artifacts, the world workbench, standalone Strategic Region Preview, or other active tasks.

## Constraints And Risks

- Never enter or enumerate `scenes/world/preview/` and never inspect or run standalone Strategic Region Preview chains or tests.
- Do not launch, stop, or inspect Godot or any user process.
- Preserve the dirty worktree and all A1/A2/A3/user changes; do not stage, commit, switch branches, restore, clean, run global `git status`, or scan the whole repository.
- Use `apply_patch` for every repository edit.
- Follow TDD: record exact baseline hashes, add public-behavior tests, capture a behaviorally meaningful RED, implement the minimum cutover, then GREEN and REFACTOR.
- Use low-concurrency minimal .NET builds and precisely classify unrelated pre-existing guards without repairing them.
- Final evidence must prove exact-path `diff --check` and that `main`, `HEAD`, and the index remain unchanged.
- If accepted authority contradicts this direction, set this task to `Needs Discussion` and stop.

## Acceptance Criteria

- Victory, defeat, and persisted reload are covered for one stable strategic location id.
- For each path, map color, control text, attackability, and next command agree with Strategic Management control and rule results.
- Friendly targets classify as reinforcement-compatible and hostile targets as assault-compatible only when Strategic Management rules allow the corresponding action.
- Legacy `WorldSiteState` no longer owns faction control, friendly/hostile classification, reinforce-versus-assault classification, attackability, or command eligibility.
- No double write, fallback, or second strategic authority is introduced.
- Navigation, selection, subordinate garrison/damage presentation, timeflow, expedition carriers, and Chinese feedback remain intact.
- Focused Strategic Management and affected WorldSite regressions pass, followed by `dotnet build rpg.sln -maxcpucount:2 -v:minimal`.
- Exact A4 diff passes whitespace checks and the full Godot C# review checklist with no unresolved scoped defect.

## Current Progress Snapshot

Completed:

- Confirmed the accepted gameplay and system authority and found no direction conflict.
- Fully read the A4 review finding and remediation ordering.
- Fully read and selected `csharp-godot`, `godot-testing`, and `godot-code-review` for execution and review.
- Recorded exact pre-A4 SHA-256 hashes for every candidate production/test path; `main` and `HEAD` both resolved to `212c69161fabe375d524adf72c13cfe9cf290d79` on branch `main`, and the exact-path cached diff was empty.
- Classified pre-existing exact-path worktree changes in `StrategicWorldRoot.BattleEntry.cs` (A3 battle-entry rollback work) and `tests/StrategicManagementRegression/Program.cs` (A1/A2/A3 regression registrations); these changes are preserved and excluded from A4 ownership except for narrow additive hunks.
- Added three public-behavior tests for victory, defeat, and persisted reload of `location_bonefield_outpost` and captured RED.

Remaining:

- None.

## Pause, Blocker, And Resume

Pause reason: None; independent verification completed.

Blocker: None.

Resume condition: Not applicable.

Resume entry: Not applicable; archived after independent PASS.

Latest verification: Independent PASS. All 102 StrategicManagement cases and all five filtered affected WorldSite cases passed; final `dotnet build rpg.sln -maxcpucount:2 -v:minimal` succeeded with 0 warnings and 0 errors; exact-path hashes, cached diff, whitespace checks, authority-residue checks, and the full Godot C# review checklist passed.

## Execution Record

- 2026-07-12: Task created as `In Progress` before code edits. Authority impact classified as Medium with no accepted-authority document change required.
- 2026-07-12: Baseline SHA-256: `StrategicManagementRules.cs` `AE0709782BD6DB9F22122B589917939898FEFCDCDE0B1D104D8ED3AEE047B255`; `StrategicManagementViewModels.cs` `B80412E8A2E24FFECD9856915E42A8F4969A6B9570420ABCBC50D51EACF695FD`; `StrategicManagementViewModelService.cs` `A2B212E0380AD387042BD0E5DBA1E9BA80C0CE9CA7FF753EBB32792CEDBEB588`; `StrategicWorldRoot.SiteEntry.cs` `2DD2EEED0A983F4036BEAFB421B2C0765F9AAEA37C57CFF9370F7A92F6BDF926`; `StrategicWorldRoot.MapDrawing.cs` `9BD944FCC91AED2D018FB7CEA6FB4824B227DD8ECF2F5D9F18BB7ECC2BA58DA7`; `StrategicWorldRoot.GeometryFormatting.cs` `68C4F756B1039AD2A21964C2830C4058D80F49F4FB75132F8E119568FF7DE689`; `StrategicWorldRoot.Expedition.cs` `E93FDB0492128620B6485732770CA208F852E55CFD672C09C6D1CEACBEF7980A`; `StrategicWorldRoot.ArmyCommands.cs` `16C56A45870C37061145CF6F3591A913E7E376FE9B930EF17CD24E7D37B98DC6`; `StrategicWorldRoot.BattleEntry.cs` `0115E1CC79B26EF91BE9C2E2DF4DCC2D1346E5859FAE0BBF9594226C882502FE`; regression `Program.cs` `A41F73C99AF7E252338B346CABCFEA30C19D69B36A3FDE90A57B61BDE494C362`.
- 2026-07-12: RED command/build succeeded, then the executable failed only the three new A4 cases with `strategic world map control presenter is missing`.
- 2026-07-12: GREEN introduced one Strategic Management target-rule query, map-focused location view fields, and a pure Strategic World map presenter. Main-map drawing, expedition targeting, selected-army targeting, blocked-state calculation, and battle confirmation control text now consume that projection. Legacy site state remains only for damage stripes, garrison/damage summary, and unrelated world-army carrier facts.
- 2026-07-12: REFACTOR removed the legacy site color and control-text helpers and removed legacy garrison-capacity command gating. Unmapped legacy markers remain visible/selectable with an explicit unavailable presentation and no inferred control or command authority.
- 2026-07-12: Focused WorldSite execution used `RPG_REGRESSION_FILTER` with exactly five A4-affected case names. The full 281-case executable was deliberately not run because its resource-recursion cases could enumerate the hard-excluded preview directory. No Strategic Region Preview chain or test was inspected or executed.
- 2026-07-12: Unrelated pre-existing guards were not repaired: WorldSite project compilation reported 17 nullable warnings in existing battle-skill/hero test files; an earlier full solution compilation reported 23 existing TargetBattle nullable warnings. The final incremental solution build reported 0 warnings/0 errors. The known obsolete-path WorldSite full-run guard was not exercised because the full executable was excluded.
- 2026-07-12: A fresh independent verifier read the accepted authority and applied `csharp-godot`, `godot-testing`, and `godot-code-review`. No authority conflict or scoped defect was found. Requested verifier configuration was `gpt-5.6-sol` / `high`; the runtime did not expose a verifiable model or reasoning setting, so no successful switch is claimed.

### Exact A4 Files And Final SHA-256

```text
src/Application/StrategicManagement/StrategicManagementRules.cs|25D47A4779EFC5BCB52C8F5E0C48F608699F55BFBBA1DC081ECFEDE3C9E39687
src/Application/StrategicManagement/StrategicManagementViewModels.cs|2E20AF31B2553D378CAFAF3AD261678B2BAC53A312E687D30DE5888F07953590
src/Application/StrategicManagement/StrategicManagementViewModelService.cs|423B32F0320043F3B1F1FD47E83A7D70606B2D2AE47DF4CC23F140CB4D6ECA00
src/Presentation/World/StrategicWorldMapSitePresenter.cs|B28FF4ADE08AD0A7B73CA166BC2F598005D8491D823814F5BB438E8C4562D15E
src/Presentation/World/StrategicWorldRoot.SiteEntry.cs|52B85761FBBADD32978EA313169AD09882C356E9EADC07584CE0D020D6FE987B
src/Presentation/World/StrategicWorldRoot.MapDrawing.cs|334DC1D55850A0369935C81F03122E7FC42F13034D1D50854A803DEB159BD1A2
src/Presentation/World/StrategicWorldRoot.GeometryFormatting.cs|5E45A4C87EE92DBED406BB22A3DE01241F1F8E72DCAF2DAD85A2AC08F7B09225
src/Presentation/World/StrategicWorldRoot.Expedition.cs|995190FE659B7DE1B474762D6D9421FC50A3F26002C654273466E50DE27B3C3B
src/Presentation/World/StrategicWorldRoot.ArmyCommands.cs|56C97EBAFA5FF34A1BBB47D6BCB5314005F3435FFFE31E26B8EF42A94E5E372F
src/Presentation/World/StrategicWorldRoot.BattleEntry.cs|DDAB49FBE6E434176A7EAEE34730448280E579AD1777BD278A5ED7D92AE8B660
tests/StrategicManagementRegression/Program.cs|C73F949F8ADD0B8C44B725CCA186E39D5039E82809D9389B43DAC9BD48EE6853
tests/StrategicManagementRegression/StrategicManagementRegressionCases.StrategicWorldControl.cs|4DFC2556B499A2BA0E96524D094F9830AA8B886A489614EC8964186080BB286E
tests/WorldSiteDeploymentCacheRegression/Program.cs|2409BE4CABACF95C10977486432BF92D653E3A27B8C5603A6F3DC3A8E44E956D
tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegressionCases.Common.cs|BBDEBC75E22FA804A92642A8CCAE9B7C977E533DB7DA9CA28C06F8ABDE571662
tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegressionCases.StrategicArmyCommand.cs|D9AFBA2EA60F352982F201F580247C3DBB63AAA2C295537EDFD30AD45BA4FC1F
```

Mixed files preserve unrelated pre-existing work: `StrategicWorldRoot.BattleEntry.cs` retains A3 rollback changes; both regression `Program.cs` files and `WorldSiteDeploymentCacheRegressionCases.StrategicArmyCommand.cs` retain earlier batch registrations/assertions. A4 changed only the narrow map-control and test-registration hunks described above.

### Verification Record

- RED: focused test project built with 0 warnings/0 errors; the executable passed every pre-existing case and failed exactly the three new A4 tests because the public map presenter was missing.
- GREEN: `dotnet build tests/StrategicManagementRegression/StrategicManagementRegression.csproj -maxcpucount:2 -v:minimal` succeeded; all 102 cases passed, including victory, defeat, and persisted reload for `location_bonefield_outpost`. Each path checks stable mapping, control color, control text, attackability, reinforce eligibility, next command, rejection of the opposite command, and acceptance of the projected command by Strategic Management rules.
- Affected WorldSite: the project built successfully; five filtered cases passed for command-state delegation, strategic retarget enforcement, Strategic Management map-control projection, removal of legacy garrison-capacity blocking, and selected-city Strategic Management authority.
- Final build: `dotnet build rpg.sln -maxcpucount:2 -v:minimal` succeeded with 0 warnings and 0 errors.
- Repository hygiene: exact-path tracked `git diff --check` and no-index checks for all new files passed. `main` and `HEAD` remained `212c69161fabe375d524adf72c13cfe9cf290d79`; the exact A4 cached diff remained empty, proving no index mutation.
- Exclusions honored: no Godot process or user process was launched, stopped, or inspected; no Godot/headless run occurred; no preview directory, Strategic Region Preview, territory artifact, world workbench, other active task, Git staging, branch operation, restore, clean, or global status/scan occurred.

### A4 Code Review Checklist

- Node/scene architecture: no scene/resource/tree change; existing partial root responsibility and stable map-site resolver are preserved.
- C# conventions: typed public contracts, PascalCase members, explicit null failure, and no Godot source-generator/signals changes.
- Performance: the map uses a focused location view instead of constructing the full dashboard; no new `GetNode`, resource load, process-loop allocation pattern, `Task.Run`, or async path was added.
- Input/communication: existing input, navigation, selection, command-service, and strategic-command boundaries remain intact; no circular signal or direct state-write path was added.
- Resource/lifecycle safety: no authored resource, dynamic node, disposal, await, or queue/free behavior changed.
- Authority/failure safety: commands reuse the same Strategic Management rule query projected to Presentation; no legacy owner/control read, legacy garrison eligibility guard, control double write, fallback strategic authority, or silent mapping substitution remains.
- Review conclusion: no Critical or Improvement finding remains in the exact A4 diff. The installed `csharp-godot`, `godot-testing`, and `godot-code-review` skills were applied.

### Independent Verification

- Verifier configuration: fresh independent Codex context; requested `gpt-5.6-sol` / `high`, with runtime model/reasoning configuration not exposed for verification.
- Strategic Management command: `dotnet build tests/StrategicManagementRegression/StrategicManagementRegression.csproj -maxcpucount:2 -v:minimal`, followed by `dotnet run --no-build --project tests/StrategicManagementRegression/StrategicManagementRegression.csproj`. Result: build 0 warnings/0 errors; 102/102 cases passed, including victory, defeat, and persisted reload for `location_bonefield_outpost`.
- WorldSite command: `dotnet build tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegression.csproj -maxcpucount:2 -v:minimal`, followed by the executable with `RPG_REGRESSION_FILTER` set to exactly the five recorded A4 case names. Result: build 0 warnings/0 errors; 5/5 filtered cases passed.
- Final command: `dotnet build rpg.sln -maxcpucount:2 -v:minimal`. Result: 0 warnings and 0 errors.
- Integrity: every final SHA-256 matched the recorded values above; `main` and `HEAD` remained `212c69161fabe375d524adf72c13cfe9cf290d79`; exact-path cached diff remained empty; tracked `git diff --check` and both new-file no-index whitespace checks produced no diagnostics.
- Review: stable location mapping, color/text projection, reinforce/assault choice, attackability, command eligibility, and submitted command all consume Strategic Management view/rule authority. No legacy control read, double write, fallback authority, second owner, garrison-capacity strategic gate, or unresolved Godot C# checklist finding was found. Remaining legacy reads are limited to allowed damage stripes, garrison/damage summary, and world-army carrier facts.
- Exclusions honored: no Godot or user process was inspected, launched, or stopped; no full WorldSite suite, standalone Strategic Region Preview chain/test, preview directory, territory artifact, world workbench, other active-task body, global status, whole-repository scan, staging, branch operation, restore, clean, production/test repair, or temporary harness was used.
- Unrelated failures: none encountered in the independent run. Executor-recorded earlier nullable warnings and the obsolete-path full-run guard remained excluded and were not repaired or re-exercised.

## Final Result

PASS. Every acceptance criterion independently passed. Main-map control visuals, text, classification, attackability, and next commands share Strategic Management authority across victory, defeat, and persisted reload. Remaining risks: None. Follow-up work: None within A4.
