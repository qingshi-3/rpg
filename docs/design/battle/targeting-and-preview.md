# Targeting And Preview

This document defines shared vocabulary for target selection, range display, action previews, and Intent previews.

The goal is to keep rules, UI, AI, and final resolution from drifting into separate interpretations.

## Shared Terms

- `legal_targets`: cells or units that the current `TargetRule` allows as a valid choice.
- `hovered_target`: the cell or unit currently indicated by player input.
- `resolved_target`: the final target after validating or snapping `hovered_target` against `legal_targets`.
- `affected_cells`: cells that would be affected if the action resolves on `resolved_target`.
- `affected_units`: units that would be affected if the action resolves on `resolved_target`.
- `preview_effects`: player-facing preview of the Effects that would happen on confirmation.
- `intent_preview`: current-state preview generated from an enemy's committed high-level Intent.

## Ownership Rules

- `TargetRule` owns `legal_targets`.
- Player input owns `hovered_target`, but cannot change rule legality.
- Action resolution owns `resolved_target`, `affected_cells`, and `affected_units`.
- UI renders previews; it must not become the source of truth for targeting rules.
- AI and Intent should use the same targeting vocabulary as player actions.

## Preview Layers

- Selection preview: highlights `legal_targets` for the currently selected action.
- Hover preview: highlights `affected_cells` and `affected_units` for the current `hovered_target` or `resolved_target`.
- Cost preview: shows AP cost and repeated-action cost changes before confirmation.
- Intent preview: shows enemy target, movement path, attack area, or status effect before the enemy phase.
- Result preview: summarizes likely damage, movement, push, block, or status changes when the result can be shown clearly.

## Phase 1 Scope

Phase 1 should support only the preview language required by the tutorial battle:

- Hero movement range.
- Enemy melee and ranged Intent preview.
- AP cost preview for basic actions.
- Basic attack target and result preview.

Cards, minion rule previews, push/block targeting, complex multi-step targeting, and hidden information previews are out of scope for the current minimal loop.

## Consistency Rules

- The same action must not use different target shapes for preview and resolution.
- Intent display must represent the committed enemy tactical posture, not a fresh unrelated recalculation every frame.
- Intent preview may update after battlefield state changes, but only within the stored Intent policy.
- If an Effect modifies Intent, the preview should update only after that Effect resolves.
- A preview can be simplified visually, but the simplification must not imply a different rule.
- If UI cannot confidently show a result, it should show the target and affected area rather than inventing a precise number.
