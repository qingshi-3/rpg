# Godot Local Knowledge Base

This directory is the local first-stop reference for Godot work in this project.

## Version

- Engine target: Godot 4.5.x.
- Official docs source: `.codex/external/godot-docs-4.5/`, cloned from `https://github.com/godotengine/godot-docs.git` branch `4.5`. It is intentionally outside `res://` so Godot does not import thousands of documentation assets.

## How to search

Use `rg` before guessing:

```powershell
rg -n "CanvasLayer|mouse_filter|_gui_input|Control|Node2D" docs/70-collaboration/local-knowledge/godot .codex/external/godot-docs-4.5
```

## High-value official routes

- UI overview: `../../../.codex/external/godot-docs-4.5/tutorials/ui/`
- Input handling: `../../../.codex/external/godot-docs-4.5/tutorials/inputs/`
- 2D and Canvas layers: `../../../.codex/external/godot-docs-4.5/tutorials/2d/`
- Performance: `../../../.codex/external/godot-docs-4.5/tutorials/performance/`
- Best practices: `../../../.codex/external/godot-docs-4.5/tutorials/best_practices/`
- Class reference: `../../../.codex/external/godot-docs-4.5/classes/`
- `Control`: `../../../.codex/external/godot-docs-4.5/classes/class_control.rst`
- `CanvasLayer`: `../../../.codex/external/godot-docs-4.5/classes/class_canvaslayer.rst`
- `Node2D`: `../../../.codex/external/godot-docs-4.5/classes/class_node2d.rst`

## Curated notes

- `curated-notes/ui-control-canvaslayer.md`
- `curated-notes/input-mouse-filter.md`
- `curated-notes/node2d-control-boundary.md`
- `curated-notes/scene-organization.md`
- `curated-notes/performance-frame-loop.md`
- `curated-notes/csharp-godot-patterns.md`

## Project rule

Before changing Godot UI, input, map presentation, animation, or per-frame logic, consult the relevant curated note and official route above. If code and docs disagree, use the official docs plus current project architecture as authority, then update the curated note if a long-lived rule changed.
