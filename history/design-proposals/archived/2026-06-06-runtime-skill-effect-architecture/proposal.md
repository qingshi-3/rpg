# Runtime Skill And Effect Architecture

Status: Archived

## Relationship Metadata

Requirement Id: `REQ-RUNTIME-SKILL-EFFECT-ACTION-ARCHITECTURE`

Parent Proposal: None

Supersedes: None

Superseded By: None

Amends: None

Amended By: None

Affected Authority Documents:

- `gameplay-design/details/combat-command/README.md`
- `system-design/battle-content-progression-architecture.md`
- `system-design/battle-command-architecture.md`
- `system-design/battle-runtime-architecture.md`
- `system-design/battle-ai-boundary-architecture.md`
- `system-design/battle-result-settlement-architecture.md`

Related Implementation Proposals:

- Existing context: `gameplay-alignment/implementation-proposals/2026-06-06-one-hero-active-skill-flow.md`
- Follow-up after this proposal is merged and archived: `gameplay-alignment/implementation-proposals/2026-06-06-runtime-skill-effect-action-flow.md`

## Current Architecture

The accepted direction already requires player-cast hero active skills, command-channel separation, explicit Runtime actor phases, and Runtime event/report attribution. It does not yet fully define the long-term boundary between skill definitions, skill command content, behavior-tree release decisions, action timing, and reusable effect execution.

The current first-slice implementation proposal proves a narrow hero active skill command path, but it is intentionally not the target architecture. It hardcodes one skill effect, has no target-selection contract, no cast or recovery action phase, no default interruption rules, and no reusable effect executor shared with basic attacks, equipment, relics, or environment effects.

## Expected Architecture

Skill work is split into five durable responsibilities:

- Skill definition: content-owned single source for identity, targeting, cost, cooldown, action timing, interrupt traits, and effect references.
- Skill command: player or AI intent that names the skill and locks required target data when the command is accepted.
- Release decision: actor behavior logic decides when an accepted skill order can start, including action-lock, cooldown, cost, caster, and target validity checks.
- Skill action: Runtime-owned cast, impact, recovery, interruption, failure, and event timing.
- Effect execution: source-agnostic Runtime effect resolver for damage, healing, shield, status, movement, control, resource, morale, and later equipment, relic, terrain, or support effects.

Basic attacks should converge on the same action-plus-effect model. A basic attack is an action with one impact point and a basic-attack effect payload. It must not remain a separate damage authority once the generic effect executor exists.

## Confirmed Rules To Encode

- Active skills are divided into targeted skills and non-targeted skills.
- Targeted skills require a player-selected target before command acceptance.
- Non-targeted skills do not require a unit target; their targeting data is defined by the skill.
- Both targeted and non-targeted active skills can interrupt a basic attack before its damage impact by default.
- Default active skills cannot cancel basic attack recovery after basic attack damage has already resolved.
- Canceling basic attack recovery is an explicit mechanic trait granted by a hero, skill, equipment, relic, or other accepted source.
- Default active skills cannot interrupt an active skill cast or recovery.
- Skill-to-skill interruption, fire-and-forget/offhand release, instant release, and recovery canceling are explicit definition traits, not default behavior.
- A targeted skill checks range and locks the selected target at command acceptance.
- If the locked target later moves out of range before execution, the skill still affects that locked target.
- If the caster or locked target is dead, invalid, untargetable, or otherwise fails the execution precheck, the skill fails and does not release.
- Execution prechecks belong to actor behavior / Runtime skill handling, not Presentation UI.
- During tactical pause, skill commands may be submitted and target previews may update, but live battle state changes only when Runtime resumes and reaches a valid release or interrupt boundary.

## Non-Goals

- This proposal does not define final skill balance, hero-specific content, projectile simulation, full area-shape implementation, or UI layout.
- This proposal does not authorize code, scene, resource, shader, or data implementation. Code work must wait until the expected documents are accepted, merged into authority, archived, and followed by a focused implementation proposal.
- This proposal does not replace LimboAI with a new AI system. It only clarifies what skill-release decisions may ask Runtime to execute.

## Merge Plan After Acceptance

Accepted by user instruction on 2026-06-06. The `expected/` copies were merged into their repository-relative authority paths, and this proposal was archived. Implementation work now starts from `gameplay-alignment/implementation-proposals/2026-06-06-runtime-skill-effect-action-flow.md`.
