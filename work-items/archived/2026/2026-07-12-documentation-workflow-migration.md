# Documentation Workflow Migration

Status: Completed
Executor: `gpt-5.6-sol` + `high`
Verifier: Parent Agent / independent verification context
Created: 2026-07-12
Updated: 2026-07-12

## Objective

Replace the retired design-proposal and implementation-proposal lifecycle with one task-document workflow that reliably hands confirmed discussion results to an execution Agent, supports interrupted work, updates durable authority directly, and archives completed task evidence without making history active authority.

## Confirmed Discussion Result

- All state-changing work uses one integrated discussion stage followed by one execution stage; gameplay and system architecture may be discussed and confirmed together.
- After user confirmation, the discussion Agent creates one self-contained task document. The execution Agent must rely on that document plus current authority and repository state, not conversational memory.
- One task document carries confirmed conclusions, authority impact, scope, non-goals, constraints, acceptance, progress, execution evidence, and final result. Do not split one task into business, design, implementation, and acceptance proposals.
- Supported task states are `Ready`, `In Progress`, `Needs Discussion`, `Paused`, `Blocked`, `Awaiting Verification`, `Completed`, and `Cancelled`.
- `Ready`, `In Progress`, `Needs Discussion`, `Paused`, `Blocked`, and `Awaiting Verification` remain active. Only `Completed` and `Cancelled` are archived.
- An execution Agent may update progress and evidence but must not silently change confirmed conclusions. Direction conflicts return the same task to `Needs Discussion`; after user reconfirmation, the discussion Agent revises it and returns it to `Ready`.
- Durable gameplay or architecture decisions update `gameplay-design/` or `system-design/` during execution. Task documents are execution contracts and historical evidence, never durable authority.
- Historical proposals and implementation records are available only through a central history route and are read only when the user explicitly requests history or resumes the exact subject.

## Authority And Route Impact

- Global guidance: `C:\Users\qs\.codex\AGENTS.md` and the execution custom-agent instructions.
- Project routing: `AGENTS.md`, `README.md`, `.codex/collaboration.md`, and `gameplay-alignment/authority-map.md`.
- Current design indexes: `gameplay-design/README.md` and `system-design/README.md` only if route language requires synchronization.
- Work tracking: new `work-items/README.md`, `work-items/active/`, and `work-items/archived/`.
- History: new `history/README.md`, with both complete legacy trees preserved as `history/design-proposals/` and `history/implementation-proposals/` without rewriting their bodies.

## Execution Scope

1. Define the task-document lifecycle, statuses, required sections, handoff rules, authority synchronization, pause/block/resume semantics, and archive rules in `work-items/README.md`.
2. Create the central history route and physically move both retired documentation trees beneath it while preserving every existing tracked, modified, and untracked file.
3. Convert the two unfinished legacy implementation records into concise active work items:
   - hero/corps reassignment manual QA -> `Awaiting Verification`;
   - first-city site-map layout scaffold -> `Paused`.
4. Leave their original documents in history as evidence; do not treat them as current execution instructions.
5. Rewrite all current bootstrap, authority, collaboration, and documentation routes so Agents check relevant active work items and avoid history by default.
6. Update the global execution Agent instructions so they require a confirmed active task document, maintain its progress snapshot, and return control on direction conflicts.
7. Complete verification, update this document with evidence, then move it to `work-items/archived/2026/` with `Status: Completed`.

## Non-Goals

- Do not change gameplay rules, system architecture contracts, code, scenes, resources, tests, or runtime behavior.
- Do not rewrite legacy proposal or implementation-record bodies.
- Do not delete historical records during this migration.
- Do not resolve the content or QA of the two migrated unfinished tasks.
- Do not modify unrelated user worktree changes, including current history edits and implementation-record moves.

## Execution Constraints

- Work on `main` and preserve the dirty shared worktree.
- Use PowerShell end-to-end for directory moves. Resolve and verify source and destination paths remain under `D:\godot\rpg` before moving.
- Never read archived proposal bodies for general context. Only the two currently unfinished implementation records may be read because they are being converted into active task handoffs.
- Historical paths embedded inside historical bodies may describe their original location; do not mechanically rewrite bodies. Current routes and live cross-references must use the new paths.
- Keep task documents concise and self-contained. Do not copy code diffs, full logs, authority document bodies, or conversational transcripts into them.
- No installed GodotPrompter skill applies because this task changes only documentation routing and Codex workflow configuration.

## Acceptance Criteria

