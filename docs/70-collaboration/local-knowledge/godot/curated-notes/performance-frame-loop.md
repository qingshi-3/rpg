# Performance and Frame Loop Rules

## Per-frame discipline

Do not rebuild UI trees, unit entities, textures, pathfinding data, or fog masks every frame. `_Process` and `_PhysicsProcess` should only do cheap incremental work or call systems that are known to be cheap.

## Event-driven refresh

Prefer refreshing on state changes:

- Movement command accepted.
- Tick advanced.
- Camera transform changed.
- Visibility/intel data changed.
- UI selection changed.

## Diagnostics

Use persistent low-frequency diagnostics for FPS, frame time, memory, draw calls, node counts, and GC deltas. Avoid per-frame logs.

## Animation stability

Idle animation is a stable state. Do not restart idle because of invalid clicks, HUD refreshes, or text updates. Rebuilding entity presentation resets animation and should be reserved for actual scene/state rebuilds.

## Official docs

- `../../../../.codex/external/godot-docs-4.5/tutorials/performance/`
- `../../../../.codex/external/godot-docs-4.5/tutorials/scripting/idle_and_physics_processing.rst`
