# Strategic Battle Active Context CAS

- Status: Blocked
- Executor: Unassigned
- Verifier: Unassigned
- Created: 2026-07-12
- Updated: 2026-07-12

## Objective

Remediate P2-03 by making Strategic Battle Active Context publication, snapshot advancement, result publication, failure cleanup, and final consumption use one immutable context/session/snapshot/result revision token with compare-and-set semantics.

## Confirmed Discussion Result

The user authorized continuous remediation in the code-review risk order. P2-14 is completed at main HEAD `955605a754950cb3d7c8c861a07bd426c76e5a7f`. Existing identity checks and durable settlement replay remain, but callers still retain a mutable Active Context object and reconstruct expected identity from its current fields. This batch introduces a store-owned or context-owned monotonic revision lease captured at each accepted boundary. Stale callbacks must use the lease they originally received and cannot adopt later snapshot/result identity by rereading the same mutable object.

## Authority Impact

Impact classification: **Medium / high correctness risk**. No gameplay rule changes. `system-design/strategic-battle-bridge-architecture.md`, `system-design/battle-result-settlement-architecture.md`, and `system-design/scene-transition-router-architecture.md` already require identity-matched publication, cancellation, retry, and consumption. No authority edit is required unless implementation finds a contradiction.

## Execution Scope

- Capture exact baseline and touched-path hashes before edits.
- Add public behavior RED tests for stale publication/failure/result/consume interleavings and exact retry.
- Introduce one immutable Active Context CAS token containing context ID, session ID, snapshot ID, monotonic revision, and accepted result identity when present.
- Return/capture the token at successful begin/peek and require the expected token for mutation, clear, and consume operations.
- Advance revision atomically when the authoritative final Snapshot replaces the preparation seed and when the result envelope is first accepted.
- Prevent callers from reconstructing an expected token from later mutable Context fields; scene-transition failure must clear only with the token captured when that transition began.
- Make result publication reject stale snapshot/revision, duplicate callbacks, and mismatched result identity before partial mutation.
- Make settlement commit/consume compare the exact accepted result token; preserve durable exact replay after the transient context is consumed.
- Preserve P2-14 single result envelope, A1 transaction ordering, Draft/Snapshot authority, participant cardinality, map authority, commander authority, and rollback semantics.
- Add low-noise diagnostics naming expected/current identity and revision on CAS rejection without per-frame logging.
- Run focused Strategic Management, scene-transition/build-only WorldSite, TargetBattle safe coverage, low-concurrency solution build, exact-path whitespace checks, and complete review.

## Non-Goals

- No new save/resume format for live battle contexts.
- No expedition/world-army/participant mirror retirement.
- No P2-02 result-summary expansion or P1-08 regroup/retreat commands.
- No UI, scene, resource, preview, content, or general code-slimming work.
- No replacement of the already accepted durable settlement replay boundary.

## Constraints And Exclusions

- Ignore `scenes/world/preview/` and do not run a runner that enumerates it; WorldSite may be build-only.
- Do not launch, stop, or inspect Godot/user processes.
- Work on `main`; do not create/switch branches, stage, or commit inside the executor.
- Use `apply_patch`, RED-GREEN-REFACTOR, low-concurrency minimal builds, and one fixed write executor.
- Do not hold the store lock across arbitrary external persistence or publication callbacks if a safer reservation/commit protocol is required; preserve retryability and exact-once effects.
- Any required change to accepted authority or persistence semantics returns this task to `Needs Discussion` before implementation continues.
- Required skills: `csharp-godot`, `godot-testing`, and `godot-code-review`.

## Acceptance Criteria

1. Every accepted Active Context state has a monotonic immutable CAS token; snapshot and result advancement produce a new revision.
2. Begin is idempotent only for the exact accepted object/token and cannot overwrite a different active identity.
3. A stale scene-transition failure, cached scene callback, or cancellation token cannot clear or roll back a newer revision, even when it holds the same mutable Context reference.
4. Final Snapshot publication and result-envelope publication compare the expected revision and identity before mutation; rejected operations leave Context/store state unchanged.
5. Duplicate result return before commit fails or returns the same accepted identity without republishing; exact durable replay after consumption still returns the original settlement identity without duplicate effects.
6. Settlement consume requires the exact context/session/snapshot/result revision token and occurs only after durable save and live publication; failure leaves the accepted context retryable.
7. Interleaving tests cover old callback after snapshot advancement, old callback after result publication, wrong revision, duplicate result return, persistence failure/retry, exact committed replay, and different-result conflict.
8. Prior A1-A4, B1-B2, P2-14 and scene-transition stale-callback behavior remains passing.
9. Safe affected builds and `dotnet build rpg.sln -maxcpucount:2 -v:minimal` succeed; no excluded preview runner is executed.
10. Exact diff passes whitespace/integrity checks and complete `godot-code-review` with no scoped Critical or Improvement finding unresolved.

## Current Progress Snapshot

Completed:

- P2-03 evidence, current store/settlement/router code, and accepted authority were reviewed.
- Existing protection was classified: exact context/session/snapshot comparison and durable replay exist, but no captured revision lease protects mutations of the same Context object.
- Clean baseline recorded at `main` HEAD `955605a754950cb3d7c8c861a07bd426c76e5a7f`.

Remaining:

- RED-GREEN-REFACTOR implementation, focused verification, exact review, and independent verification.

## Pause, Blocker, And Resume

- Pause/blocker: The fixed `executor-xhigh` invocation was rejected by the local Codex usage limit before it could read or modify the repository. No implementation work started.
- Resume condition: Codex execution quota becomes available after the reported reset time, 2026-07-13 00:28 Asia/Shanghai, or the user provides another explicit fixed `gpt-5.6-sol` + `xhigh` execution channel.
- Resume entry: Read the required authority and skills, hash the store, Active Context model, Draft finalization, Runtime result publication, SceneTransitionRouter, settlement commit service, and current transaction/router tests before editing.
- Latest verification: P2-14 checkpoint passed; P2-03 production/test files remain untouched at clean HEAD `955605a754950cb3d7c8c861a07bd426c76e5a7f`.

## Execution Record

- 2026-07-12: Primary discussion context confirmed the remaining defect is mutable-identity reconstruction rather than absence of all identity checks. No authority contradiction was found.
- 2026-07-12: Attempted to start the required fixed `executor-xhigh` (`gpt-5.6-sol`, `xhigh`), but the local Codex execution service returned its usage-limit error before repository work began. The task was marked `Blocked` with an exact resume condition; no code, test, authority, preview, scene, or resource file changed.

## Final Result

Pending execution and independent verification.
