# Strategic Battle Result Envelope Authority

- Status: Completed
- Executor: Codex fixed executor
- Verifier: Codex primary independent context
- Created: 2026-07-12
- Updated: 2026-07-12

## Objective

Remediate P2-14 by replacing the Strategic Battle Active Context's parallel direct result fields and `FlowResult` mirror with one authoritative immutable result envelope for Runtime result, settlement plan, and battle report facts.

## Confirmed Discussion Result

The user authorized continuous remediation and requested clean checkpoint commits between batches. The prior remediation set through P1-07 is committed at main HEAD `efc1d28255975ddf29f6c9a5b62cfb7061e396fb`. This batch removes direct-or-FlowResult fallback behavior: consumers read one typed envelope, publication happens once after complete validation, and missing or conflicting legacy-shaped values fail explicitly rather than selecting whichever side is populated.

## Authority Impact

Impact classification: **Medium**. No gameplay rule changes. `system-design/strategic-battle-bridge-architecture.md` already defines Bridge Active Context as the single result carrier and forbids parallel result authority; `system-design/battle-result-settlement-architecture.md` requires Runtime result, settlement, and report facts to remain consistent. No authority edit is required unless implementation discovers a contradiction.

## Execution Scope

- Capture exact pre-edit hashes for every touched path from the clean checkpoint baseline.
- Add public behavior RED tests proving one result envelope is published once and direct/mirrored divergence cannot be silently consumed.
- Introduce or reuse one typed immutable envelope containing the accepted Runtime result, settlement plan, and report.
- Publish the envelope only after all three facts and their battle/snapshot identities validate.
- Make Active Context and strategic summary/result consumers read the envelope only.
- Remove direct-or-`FlowResult` fallback resolution and duplicate write paths from the Strategic Management-backed battle flow.
- Keep legacy non-strategic flow results only at an explicit adapter boundary where still required; they must not be Active Context authority.
- Preserve settlement transaction/idempotency, participant cardinality, command authorization, map authority, Draft snapshot authority, and commander-state authority.
- Run focused bridge/result regressions, safe affected project builds, final low-concurrency solution build, exact-path whitespace checks, and complete code review.

## Non-Goals

- No session/context CAS implementation from P2-03.
- No expedition/world-army carrier or participant-reference mirror retirement beyond compatibility necessary for this envelope.
- No result-summary feature expansion from P2-02.
- No regroup/retreat product commands, UI redesign, scene/resource changes, content-contract work, or general code slimming.

## Constraints And Exclusions

- Continue to ignore `scenes/world/preview/` and standalone Strategic Region Preview work while this code batch executes.
- Do not alter unrelated territory/workbench behavior or other active task bodies.
- Do not launch, stop, or inspect Godot/user processes.
- Work on `main`; do not create or switch branches.
- Use `apply_patch` for edits, TDD RED-GREEN-REFACTOR, and low-concurrency minimal `dotnet` commands.
- No staging or commit inside executor; the primary context owns checkpoint commits after independent verification.
- Skills required: `csharp-godot`, `godot-testing`, and `godot-code-review`.

## Acceptance Criteria

1. Strategic Battle Active Context exposes one authoritative typed result envelope; Runtime result, settlement plan, and report are not separately writable competing facts.
2. Envelope publication validates matching battle/session/snapshot identities and commits once; missing, partial, duplicate, or mismatched publication fails before context mutation.
3. Strategic result-summary, settlement/writeback, presentation feedback, and cleanup paths read the same envelope without direct-or-mirror fallback.
4. Deliberately divergent legacy `FlowResult` or direct fields cannot change or substitute the accepted envelope; migration incompatibility is explicit and diagnosable.
5. A1-A4 and B1-B2 behavior remains passing in focused regressions.
6. Safe bridge/result/TargetBattle/StrategicManagement coverage passes; WorldSite runner remains build-only if it may enumerate ignored preview content.
7. `dotnet build rpg.sln -maxcpucount:2 -v:minimal` succeeds.
8. Exact-path whitespace checks succeed; branch remains `main`, HEAD remains `efc1d28255975ddf29f6c9a5b62cfb7061e396fb`, and index tree remains `745fb5c3b1066ca3d27fc56f8ce9ca47e1f3cd9d` during executor work.
9. The exact diff passes complete `godot-code-review` with no scoped Critical or Improvement finding unresolved.

