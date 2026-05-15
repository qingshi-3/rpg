# Input and Mouse Filter Rules

## Control mouse filters

Use `MouseFilter.Stop` only on controls that should consume pointer input, such as buttons and active panels. Use `MouseFilter.Ignore` for full-screen layout roots and purely visual containers when map input should pass through.

## Do not confuse visibility with hit ownership

A visible full-screen UI root is not necessarily an input blocker. Hit tests must use the actual interactive panel or button rect. If a full-screen root is used for hit testing, it will block the entire map.

## Event routing rule

Map input should first give explicit UI buttons a chance to handle clicks. Then it may reject clicks over visible UI panels. Only after that should it interpret the click as a world/map action.

## Diagnostics

When debugging UI input, log these separately:

- HUD scene instantiated and parent path.
- Button nodes found.
- Button `Pressed` or fallback dispatch fired.
- State gate accepted or rejected the action.

This separates scene/layer bugs from signal bugs and state-machine bugs.

## Official docs

- `../../../../.codex/external/godot-docs-4.5/classes/class_control.rst`
- `../../../../.codex/external/godot-docs-4.5/tutorials/inputs/`
