# Battle Command Architecture

Status: Accepted Architecture

## Gameplay Authority

This document supports hero-led light RTS combat by defining how player intent becomes runtime orders without turning combat into high-frequency single-unit micro.

## Responsibility

The command system owns:

- hero command, corps command, and combined command channels;
- command request shape and validation boundary;
- conversion from accepted player intent to runtime order;
- command state attribution in runtime events and reports;
- command failure semantics.

## Does Not Own

The command system does not own:

- UI selection state or button layout;
- pathfinding execution;
- AI movement legality;
- long-term state mutation;
- settlement or report generation.

## Command Lifecycle

```text
Create Intent
-> Client-Side Basic Check
-> Submit Command
-> Application Validation
-> Runtime Acceptance / Rejection
-> Execution
-> Interrupt / Complete / Fail
-> UI Feedback
-> EventStream Attribution
```

## Layer Rules

- UI creates command intent and performs only basic availability hints such as selected battle group and disabled buttons.
- Application validates battle existence, player ownership, battle-group availability, and whether the requested command channel matches hero/corps/combined authority.
- Runtime validates battle-context facts such as whether the target still exists, is reachable, or can be affected.
- Runtime consumes accepted orders at the next valid actor decision boundary, or at an explicit interrupt boundary if the command is allowed to interrupt the current action.
- Commands must not force pathfinding, target reacquisition, or attacks while the actor is still locked in movement, attack recovery, casting, or another non-decision phase.
- Accepted runtime commands must become traceable states: `accepted`, `rejected`, `interrupted`, `completed`, `failed`.
- Commands do not directly mutate long-term state.

## Command Channels

| Channel | Purpose |
|---|---|
| Hero command | Hero skill, priority target, retreat, protect, or hero-specific tactical intent. |
| Corps command | Corps posture, attack/hold/regroup/retreat, formation or pressure behavior. |
| Combined command | Battle-group-level intent that coordinates hero and corps behavior. |

The first implementation may expose only a small subset of commands, but it must preserve the channel boundary so later hero/corps differentiation does not become a UI-only distinction.

## Event Rules

- UI-local invalid input does not enter `BattleEventStream`; it only produces UI feedback.
- Application rejection defaults to diagnostics, not battle events.
- Runtime rejection enters `BattleEventStream` because it explains battle state.
- Command accepted, interrupted, superseded, completed, or failed events must enter `BattleEventStream`.
- Runtime events should carry source command identity when the command materially influenced movement, target choice, attack, retreat, failure, or outcome.

## Inputs

- selected battle group and command channel from Presentation;
- `CommandRequest`;
- Application battle and ownership context;
- Runtime battle-context facts.

## Outputs

- `CommandValidationResult`;
- accepted `RuntimeOrder`;
- command-related runtime events and diagnostics.

## Failure Rules

- Invalid UI input stays local to UI feedback.
- Application rejection must state why the command could not be submitted.
- Runtime rejection or failure must enter battle facts when it affects battle explanation.
- Command supersession must be explicit so reports do not attribute an outcome to an obsolete order.

## Acceptance

This architecture is acceptable when:

- future command UI can route hero, corps, and combined commands without changing runtime ownership;
- command validation is split between UI hints, Application permission checks, and Runtime battle-context validation;
- command state transitions are visible to runtime diagnostics and report attribution;
- accepted commands respect Runtime actor state-machine action locks and decision boundaries;
- commands influence Runtime first, and long-term state changes wait for Settlement.
