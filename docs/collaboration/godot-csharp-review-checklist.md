# Godot C# Review Checklist

Use this checklist for Godot C# changes that affect gameplay runtime, scenes, UI,
or editor-authored content. It is adapted from the external game-studio workflow
reference and narrowed to this project.

## Scene Contracts

- `.tscn` paths point to current project structure.
- Exported `NodePath` values match actual scene nodes.
- Optional nodes use `GetNodeOrNull` and log clear warnings.
- Required nodes fail loudly enough for debugging.
- New scene nodes do not duplicate old prototype/runtime responsibilities.
- Scene names distinguish strategic world, site runtime, site maps, and battle
  runtime clearly.

## Godot Lifecycle

- `_Ready` resolves nodes before runtime services use them.
- `_Process` does only continuous work that must happen every frame.
- Scene changes go through existing handoff services.
- Signals are connected once and do not leak duplicate handlers.
- Runtime-created nodes have stable names when they matter for debugging.

## C# Runtime Quality

- Domain state is serializable when it needs to survive save/load.
- Godot structs such as `Vector2` are not the only persisted representation when
  JSON stability matters.
- Public service methods return result objects instead of mutating UI directly.
- Null checks are explicit around definitions, state dictionaries, and node
  lookups.
- Per-frame paths avoid unnecessary LINQ over large collections when the data can
  grow.
- Logs are state-transition oriented, not per-frame spam.

## Project Boundaries

- Strategic-world movement uses continuous `Vector2` world coordinates.
- Battle movement remains inside battle grid systems.
- World-to-battle handoff uses request/result objects.
- Battle result writeback does not reach into presentation nodes.
- New content behavior prefers definitions/effects/conditions over hardcoded
  action branches.

## Review Output

For a code review, report findings first:

- Severity and concrete file/line reference.
- Why it can break behavior, data, save/load, or authoring.
- Minimal fix direction.

If no issues are found, say so and list remaining test gaps.