## Current Progress Snapshot

Completed:

- Prior remediation through P1-07 was independently verified and checkpointed.
- P2-14 evidence and accepted Bridge/result authority were reviewed.
- Clean execution baseline recorded.
- Execution bootstrap completed: required authority and `csharp-godot`, `godot-testing`, and `godot-code-review` skills were read; no authority conflict was found.
- RED added public Strategic Management behavior coverage for single publication, duplicate rejection, identity mismatch, no partial mutation, and legacy-mirror non-authority; the focused build failed only on the missing result-envelope API.
- GREEN introduced the single Active Context result envelope and switched strategic result publication and consumers to it; all 106 Strategic Management regressions pass.
- REFACTOR removed the scoped oversized-file regression, eliminated new nullability warnings, and completed affected builds, exact-path hygiene, baseline integrity, and the full Godot C# review.
- Independent verification confirmed the production method now has the authoritative single-context signature, but found four stale `ExtractMethodBody` selectors in `WorldSiteDeploymentCacheRegressionCases.StrategicArmyCommand.cs` that still name the retired two-parameter signature.
- All four stale selectors now name the authoritative single-context signature; focused static checks, the build-only WorldSite project, solution build, diff hygiene, and scoped code review passed without production changes.

Remaining:

- None.

## Pause, Blocker, And Resume

- Pause/blocker: None; task completed.
- Resume condition: None.
- Resume entry: None.
- Latest verification: Independent re-verification passed all acceptance criteria. Strategic Management completed 106/106 cases; the WorldSite regression project and solution both built with 0 warnings/errors; stale selector count is zero and the authoritative selector count is four; diff hygiene and complete scoped code review passed.

## Execution Record

