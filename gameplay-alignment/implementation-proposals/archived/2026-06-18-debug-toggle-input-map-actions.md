# Debug Toggle Input Map Actions Implementation Proposal

Status: Implemented - Automated Verification Passed

Priority: Medium

## Relationship Metadata

- Origin: 2026-06-18 GodotPrompter implementation-standards audit.
- Requirement slice: Debug toggle shortcuts should use Project Input Map actions instead of hardcoded `Key.F3` / `Key.F4` checks in Presentation code.
- Originating design proposal: Not required; this is an implementation standards repair and does not change gameplay or UI architecture authority.
- Amendment proposals: None.
- Blocking issues: `PerformanceDebugOverlay`, `BattleDebugController`, and `BattleGuideGridDebug` still compare raw keycodes for debug toggles.
- Verification records: Automated verification passed on 2026-06-18.

## Authority

- Implements project `AGENTS.md` Godot Resource Authoring and Implementation Authority rules by keeping input bindings centralized in `project.godot`.
- Follows GodotPrompter `input-handling` guidance: discrete actions use `_UnhandledInput()` and Input Map action names, not raw key constants.
- Uses GodotPrompter skills: `input-handling`, `csharp-godot`, `godot-testing`, and `godot-code-review`.

## Goal

Move debug toggle shortcuts onto Input Map actions while preserving default keyboard behavior:

```text
performance_debug_toggle -> F3
battle_debug_toggle -> F3
battle_guide_grid_toggle -> F4
```

## Scope

- Add default Input Map actions to `project.godot`.
- Replace raw keycode checks in debug Presentation scripts with `InputEvent.IsActionPressed(...)`.
- Keep the existing debug default behavior, visibility defaults, and runtime diagnostics unchanged.
- Add regression coverage preventing reintroduction of hardcoded debug toggle keys.

## Non-Goals

- Do not add runtime rebinding UI.
- Do not change debug overlay content, sample interval, or battle debug component configuration.
- Do not change camera, battle runtime pause, or perception overlay input actions.
- Do not add gamepad bindings in this slice.

## Touched Systems

- Global performance debug overlay input.
- Battle debug controller input.
- Battle guide grid debug input.
- `tests/WorldSiteDeploymentCacheRegression` anti-rot coverage.

## Tests

- Add regression coverage proving:
  - `project.godot` defines `performance_debug_toggle`, `battle_debug_toggle`, and `battle_guide_grid_toggle`;
  - debug Presentation scripts use `IsActionPressed(...)`;
  - debug Presentation scripts no longer contain `ToggleKey`, `Key.F3`, `Key.F4`, or `InputEventKey` checks for these toggles.
- Re-run:
  - `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal`
  - `dotnet build rpg.sln -maxcpucount:2 -v:minimal`

## Diagnostics

- No new runtime logs required. This only changes how the same debug toggle input is read.

## Manual QA

- Press F3 in strategic/world-site contexts and confirm the performance overlay and battle debug controller toggle as before.
- Press F4 in battle debug context and confirm the guide grid toggles as before.

## Acceptance Evidence

- `project.godot` now defines `performance_debug_toggle`, `battle_debug_toggle`, and `battle_guide_grid_toggle` Input Map actions with the existing F3/F4 default bindings.
- `PerformanceDebugOverlay`, `BattleDebugController`, and `BattleGuideGridDebug` now use `InputEvent.IsActionPressed(...)` instead of raw `Key.F3` / `Key.F4` checks.
- `tests/WorldSiteDeploymentCacheRegression` guards the debug Input Map actions and prevents reintroducing `ToggleKey`, hardcoded F-key constants, or `InputEventKey` checks in these debug toggle scripts.
- Passed `dotnet run --project tests\WorldSiteDeploymentCacheRegression\WorldSiteDeploymentCacheRegression.csproj -v:minimal` on 2026-06-18.
- Passed `dotnet build rpg.sln -maxcpucount:2 -v:minimal` on 2026-06-18. The build still emits pre-existing nullable/source-generator warnings in test projects.
- Manual QA is still recommended for F3/F4 debug toggles in strategic-world and world-site battle contexts.