- `work-items/README.md` defines all eight states and legal transitions, plus required pause/block/resume and verification fields.
- Every current state-changing workflow routes through one confirmed active task document before execution.
- `history/README.md` is the only default route into legacy records.
- The old proposal trees exist under `history/`, and no file from them is lost.
- Current project routes contain no mandatory design-proposal or implementation-proposal workflow and no live references to their old physical paths.
- The two unfinished legacy records have equivalent active task handoffs with their remaining work and verification state preserved.
- Global custom-agent TOML files parse, current Markdown diffs pass whitespace checks, and current-route link targets exist.
- This migration task is completed and archived only after all checks pass.

## Current Progress Snapshot

### Completed

- Discussion result confirmed by the user.
- Confirmed discussion result captured in this active task document.
- Defined the eight-state task lifecycle, required document shape, handoff rules, authority synchronization, interruption semantics, independent verification, and archive rules in `work-items/README.md`.
- Added the `work-items/archived/` boundary for year-organized completed and cancelled tasks.
- Added `history/README.md` as the only default legacy route.
- Moved both complete legacy trees beneath `history/` with per-file relative-path, length, and SHA-256 verification: 507 design-history files and 112 implementation-history files.
- Converted hero/corps reassignment manual QA to an `Awaiting Verification` active task and first-city site-map layout authoring to a `Paused` active task.
- Updated global and project Agent rules, all three global execution profiles, project bootstrap, collaboration, authority, alignment, architecture, and workstream routes.
- Corrected the independent-verification findings: isolated execution-channel fallback, project bootstrap and lifecycle closure, verifier metadata and separation, stage-specific sandbox rules, risk-proportionate acceptance, live audit recommendations, tech-debt baseline terminology, and editor folder routes now follow the active-work-item workflow.
- Corrected the second-round independent-verification findings: historical Executor separation, completion-only verification handoff wording, removal of parallel baseline terminology, collaboration role/interface ownership, and conditional code-risk review checks.
- Replaced the four remaining global Agent description references to the approved discussion baseline with user-confirmed active task terminology while preserving the routine, compatibility, state-changing, and correctness-critical purposes.
- Recorded that no installed GodotPrompter skill applies to this documentation-only migration.
- Completed the correction-round executor-side route, retired-gate, state/Verifier, TOML, link-target, diff, and new-text whitespace checks.
- Parent Agent independently verified the complete migration, correction rounds, global model routing, active-task lifecycle, current links, and archive integrity.

### Remaining

- None.

### Pause Or Blocker

- None.

### Resume Condition

- Not applicable; the task is complete.

### Resume Entry

- Not applicable; this archived task is historical execution evidence only.

### Latest Verification

- Parent Agent final verification found no live old proposal paths, mandatory proposal gates, or parallel task-baseline rules in current routes. The retired trees are absent from their old locations and contain 507 design-history files plus 112 implementation-history files under `history/`.
- Independent Git-blob comparison found 607 same-path files unchanged from `HEAD`, nine same-path files preserving existing modifications, two previously moved implementation records preserved under their existing `archived/` subdirectory with modifications, and the untracked strategic-region preview record preserved in history.
- The tracked `design-proposals/proposal-template.md` was already absent from the 507-file source tree recorded before migration. Because no independent pre-move manifest was retained, post-hoc verification cannot prove that pre-existing deletion; it was not restored so the migration would not overwrite the user's dirty-worktree state.
- Forty-seven current Markdown files passed local-link resolution. Final current-route scans, TOML parsing, model/effort/registration guards, `ReadOnly` verification, `git diff --check`, and current new-file whitespace checks passed.
- `rg` found no `discussion baseline`, `execution baseline`, or `执行基线` in global `AGENTS.md`, `agents/*.toml`, or `config.toml`.
- Python `tomllib` parsed `executor.toml`, `executor-xhigh.toml`, `worker.toml`, and `config.toml`. Model and effort guards remained `gpt-5.6-sol` + `high` for executor/worker and `gpt-5.6-sol` + `xhigh` for executor-xhigh.
- Inspection confirmed the four corrected descriptions preserve their assigned routine execution, built-in compatibility, routine state-changing, and correctness-critical execution purposes. `config.toml` was changed only after clearing `ReadOnly`, and `ReadOnly` was immediately restored and verified true.
- `git diff --check` passed after the four-description correction. No Godot process, build, regression test, commit, staging action, historical-body read, or delegated Agent was used.
- Targeted metadata inspection confirmed all three active tasks have status-appropriate Executor and Verifier semantics: migration awaits an independent parent verifier, historical manual QA has no assigned Executor and retains its independent verifier, and the paused authoring task remains unassigned on both roles.
- Targeted `rg --hidden --no-ignore`, with the required path exclusions, found no remaining prohibited parallel-baseline workflow terminology.
- Targeted text inspection confirmed project `AGENTS.md` requires `Awaiting Verification` only when scoped execution is complete, does not let an execution Agent set `Completed`, and retains `Needs Discussion`, `Paused`, and `Blocked` lifecycle routes.
- Targeted collaboration-text inspection confirmed role spacing, technical-support and implementation responsibilities, single-active-task ownership, independent acceptance, active-task-scoped returns, and conditional build/regression and guard-test checks.
- Final inspection confirmed global `config.toml` intentionally changes only the executor and executor-xhigh descriptions, still parses, retains all model/effort/registration and permission settings, and remains `ReadOnly`. No Godot process, build, or regression test was run for this documentation-only task.

