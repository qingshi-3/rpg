# Scene Organization Rules

## Resource-first rule

Use authored scenes and resources for UI, reusable map markers, themes, style boxes, shaders, and repeated controls. Runtime code should load resources, bind named nodes, and refresh state.

## Root node rule

Choose root node type by responsibility:

- Full-screen HUD: `Control` under `CanvasLayer`.
- World actor or map visual: `Node2D` or an authored actor scene.
- Repeated UI row/button: reusable `Control` scene.

## Binding rule

When code depends on a scene path, the node path is a contract. If the scene structure changes, update binding code and add low-noise logs for missing nodes.

## Avoid

- Constructing complex UI trees in gameplay code.
- Duplicating authoritative state in scene nodes.
- Using fallback node creation to hide broken authored scene paths.

## Official docs

- `../../../../.codex/external/godot-docs-4.5/tutorials/best_practices/scene_organization.rst`
- `../../../../.codex/external/godot-docs-4.5/tutorials/best_practices/`
