# Thunder Fold Two-Stage Targeting

Status: Accepted for authority merge

Requirement Id: `thunder-fold-two-stage-targeting`

Parent Proposal: `design-proposals/archived/2026-06-11-thunder-mark-skill-family/`

Supersedes: None

Superseded By: None

Amends: `design-proposals/archived/2026-06-11-thunder-mark-skill-family/`

Amended By: None

Affected Authority Documents:

- `gameplay-design/details/combat-command/README.md`
- `system-design/battle-command-architecture.md`
- `system-design/battle-runtime-architecture.md`
- `system-design/battle-content-progression-architecture.md`

Related Implementation Proposal: `gameplay-alignment/implementation-proposals/2026-06-11-thunder-mark-demo-skill-family.md`

## Current Design

Accepted thunder-mark design says Thunder Mark Fold teleports the hero to a player-selected legal anchor near one live mark. The current implementation treats the skill as a single cell-target command: the player clicks a destination cell, Runtime finds any live mark for the battle group, and release-time effect validation rejects destinations that are not near that mark.

This is underspecified for player operation. It does not require the player to choose which mark drives the fold, does not expose the legal landing ring after mark selection, and can allow an invalid destination to enter the command stream before failing later.

## Expected Design

Thunder Mark Fold is a two-stage commander skill:

1. The player activates the skill and enters mark selection.
2. The first click must select a live thunder mark owned by the selected hero company. Valid mark selections are a ground-mark cell or an enemy actor carrying an attached mark.
3. Selecting a mark does not submit the skill. It switches the UI into landing selection and renders legal landing candidates around the selected mark.
4. The first implementation uses a landing radius of 3 square-grid cells around the selected mark anchor.
5. Only empty, topology-legal anchors for the caster footprint are valid landing selections.
6. The command is submitted only after a legal landing anchor is selected.
7. Runtime acceptance must receive enough payload to identify the selected mark and the requested landing anchor, then validate that the mark is still live, the landing anchor is still within the content-defined mark radius, and the destination is still legal and empty.
8. Accepted fold commands prelock the requested landing anchor. On release, Runtime revalidates the mark and destination, commits displacement through the shared displacement boundary, clears stale movement and target context, and returns the actor to normal state-machine decision flow from the new anchor unless another active skill lock still owns it.

## Non-Goals

- Do not implement Thunder Mark Transfer.
- Do not add individual soldier micro or drag-based landing selection.
- Do not make Presentation decide final teleport legality.
- Do not consume or delete the mark on fold unless a later accepted rule changes the kit.

## Acceptance

- Authority documents state the two-stage selection rule and Runtime validation boundary.
- The implementation proposal records RED/GREEN coverage for mark selection, destination validation, stale movement clearing, and post-fold decision flow.
