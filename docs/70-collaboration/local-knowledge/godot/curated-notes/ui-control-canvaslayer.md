# UI, Control, and CanvasLayer Rules

## Core boundary

Screen-space UI belongs under a `CanvasLayer` and should be built with `Control` nodes. World/map/unit presentation belongs in `Node2D` or map layers. Do not attach HUD controls to world `Node2D` roots where camera transforms, Y-sort, and world ordering can affect them.

## Layout rule

Use a full-screen `Control` root for HUD scenes. Place panels, bars, dialogs, and buttons as children with anchors/containers. Do not make the scene root a small floating panel unless the parent layout is explicitly designed for that.

## Input rule

A full-screen HUD root should usually be `MouseFilter.Ignore`. Actual interactive controls should use `Stop` or default button behavior. For map-click filtering, test the visible panel/button rect, not the full-screen HUD root.

## Project application

For world-site exploration, the root HUD scene is full-screen under the site UI canvas. The bottom exploration panel is the only area that should block map input. Map cells outside the panel must remain clickable.

## Official docs

- `../../../../.codex/external/godot-docs-4.5/classes/class_control.rst`
- `../../../../.codex/external/godot-docs-4.5/classes/class_canvaslayer.rst`
- `../../../../.codex/external/godot-docs-4.5/tutorials/ui/`
- `../../../../.codex/external/godot-docs-4.5/tutorials/2d/canvas_layers.rst`
