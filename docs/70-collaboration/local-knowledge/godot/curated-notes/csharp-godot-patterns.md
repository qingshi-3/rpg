# Godot C# Patterns

## Node lifecycle

Bind nodes in `_Ready` or immediately after instancing an authored scene. Treat missing required nodes as a scene contract error; log clearly and avoid silent fallback UI construction.

## Signals

Connect C# event handlers once per instantiated scene. If a node can be recreated, clear references when it is freed and reconnect on the new instance.

## Object validity

Before writing to Godot objects from tween callbacks, delayed calls, or async-style callbacks, check that the object is still valid and inside the tree when appropriate.

## Scene paths

Use constants for resource paths that are architectural contracts. Keep those constants near the owning system and update them with scene changes.

## Input

Separate UI input handling from map input handling. When a full-screen HUD exists, use the concrete panel/button rect for blocking map input, not the full-screen root.

## Official docs

- `../../../../.codex/external/godot-docs-4.5/tutorials/scripting/c_sharp/`
- `../../../../.codex/external/godot-docs-4.5/classes/`
