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
- Production Presentation submits every live-battle and tactical-pause command through one focused Application submission boundary; Presentation does not call Runtime directly as its authorization boundary.
- Application validates the active battle identity, player-faction ownership, deployed battle-group availability, source actor identity and kind, and whether the requested hero/corps/combined channel and skill are authorized by the compiled battle snapshot. Request fields cannot grant skill or channel eligibility.
- Application rejection returns typed feedback and diagnostics without calling Runtime, emitting accepted runtime events, consuming costs or cooldowns, or mutating command or tactical state.
- Runtime validates battle-context facts such as whether the target still exists, is reachable, or can be affected. It also defensively revalidates battle, group, source, player ownership, and snapshot-authored skill/channel identity so forged or stale requests fail even when Runtime is called outside Presentation.
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

Commands address the battle-group commander identity. A command may later fan out into hero and corps actor intents, but it must not be stored separately on expanded force-count actors as if each count row were an independent commandable battle group. Runtime actor commands are execution facts derived from the accepted group command.

## Battle-Group Plan Relationship

`BattleGroupPlan` is the initial command-scoped intent created before battle start. It is not a UI-owned command queue and it is not a precomputed runtime script.

A plan may carry:

- battle-group identity;
- hero and corps deployment facts;
- initial formation or lane alignment;
- initial destination beacon chosen during battle preparation;
- optional future planning metadata when a later accepted mode reintroduces pre-battle objectives or postures.

When Runtime starts, deployed player groups enter the default attack posture without requiring objective-zone or engagement-rule choices. If battle preparation supplied an initial destination beacon, Runtime seeds the battle group's active player movement intent from that beacon before the first movement decision. Later accepted hero, corps, or combined commands, including destination beacon commands, produce or replace player-sourced tactical intent. Supersession must be explicit so runtime events and reports can distinguish:

- followed default attack behavior;
- destination changed by player beacon command;
- command superseded by retreat, protect, hold, regroup, target loss, or failure.

Commands must preserve state-machine action locks. A command or future planning change may request an interrupt only through an accepted interrupt rule; it must not force pathfinding, target reacquisition, or attacks while the actor is locked in movement, attack recovery, casting, or another non-decision phase.

The battle-group commander state is the runtime owner of active command scope, tactical intent state, local-combat assignment, and regroup or retreat intent. Actor action state machines consume derived execution intents and report success, failure, or interruption back through runtime events. `BattleGroupPlan` remains an upstream battle-preparation input DTO; after battle entry it must not remain a separate player-only movement authority.

## Destination Beacon Command Contracts

A destination beacon command is the default movement command for player battle groups. It may be seeded from battle preparation before the battle clock starts, or submitted through live battle or tactical-pause input after Runtime is active.

The concrete mouse gesture is Presentation-owned. Battle preparation may use a focused left-click destination-targeting state to seed the initial beacon, while live battle or tactical pause may use a different command gesture. Both routes must submit the same command payload shape and validation boundary.

The command payload must carry:

- selected battle-group ids;
- command channel, normally combined command unless a later hero/corps split is accepted;
- requested destination anchor and height;
- command source and revision;
- optional existing beacon id when moving/replacing a selected group's beacon.

Presentation may create local hover previews and invalid-input feedback, but it must not commit a runtime beacon by itself. Application or Bridge validation checks ownership, selected deployed battle groups, preparation or tactical-pause allowance, and basic destination availability. Runtime acceptance revalidates battle-context facts, topology version, footprint/passability profiles for every selected group, and whether a beacon flow field can reach the destination.

The first shared-beacon command is atomic for multi-selection. If any selected battle group cannot reach the destination under its required static topology and footprint profile, Runtime rejects the command for all selected groups and leaves their previous destinations unchanged. A later accepted command design may add explicit partial acceptance with clear per-group feedback.

An accepted beacon command creates or moves one destination beacon shared by the selected battle groups. Non-selected groups do not change command scope. Reissuing a beacon command for a group supersedes that group's previous player destination at the next valid command boundary while preserving actor action locks.

Commands issued during preparation or tactical pause may place or update beacons and command facts, but they do not advance movement, attacks, cooldowns, perception, flow-field consumption, or battle time until Runtime is active and unpaused.

## Skill Command Contracts

Hero and later corps active-skill commands carry intent, not immediate effects. A skill command must name the skill definition id, command channel, source battle group or actor, and targeting payload required by the definition.

Skill command payloads preserve the definition's player-directed targeting contract:

