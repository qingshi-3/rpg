# Direct Battle Trigger Entry Proposal

Status: Merged

## Requirement Id

REQ-2026-06-14-direct-battle-trigger-entry

## Parent Proposal

None

## Supersedes

- `gameplay-design/vertical-slices/first-playable-slice.md` VS-03 strategic battle-preparation choice requirement.

## Superseded By

None

## Amends

- `design-proposals/archived/2026-06-13-strategic-battle-bridge-contract/`
- `design-proposals/archived/2026-06-13-strategic-management-system-architecture/`

## Amended By

None

## Affected Authority Documents

- `gameplay-design/vertical-slices/first-playable-slice.md`
- `system-design/strategic-management-system-architecture.md`
- `system-design/strategic-battle-bridge-architecture.md`

## Related Implementation Proposals

- `gameplay-alignment/implementation-proposals/2026-06-14-direct-battle-trigger-entry.md`

## Current

The first playable slice currently requires one mutually exclusive strategic battle-preparation choice before Bonefield assault. The three authored options are Scout Bonefield, Drill The Corps, and Prepare Field Support. The implementation stores this choice in Strategic Management, gates battle entry through the Strategic Battle Bridge, and carries preparation metadata into pre-battle text and battle feedback.

This requirement is too early for the current combat direction. The options are mostly text metadata and do not have fully implemented battle effects. Keeping them as a mandatory gate blocks the intended direct expedition-to-battle flow and confuses the player.

## Expected

Remove the mandatory strategic battle-preparation choice from the first playable slice.

The player flow becomes:

```text
select or form an expedition force
-> right-click a hostile battle-capable city / stronghold
-> the expedition travels on the world map
-> arrival pauses world-map time, focuses the camera on the battle location, and opens a "触发战斗" confirmation dialog
-> player confirms and enters the battle preparation / deployment scene
```

The Strategic Battle Bridge should not require a strategic preparation selection before creating a battle session. The bridge may still own battle deployment and launch readiness after the player confirms battle entry.

Future scouting, siege methods, or alternate entry routes require a separate accepted design before returning as gameplay.

## User Acceptance

User accepted deleting the current strategic battle-preparation feature and specified the direct battle trigger path:

> 删除，先不做这个，操作路径是：选择出征部队，然后右键出征，右键敌人城池的时候则直接给出弹窗：“触发战斗”，然后屏幕中心定位到触发战斗的地点，然后用户点击确认则进入战斗环节
