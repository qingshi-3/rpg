# Codex Godot UI Guidance

This document adapts the local GodotPrompter reference clone for UI work in this project.

## Local Reference

- Clone path: `.codex/external/GodotPrompter`
- Source: `https://github.com/jame581/GodotPrompter`
- Current local revision: `604c558`
- License: MIT

Before creating or refactoring UI, read only the relevant GodotPrompter skill files:

- General UI: `.codex/external/GodotPrompter/skills/godot-ui/SKILL.md`
- HUD: `.codex/external/GodotPrompter/skills/hud-system/SKILL.md`
- Responsive layout: `.codex/external/GodotPrompter/skills/responsive-ui/SKILL.md`
- UI task framing: `.codex/external/GodotPrompter/agents/godot-ui-designer.md`

Do not copy the whole external skill content into project docs. Summarize the rule that affects the current task and link to the source path.

## Project Adaptation

GodotPrompter examples are mostly GDScript-first. This project is Godot 4.5 + C#, so translate implementation snippets into C# and keep scene structure in authored `.tscn` resources.

Project-specific rules:

- Use `Control` roots for screen UI and `CanvasLayer` roots for HUD layers that must stay independent from camera movement.
- Prefer `MarginContainer`, `VBoxContainer`, `HBoxContainer`, `GridContainer`, `PanelContainer`, `ScrollContainer`, and authored child scenes over manual `Position` / `Size` layout in code.
- Centralize repeated visual styling in `Theme` resources and existing skin helpers instead of scattering per-node overrides.
- Reuse `assets/themes/basic_ui_2_world_theme.tres`, `Rpg.Presentation.Common.GameUiSkin`, and `Rpg.Presentation.Common.GameUiSceneFactory` where they fit.
- Put battle UI scenes under `scenes/battle/ui/` and world UI scenes under `scenes/world/ui/`.
- Put reusable UI behavior under `src/Presentation/UI/` or the owning presentation domain, not under domain logic.
- Player-visible in-game text defaults to Chinese.
- Runtime code should bind data, signals, and state transitions; authored resources should own layout, theme, textures, and reusable UI composition.
- UI work must not modify battle flow, AP, or `TurnSystem`. If UI needs new gameplay data, expose it through view models, definitions, or existing presentation-facing query APIs.

## Codex Workflow For UI Tasks

When asking Codex to build UI, include this context block or ask Codex to derive it before editing:

```text
Use docs/collaboration/codex-godot-ui-guidance.md.
Read GodotPrompter skill(s): godot-ui; add hud-system for HUD; add responsive-ui for resolution/platform work.
Target surface: <battle HUD / strategic world HUD / dialog / menu / reusable widget>.
Target scenes: <existing .tscn path or new scene path>.
Target scripts: <existing C# path or new C# path>.
Data source: <view model, definition, controller, signal, or temporary mock>.
Responsive targets: 1960x1080 base, 16:9 desktop, one wider desktop, and one narrower/taller layout if relevant.
Constraints: C#, authored .tscn/.tres resources, Chinese player text, reuse GameUiSkin/GameUiSceneFactory when suitable.
Expected output: scene tree, theme strategy, C# wiring, data contract, manual test notes.
```

Codex should then:

1. Inspect the existing scene and script paths before proposing layout.
2. Pick the root type and container hierarchy first.
3. Identify whether the task needs `godot-ui`, `hud-system`, `responsive-ui`, or a combination.
4. Implement layout in `.tscn` / `.tres` resources and keep C# focused on binding and state updates.
5. Add low-noise logs for key user-facing state transitions or failures.
6. Verify at the target resolutions when the change affects layout.

## Native Codex Skill Discovery

The local clone alone is enough for project documentation and manual consultation. Native Codex skill discovery requires a user-level skills link and a Codex restart, so do this only when you want GodotPrompter skills available in future Codex sessions:

```powershell
New-Item -ItemType Directory -Force -Path "$env:USERPROFILE\.agents\skills"
cmd /c mklink /J "$env:USERPROFILE\.agents\skills\godot-prompter" "D:\godot\rpg\.codex\external\GodotPrompter\skills"
```

After restarting Codex, the GodotPrompter skills should be discoverable from the user skill directory. This affects user-level Codex skill discovery, not only this repository.