- unit target: the player selects a valid Runtime actor;
- cell target: the player selects a legal grid anchor;
- direction target: the player selects a direction under the definition's snap or arc rules;
- area target: the player places or orients a definition-owned area shape;
- multi-stage target: the player completes each required selection before one complete command is submitted;
- self-centered or explicitly automatic target: no player target is required only when the accepted definition says so.

Internal code may group these forms into targeted and non-targeted payload families, but that grouping must not collapse cell, direction, area, or multi-stage player input into automatic target selection and release. Automatic targeting is definition-specific, not the default identity of player-cast skills.

Runtime acceptance of a targeted skill command checks that the target exists, is targetable for that skill, and is in range at acceptance time. Default active-skill range is footprint-aware Manhattan distance between caster and target footprints, so the player sees and submits a diamond-shaped range on the square grid. Acceptance locks the target identity for later execution. If the locked target moves out of range before the actor can release the skill, the order still resolves against that target. If the target dies or becomes invalid before release, the order fails instead of retargeting or firing at empty space.

Some spatial skills require multi-stage targeting before a command exists. Thunder Mark Fold is the first accepted example: Presentation first selects a live owned mark, then selects an empty legal landing anchor within the mark's content-defined landing radius. The submitted command must carry both the selected mark identity or resolved mark reference and the requested landing anchor. Runtime acceptance revalidates the mark, the landing radius, topology legality, footprint placement, and dynamic occupancy before accepting the command. UI-local clicks that do not select a live mark or legal landing anchor remain local feedback and must not enter the runtime event stream as accepted skill commands.

By default, accepted active-skill orders may interrupt a basic attack before that attack reaches its damage impact. They do not cancel basic attack recovery after impact, and they do not interrupt another active skill. Recovery canceling, skill-to-skill interruption, instant release, and offhand or fire-and-forget release require explicit interrupt traits from accepted definitions or modifiers.

Commands issued during tactical pause may be accepted, rejected, superseded, or queued as command facts, but they do not advance cast time, damage, cooldown, or other live battle state until Runtime resumes.

For active skills, supersession is scoped to the selected caster. If the caster is at a decision boundary or otherwise not already casting or recovering from an active skill, a newly accepted active-skill command replaces that caster's older unstarted pending skill command and emits a command supersession/interruption event. If the caster is already casting or recovering from an active skill, the newly accepted command remains queued behind the active skill and cannot interrupt it unless an explicit skill trait allows that later.

## Event Rules

- UI-local invalid input does not enter `BattleEventStream`; it only produces UI feedback.
- Application rejection defaults to diagnostics, not battle events.
- Runtime rejection enters `BattleEventStream` because it explains battle state.
- Command accepted, interrupted, superseded, completed, or failed events must enter `BattleEventStream`.
- Destination beacon accepted, moved, superseded, rejected, unreachable, or invalidated events must carry beacon id, command id, selected battle-group ids, destination anchor, and reason code.
- Runtime events should carry source command identity when the command materially influenced movement, target choice, attack, retreat, failure, or outcome.

## Inputs

- selected battle group or multi-selected battle groups and command channel from Presentation;
- accepted deployment, formation, and initial destination-beacon facts from battle preparation;
- `CommandRequest`;
- destination beacon command payloads;
- Application battle and ownership context;
- Runtime battle-context facts.

## Outputs

- `CommandValidationResult`;
- accepted `RuntimeOrder`;
- player-sourced tactical intent, destination beacon runtime facts, or command-supersession runtime facts;
- command-related runtime events and diagnostics.

## Failure Rules

- Invalid UI input stays local to UI feedback.
- Application rejection must state why the command could not be submitted.
- Runtime rejection or failure must enter battle facts when it affects battle explanation.
- Command supersession must be explicit so reports do not attribute an outcome to an obsolete order.
- Shared destination beacon commands fail atomically in the first version when any selected battle group cannot reach the destination under static topology and footprint/profile validation.

## Acceptance

This architecture is acceptable when:

- future command UI can route hero, corps, and combined commands without changing runtime ownership;
- expanded runtime actors remain under one command-owned battle-group commander unless they are explicitly separate commandable battle groups;
- deployment plans can initialize placement, formation, and initial destination beacons without becoming UI-owned combat truth;
- destination beacon commands initialize or replace player-sourced tactical intent instead of a separate player-only movement path;
- command validation is split between UI hints, Application permission checks, and Runtime battle-context validation;
- command state transitions are visible to runtime diagnostics and report attribution;
- accepted commands respect Runtime actor state-machine action locks and decision boundaries;
- commands influence Runtime first, and long-term state changes wait for Settlement.
- player-cast skill commands preserve definition-required unit, cell, direction, area, and multi-stage choices instead of defaulting to automatic target-and-release.
