# Project Architecture

The project uses Flow + Domain + Presentation Architecture.

It does not use a global MVC architecture and does not use a full ECS framework. UI may borrow presenter-style patterns, and gameplay data may borrow component-style composition, but the project-level architecture is flow-driven and definition-driven.

## Core Principle

Abstraction exists to hide implementation details, stabilize system contracts, and reduce the cost of changing concrete content.

Upper-level flow code must interact only with abstract protocols, requests, results, and definitions. It must not depend on concrete scene implementations or internal node paths.

## Layers

```text
Presentation
  Godot scenes, nodes, input, visual feedback, UI, scene adapters

Application
  Flow controllers, requests, results, use-case orchestration

Domain
  Pure gameplay rules, battle rules, world rules, state transitions

Definitions
  Resource data, content definitions, scene references, ids

Infrastructure
  Save/load, asset loading adapters, platform integration later
```

Current directories already follow this direction:

- `src/Definitions/`: data and resource definitions.
- `src/Presentation/`: Godot-facing scenes and adapters.

Future rule-heavy work should introduce `src/Application/` and `src/Domain/` before adding more logic to presentation nodes.

## Dependency Rules

- `Presentation` may depend on `Application`, `Domain`, and `Definitions`.
- `Application` may depend on `Domain` and `Definitions`.
- `Domain` should not depend on Godot scenes, UI nodes, or presentation scripts.
- `Definitions` should stay data-oriented and avoid runtime flow decisions.
- Cross-system communication should use requests and results, not direct mutation of another system's internals.

Forbidden dependencies:

- `WorldFlow` depending on concrete site scene details such as `BonefieldSite`.
- `BattleFlow` depending on a concrete UI button or a concrete map node path.
- UI directly modifying AP, TurnSystem, Intent, or battle resolution.
- Domain rules calling concrete Godot scene nodes to make gameplay decisions.

## Flow Contracts

Upper-level flow code owns orchestration, not local implementation.

World flow should speak in:

- `WorldSite`
- `WorldSiteDefinition`
- `WorldArmy`
- `Opportunity`
- `StrategicWorldState`
- site action requests
- encounter requests
- `BattleStartRequest` and `BattleResult`

Battle flow should speak in:

- `BattleAction`
- `BattleMapDefinition`
- unit, ability, rule, and encounter definitions
- `BattleContext`
- `BattleResult`

Concrete scenes implement these contracts. They should not become dependencies of the flow layer.

## Godot Scene Role

Godot scenes are presentation adapters and content containers.

They are responsible for:

- Visual composition.
- Collision and interaction nodes.
- Local scene wiring.
- Translating input or trigger signals into abstract requests.

They are not responsible for:

- Owning global progression rules.
- Owning battle rules.
- Loading unrelated concrete scenes directly.
- Mutating other systems' internal state.

## UI Pattern

UI can use a presenter-style split:

```text
View -> Command -> Flow -> State -> Presenter -> View
```

Rules:

- Views render state.
- UI input emits commands or requests.
- Flow validates and applies decisions.
- Presenters convert domain or flow state into UI-friendly display state.

Do not let UI buttons directly resolve damage, spend AP, change turns, or mutate world flags.

## ECS Position

The project may use component-style data where useful, such as statuses, abilities, rules, control modes, and presentation profiles.

The project should not adopt a full ECS framework unless there is a proven need for high-volume entity simulation that Godot scene composition cannot support.

## MVC Position

The project should not use MVC as the global architecture.

MVC-like naming can be used locally for UI when it improves clarity, but project-level systems should remain organized by flow, domain rules, definitions, and presentation adapters.