- 2026-07-12: Fixed executor started P2-14 on `main`. Verified HEAD `efc1d28255975ddf29f6c9a5b62cfb7061e396fb` and index tree `745fb5c3b1066ca3d27fc56f8ce9ca47e1f3cd9d`; no staged paths were present. Pre-edit hash for this task document: `2206cc83d5206766d4347a6c38a53439327469cc`.
- 2026-07-12: Captured pre-edit hashes for implementation/test paths: models `9e6bc957e229c530aacf27a6a77968420efe698d`, bridge service `860544fd324a8881e1d5bd8db7dfe5eb26d1627d`, draft compiler `c33d74d081e779d8e9e08677403f9b35e15ec4de`, Runtime adapter `89a56cbedb46bc8f539618bcefcf43197b002e5f`, presentation result path `1a75f7fbf76e65a224e8a97d54dbdc8a17cc011d`, Strategic runner `78f1d363908d64c4728f5f46221393bbbc18169c`, result cases `874f6c84ede5f98cbf427f50af099c3d9ad67fd9`, support `b35cc1b1b6b8379aa346b9d8e7af43204fafdfde`, cardinality `be7746c0e9d019f6f42725989c340ebd2b53dcce`, WorldSite architecture cases `382e1548bb1c04ee5c01d20043b9f14cd745fd65`.
- 2026-07-12: Focused RED build failed with 11 expected missing-envelope API errors. GREEN build succeeded and all 106 Strategic Management regressions passed; only two test nullability warnings introduced by the RED fixture remained and were then removed.
- 2026-07-12: Final Strategic Management build passed with 0 warnings/errors and all 106 cases passed, covering retained A1-A4/B1-B2 behavior plus P2-14 publication/failure cases. TargetBattle built successfully and every behavior case passed; its sole runner failure is the pre-existing oversized guard for `WorldSiteDeploymentCacheRegressionCases.PresentationResourceAuthoring.cs:1634` and `StrategicManagementRules.cs:1122`, neither read nor changed by P2-14. WorldSite built successfully and was not run because it may enumerate excluded preview content. BattleHitFeedback result behavior passed; its two unrelated failures were the excluded preview workbench and a pre-existing command-spotlight expectation.
- 2026-07-12: `dotnet build rpg.sln -maxcpucount:2 -v:minimal` passed with 0 warnings/errors. Exact tracked-path `git diff --check` passed; the untracked task no-index check returned difference-only exit `1` with no whitespace diagnostic; explicit trailing-whitespace count was zero. Branch/HEAD/index remained `main` / `efc1d28255975ddf29f6c9a5b62cfb7061e396fb` / `745fb5c3b1066ca3d27fc56f8ce9ca47e1f3cd9d`, with no staged paths.
- 2026-07-12: Complete `godot-code-review` and `csharp-godot` checklist review found no scoped Critical or Improvement item. Node/scene, input, signal, resource, and hot-path sections are unaffected; the result lock is confined to one-time Application publication; validation and diagnostics precede mutation; strategic summary, settlement, feedback, and cleanup consume the envelope path. Skills used: `csharp-godot`, `godot-testing`, and `godot-code-review`.
- 2026-07-12: Independent verification returned P2-14 to `In Progress` after confirming four stale `ExtractMethodBody` selectors in `tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegressionCases.StrategicArmyCommand.cs`; production code and authority remain consistent, so remediation is limited to test signature maintenance.
- 2026-07-12: Replaced exactly four retired two-parameter `ExtractMethodBody` selector strings with `ApplyStrategicBattleResultToWorld(StrategicBattleActiveContext context)`. Static checks found zero old selectors and exactly four authoritative selectors. The WorldSite regression project was built but not run; it succeeded with 17 pre-existing nullable warnings from untouched test files and no errors. `dotnet build rpg.sln -maxcpucount:2 -v:minimal` succeeded with 0 warnings/errors.
- 2026-07-12: Scoped `git diff --check`, explicit task trailing-whitespace inspection, exact four-line diff review, branch/HEAD/staging checks, and complete scoped `godot-code-review` passed. Branch and HEAD remain `main` / `efc1d28255975ddf29f6c9a5b62cfb7061e396fb`; no staged paths exist. No production or preview business content was modified. Skills used for remediation and review: `csharp-godot`, `godot-testing`, and `godot-code-review`.
- 2026-07-12: Independent re-verification accepted P2-14. `StrategicManagementRegression` built with 0 warnings/errors and passed 106/106 cases; `WorldSiteDeploymentCacheRegression` was build-only and passed with 0 warnings/errors; `rpg.sln` built with 0 warnings/errors. Exact selector counts were old `0` / authoritative `4`; `git diff --check` and the full scoped `godot-code-review` passed with no unresolved Critical or Improvement finding. Verifier used `csharp-godot`, `godot-testing`, and `godot-code-review`.

Final exact git object hashes:

- Models `13d61a77804a5f2fb4f5fa962047f8f9916bf7d6`; bridge service `0d2325570ec98d3bc2c665b4aec4c5d693c06f47`; Draft compiler `b233b417fa3ae22841f15dc68ce6a567ab659b87`; Runtime adapter `6c91b175e66312e7c7224c074d40faeab6ce5a8b`; presentation result path `b0da7f0739cb450a1c7f1c2de873d550059e70ce`.
- Strategic runner `f6a62063da7281a2aba87286bff20a5e1dcd6bc3`; cardinality cases `0a7691c7ead300dfe25737df56d4b5dbacd47967`; result cases `03198f73cb4284ff9e53e32ebbbbd1609f9a4481`; support `f2841cc5a338c87c6d869864b2acd55237094fcc`; WorldSite architecture cases `4b05127f463f92fceeac98451aa024827ac3aca2`.
- WorldSite strategic-army command cases after verifier remediation `6c2c9fb48fdf0c7db298e667d44b766be3faccf2`.

## Final Result

P2-14 is independently verified and completed. Active Context publishes one validated result envelope exactly once; strategic consumers no longer use direct fields or `FlowResult` mirrors; invalid, incomplete, duplicate, mismatched, and ambiguous result facts fail before mutation. All returned test maintenance is resolved. Remaining risks: None. Follow-up work continues under separate confirmed remediation work items, beginning with P2-03.
