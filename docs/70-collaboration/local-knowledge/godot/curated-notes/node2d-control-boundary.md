# Node2D and Control Boundary

## Rule

`Node2D` is for world-space presentation. `Control` is for screen-space UI. Mixing them is allowed by the engine but should be treated as an architecture risk unless the intent is explicit and documented.

## Common failure modes

- A `Control` attached under a world `Node2D` appears in the wrong place or is affected by camera/world transforms.
- UI participates visually with map/unit layers instead of the UI canvas.
- Full-screen anchors do not behave as expected because the parent is not a full-screen `Control`.
- Hit tests use screen coordinates against a node whose transform is not in the expected canvas space.

## Project rule

World-site HUD, exploration HUD, dialogs, action bars, and performance overlays belong under `CanvasLayer`. Unit markers, alert-range outlines, path highlights, and map objects belong under world/map presentation roots.

## Official docs

- `../../../../.codex/external/godot-docs-4.5/classes/class_node2d.rst`
- `../../../../.codex/external/godot-docs-4.5/classes/class_control.rst`
- `../../../../.codex/external/godot-docs-4.5/classes/class_canvasitem.rst`
- `../../../../.codex/external/godot-docs-4.5/classes/class_canvaslayer.rst`