## Execution Record

- 2026-07-12: Parent Agent independently verified every acceptance criterion, recorded the pre-existing `proposal-template.md` evidence boundary and untracked-history operational risk, marked the task `Completed`, and archived it under `work-items/archived/2026/`.
- 2026-07-12: Applied only the user-directed four-description terminology correction in global `executor.toml`, `worker.toml`, and the executor/executor-xhigh entries in `config.toml`. Temporarily cleared only `config.toml`'s `ReadOnly` attribute, patched the two descriptions, and immediately restored `ReadOnly`; no other configuration content or profile settings were changed.
- 2026-07-12: Terminology scan, four-file TOML parsing, model/effort guards, config `ReadOnly` verification, and `git diff --check` passed. Returned the task to `Awaiting Verification`; independent verification and archival remain outstanding.
- 2026-07-12: User-directed terminology correction resumed the task at `In Progress`. Inspection confirmed the four global Agent descriptions still used the approved discussion baseline terminology; scope remains limited to replacing those descriptions with user-confirmed active task terminology while preserving each profile's purpose and all model/effort settings.
- 2026-07-12: Second-round independent verification returned the task to `In Progress`. Findings: the migrated manual-QA task assigned the same independent verifier role as both Executor and Verifier; project handoff wording over-constrained non-completion states; global and current route language retained parallel baseline terminology; collaboration roles, active-task ownership, and conditional document-task verification requirements needed tighter wording.
- 2026-07-12: Applied only the second-round directed corrections to the two active tasks, project and global Agent guidance, authority map, and collaboration contract; did not change code, gameplay, architecture facts, historical bodies, or configuration.
- 2026-07-12: Second-round targeted metadata, terminology, handoff-semantics, collaboration-text, config-guard, and diff checks passed. Returned the task to `Awaiting Verification` for the parent Agent's independent re-verification.
- 2026-07-12: Independent verification returned the task to `In Progress`. Findings: execution-channel fallback wording conflicted with the available isolated `codex exec` route; project bootstrap and work-item lifecycle omitted required verifier separation and terminal-state details; collaboration sandbox and acceptance guidance was over-broad; two live audits, the tech-debt register, project folder colors, and task terminology retained retired-workflow language or routes.
- 2026-07-12: Applied the fixed-scope acceptance corrections without changing gameplay, architecture, runtime, historical bodies, or `config.toml`; preserved all unrelated dirty-worktree changes.
- 2026-07-12: Correction-round read-only verification passed for live route/retired-gate scans, work-item state and Verifier invariants, current Markdown links, TOML parsing and config guards, `git diff --check`, and untracked text whitespace. Returned the task to `Awaiting Verification` for the parent Agent's independent re-verification.
- 2026-07-12: Added the active work-item lifecycle and central history route.
- 2026-07-12: Added concise active handoffs for the two explicitly named unfinished implementation records without changing their gameplay or implementation state.
- 2026-07-12: Whole-directory `Move-Item` was denied by Windows before any move occurred. Retried with a verified PowerShell-only filewise move: each destination file was hashed immediately, all source files were confirmed moved, and only empty source directories were removed.
- 2026-07-12: Preserved the newer strategic-region preview record and all other tracked, modified, and untracked legacy-tree files unchanged inside history; it was not converted into an unapproved third active task.
- 2026-07-12: Synchronized `C:\Users\qs\.codex\AGENTS.md`, all three global execution Agent TOML profiles, and repository routes to require a confirmed active task, maintain progress/evidence, and return direction conflicts as `Needs Discussion`.
- 2026-07-12: Executor-side verification passed. Final independent verification and archival remain assigned to the parent Agent.

## Final Result

Completed. Current work now follows discussion, user confirmation, one self-contained active work item, execution, independent verification, and completion/cancellation archival. Durable gameplay and architecture remain in their authority directories; retired proposal records are available only through `history/README.md` and are not default inputs.

Remaining risks: `design-proposals/proposal-template.md` remains absent as a pre-existing dirty-worktree deletion whose pre-move state cannot be independently reconstructed. The 619 history destination files are currently untracked until the user stages or commits this migration, so destructive untracked-file cleanup must be avoided. No gameplay, architecture, code, resource, test, or runtime behavior changed.
