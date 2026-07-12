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

Commands address the battle-group commander identity. A command may later fan out into hero and corps actor intents, but it must not be stored separately on expanded force-count actors as if each count row were an independent commandable company. Runtime actor commands are execution facts derived from the accepted group command.

## Battle-Group Plan Relationship

`BattleGroupPlan` is the initial command-scoped intent created before battle start. It is not a UI-owned command queue and it is not a precomputed runtime script.

A plan may carry:

- battle-group identity;
- hero and corps deployment facts;
- selected objective-zone id;
- initial formation or lane alignment;
- engagement rule such as fire-on-the-move, move-first, attack-first, hold, retreat-first, or protect-hero.

When Runtime starts, the active plan behaves like the default combined-command scope for that battle group. Later accepted hero, corps, or combined commands may supersede part or all of that scope. Supersession must be explicit so runtime events and reports can distinguish:

- followed original plan;
- objective changed by player command;
- engagement rule changed by command;
- plan interrupted by retreat, protect, hold, target loss, or failure.

Commands must preserve state-machine action locks. A command or plan change may request an interrupt only through an accepted interrupt rule; it must not force pathfinding, target reacquisition, or attacks while the actor is locked in movement, attack recovery, casting, or another non-decision phase.

The battle-group commander state is the runtime owner of active command scope, plan state, local-combat assignment, and regroup or retreat intent. Actor action state machines consume derived execution intents and report success, failure, or interruption back through runtime events.

## Skill Command Contracts

Hero and later corps active-skill commands carry intent, not immediate effects. A skill command must name the skill definition id, command channel, source battle group or actor, and targeting payload required by the definition.

Skill commands use two default targeting forms:

- Targeted skill command: requires a selected runtime actor target before Runtime accepts the command.
- Non-targeted skill command: requires only the definition-owned payload, such as self, cell, direction, or no explicit target.

Runtime acceptance of a targeted skill command checks that the target exists, is targetable for that skill, and is in range at acceptance time. Acceptance locks the target identity for later execution. If the locked target moves out of range before the actor can release the skill, the order still resolves against that target. If the target dies or becomes invalid before release, the order fails instead of retargeting or firing at empty space.

By default, accepted active-skill orders may interrupt a basic attack before that attack reaches its damage impact. They do not cancel basic attack recovery after impact, and they do not interrupt another active skill. Recovery canceling, skill-to-skill interruption, instant release, and offhand or fire-and-forget release require explicit interrupt traits from accepted definitions or modifiers.

Commands issued during tactical pause may be accepted, rejected, superseded, or queued as command facts, but they do not advance cast time, damage, cooldown, or other live battle state until Runtime resumes.

## Event Rules

- UI-local invalid input does not enter `BattleEventStream`; it only produces UI feedback.
- Application rejection defaults to diagnostics, not battle events.
- Runtime rejection enters `BattleEventStream` because it explains battle state.
- Command accepted, interrupted, superseded, completed, or failed events must enter `BattleEventStream`.
- Runtime events should carry source command identity when the command materially influenced movement, target choice, attack, retreat, failure, or outcome.

## Inputs

- selected battle group and command channel from Presentation;
- accepted `BattleGroupPlan` values from battle preparation;
- `CommandRequest`;
- Application battle and ownership context;
- Runtime battle-context facts.

## Outputs

- `CommandValidationResult`;
- accepted `RuntimeOrder`;
- active plan or plan-supersession runtime facts;
- command-related runtime events and diagnostics.

## Failure Rules

- Invalid UI input stays local to UI feedback.
- Application rejection must state why the command could not be submitted.
- Runtime rejection or failure must enter battle facts when it affects battle explanation.
- Command supersession must be explicit so reports do not attribute an outcome to an obsolete order.

## Acceptance

This architecture is acceptable when:

- future command UI can route hero, corps, and combined commands without changing runtime ownership;
- expanded runtime actors remain under one command-owned battle-group commander unless they are explicitly separate commandable battle groups;
- battle-start plans can initialize command-scoped runtime intent without becoming UI-owned combat truth;
- command validation is split between UI hints, Application permission checks, and Runtime battle-context validation;
- command state transitions are visible to runtime diagnostics and report attribution;
- accepted commands respect Runtime actor state-machine action locks and decision boundaries;
- commands influence Runtime first, and long-term state changes wait for Settlement.
