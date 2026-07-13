# Defense-Only Foundation Construction Authority

Status: Completed
Executor: executor
Verifier: primary Agent
Created: 2026-07-13
Updated: 2026-07-13

## Objective

Align accepted gameplay and system authority so the first city-construction version uses the existing bounded detailed-map placement interaction only for defensive fortifications.

## Confirmed Discussion Result

- The first construction version does not expose ordinary economy, recruitment, hero, workshop, or other non-defensive city buildings for player construction.
- The player-facing construction list contains only defensive fortifications whose detailed-map position can materially affect a local defense battle.
- Reuse the accepted authored construction-region, building-card, mouse-preview, grid-snap, footprint, bounds, overlap, eligibility, and resource-cost flow.
- Pure economy buildings such as farms, markets, lumber camps, and mines are outside this first construction version.
- Training grounds, taverns, and other facilities whose placement has no first-version battle consequence are also outside the first player-facing construction list.
- The initial defensive set remains deliberately small, with arrow towers as the only confirmed baseline. Medical facilities and other support buildings remain outside the first construction version even if they could later affect battle.
- Passability-changing walls, barricades, traps, and similar topology-changing structures are not part of this authority change and require later focused discussion.

## Authority Impact

Update the durable gameplay authority in:

- `gameplay-design/content-systems-long-term-design.md`
- `gameplay-design/details/cities-and-locations/README.md`

Update implementation-facing authority only where required to remove contradictions about the foundation construction scope:

- `system-design/strategic-management-system-architecture.md`
- `system-design/semantic-map-marker-architecture.md`
- `system-design/strategic-battle-bridge-architecture.md` if its local-support scope needs clarification

No installed GodotPrompter skill applies because this task changes authority documents only and performs no Godot implementation.

## Execution Scope

- Replace the accepted first-version broad city-building pool with defense-only construction scope.
- Preserve city development, economy, recruitment, corps support, and special facilities as later strategic-management capabilities without implying that they are freely placed in the first construction version.
- Preserve the current generic construction-region mechanism and its category-neutral placement legality.
- Define the minimum battle-facing condition that makes a defensive fortification eligible for the first construction list.
- Remove or revise first-phase lists, examples, and architecture statements that imply passive economy buildings are part of the foundation placement slice.
- Preserve unrelated existing edits in the dirty worktree.

## Non-Goals

- No code, configuration, scene, resource, test, or UI changes.
- No final defensive-building roster, balance values, art requirements, battle stats, destruction persistence, repair economy, upgrade tree, or build duration.
- No walls, roadblocks, route sealing, navigation-topology mutation, siege-engine system, or attacker AI design.
- No redesign of the existing authored construction-region interaction.
- No implementation or verification of city-defense battles.

## Constraints And Risks

- The repository is already dirty with unrelated strategic-map work, including edits to some target authority files; patches must preserve those changes.
- Defense-only construction is meaningful only when placed structures cross the accepted Strategic Battle Bridge as position-aware local support or battle entities. The documents must not claim that this implementation already exists.
- Existing economy-building configuration and code may remain as implementation evidence after this docs-only task; they do not override the updated authority and require a separate confirmed implementation task to align.
- Construction-region labels must not silently become building-category bans. Reuse the current placement rules and narrow only the player-facing first-version building definitions/list.

## Acceptance Criteria

- Gameplay authority clearly states that the first player-facing construction list contains only defensive fortifications.
- Gameplay authority no longer lists farm, market, lumber camp, mine, training ground, tavern, medical shrine, or medical facility as first-version placeable buildings.
- The existing bounded detailed-map construction-region and placement interaction remains accepted.
- Architecture authority no longer describes passive economy production buildings as part of the foundation construction slice.
- Architecture preserves Strategic Management ownership of persistent building facts and the Bridge snapshot boundary for battle-facing local support.
- Deferred topology-changing fortifications and non-defensive facility construction are explicitly outside this first version.
- No files outside the scoped authority and work-item documents are changed by this task.

## Current Progress Snapshot

Completed:

- Discussion completed and direction confirmed by the user.
- Existing authority, current building definitions, construction-region rules, Bridge support boundary, Git branch, and dirty worktree were inspected.
- Active work item created.
- Execution started on `main`; repository routing and all authority files named by this task were read.
- Gameplay authority now limits the first player-facing construction list to defensive fortifications, with Arrow Tower as the sole confirmed baseline; medical/support and every other non-defensive building are explicitly deferred even if they could later affect battle.
- System authority now preserves generic construction-region placement, Strategic Management persistence ownership, and the Bridge position-aware support/entity boundary while deferring non-defensive and topology-changing construction.
- Focused diff, whitespace, obsolete-admission, and cross-document contract searches completed successfully after the verification correction.
- Execution mutations were limited to the five scoped authority files and this work item; no code, configuration, scene, resource, test, or UI file was changed by this task.

Remaining:

- None.

## Pause, Blocker, And Resume

Pause or blocker: None. The scoped correction and independent verification are complete.

Resume condition: Reopen only if later implementation work or a newly accepted gameplay decision reveals an authority contradiction.

Resume entry: Read this task and review the facilities sections in the two gameplay authority files, `Cities And Facilities` and `Contracts` in Strategic Management architecture, `Strategic Construction Region Consumption` in semantic-marker architecture, and `Local Building Support Snapshot Boundary` in Bridge architecture. Confirm that Arrow Tower is the sole baseline, medical/support buildings are deferred, local support is only a Bridge representation of an eligible defensive fortification's effect, and existing construction regions remain category-neutral. Use `git diff --check` and do not infer task changes from unrelated dirty-worktree diffs.

Latest verification: Primary Agent independently re-verified the corrected authority on 2026-07-13. `git diff --check` passed for all five scoped authority documents. Both player-facing first-version lists contain only Arrow Tower; medical/support and every other non-defensive facility are explicitly deferred; local support is only a Bridge representation of an eligible defensive fortification's effect; construction-region labels remain category-neutral; and topology-changing fortifications remain deferred. No code, configuration, scene, resource, test, or UI change belongs to this task.

## Execution Record

- 2026-07-13: User confirmed defense-only first-version construction and requested authority-document updates.
- 2026-07-13: Execution started on `main`. Read repository/work-item routing and every authority file named by this task; confirmed the scoped contradiction is limited to obsolete first-version economy/facility construction claims. Recorded that the dirty worktree already contains unrelated strategic-map edits, including edits in target authority files, which must be preserved.
- 2026-07-13: Meaningful milestone: synchronized gameplay and architecture authority. Preserved the category-neutral construction-region mechanism, narrowed only the first player-facing definitions/list, retained Strategic Management ownership of durable building facts, and clarified the Bridge requirement for position-aware battle support or an explicitly accepted battle-entity snapshot.
- 2026-07-13: Handoff checks passed: `git diff --check` reported no errors for scoped tracked documents; focused searches found no obsolete first-version broad building-pool or foundation city economy-building authority phrases; contract searches confirmed defense-only list ownership, category-neutral construction regions, position-aware Bridge eligibility, and explicit topology deferral. No GodotPrompter skill applied because execution was documentation-only.
- 2026-07-13: Independent verification returned the task to `In Progress`. The task transcription and authority still conditionally admitted a medical facility, but the user's final confirmation was narrower: first-version player construction contains defensive fortifications only. Required correction: defer medical/support buildings and retain position-aware support language only for battle effects produced by eligible defensive fortifications.
- 2026-07-13: Correction milestone: removed medical/support buildings as conditionally eligible first-version content across the scoped gameplay and architecture authority. Arrow Tower is now the sole confirmed baseline, while position-aware local support remains only a Strategic Battle Bridge representation of an eligible defensive fortification's battle effect. Existing construction regions and unrelated dirty-worktree edits were preserved. Focused verification remains before handoff.
- 2026-07-13: Correction handoff checks passed. Scoped diff whitespace and task trailing-whitespace checks were clean; focused contract searches confirmed defense-only first-list eligibility, explicit medical/support deferral, Arrow Tower as the sole baseline, Bridge representation boundaries, unchanged category-neutral construction-region authority, and deferred topology-changing structures. No code, configuration, scene, resource, test, or UI file was changed. No installed GodotPrompter skill applied because this correction was documentation-only. Returned the task to `Awaiting Verification`.
- 2026-07-13: Primary Agent independently verified every acceptance criterion and marked the task `Completed`. Focused whitespace and authority-contract checks passed; the task is ready for archive. No installed GodotPrompter skill applied because verification was documentation-only.

## Final Result

The scoped docs-only authority correction is applied, independently verified, and complete.

Evidence:

- Gameplay authority limits the first player-facing construction list to defensive fortifications and names Arrow Tower as the sole confirmed baseline.
- Gameplay authority explicitly defers medical shrines, medical facilities, every support building, farms, markets, lumber camps, mines, training grounds, taverns, workshops, and all other non-defensive facilities from the first construction version even if they could later affect battle.
- Strategic Management architecture preserves persistent building ownership and the generic bounded placement legality path while removing passive economy buildings from the construction foundation.
- Semantic-marker architecture preserves existing construction regions and labels as category-neutral placement metadata.
- Bridge architecture uses position-aware local support only as a representation of an eligible defensive fortification's battle effect, requires a validated battle anchor or separately accepted position-aware battle-entity snapshot, and does not claim that implementation already exists.
- Topology-changing walls, barricades, traps, roadblocks, and route sealing remain deferred.

Remaining risks:

- Existing code and configuration may still expose broad or economy-building definitions; aligning implementation requires a separate confirmed work item.
- Position-aware defensive support and battle-entity integration are authority contracts only in this task and remain unimplemented until separately authorized.
- The repository remains heavily dirty from unrelated strategic-map work, including pre-existing edits in several target authority files; independent review must preserve that work and assess only the scoped authority changes.
