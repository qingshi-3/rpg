# Technical Slice

## Goal

Implement the v0.1 player flow while staying aligned with the hero-led battle group architecture.

## Systems

### World Expedition Entry

- Add or wire a `出征` action on the world UI.
- Store the selected expedition hero in a short-lived world/session state object.
- Log selection and cancellation.

### Expedition Targeting

- When an expedition is active, right-clicking an enemy strategic location should create an assault world-army order. It must not create an immediate battle preparation request.
- Battle preparation begins from the arrived assault army flow after world travel completes.
- Non-enemy or invalid targets should fail explicitly with a low-noise log and no hidden fallback.

### V0 Roster Definitions

- Define the first playable hero, default corps, and enemy from existing `BattleUnitDefinition` resources.
- Keep this data resource-driven where practical.
- Required first resources:
  - `res://assets/battle/units/f1_将军/unit.tres`
  - `res://assets/battle/units/f1_近战兵/unit.tres`
  - `res://assets/battle/units/首领_城域守卫/unit.tres`

### Deployment Preparation

- Introduce a preparation state before live combat starts.
- Show player deployment zone data.
- Carry selected hero/corps/enemy facts into the battle start payload.
- Reuse existing deployment/runtime services where possible instead of building a second battle authority path.

### Site Battle Runtime Pool

- Follow the accepted data-layer split from the hero-led light-RTS architecture:
  - persistent Domain state is the saveable long-term authority;
  - battle Runtime state is the active in-memory authority during combat;
  - snapshots/results/settlement are the only bridge between them.
- Before an assault battle starts, Application builds one stable battle start snapshot from the arriving player army and target strategic location state.
- The runtime unit pool is created once from that snapshot. Deployment, combat, casualties, retreat, and victory/defeat decisions read and mutate that runtime pool, not ad hoc reconstructed request lists.
- `BattleStartRequest` and current `WorldSiteUnitPlacement` rows are migration adapters/presentation data only. They must not become competing roster authorities.
- No battle may mutate persistent Domain state before settlement. At the settlement boundary:
  - player victory writes surviving player units to the captured location and removes defeated defender units;
  - player defeat/retreat writes surviving defender units to the location and removes/returns surviving player units according to retreat rules;
  - all long-term writes are traceable to runtime result/event facts.
- `WorldArmyState` remains the world-travel carrier. After battle settlement it must not remain a second authoritative roster for units already written to persistent Domain state.

### Battle Start

- `开战` commits deployment and starts real-time battle.
- Light RTS command UI remains out of scope, but the start payload should not block adding it later.

## Test Targets

- Hero selection creates an expedition state.
- Cancelling selection clears the expedition state.
- Right-clicking an enemy strategic location with an expedition active creates an assault army order without immediately entering battle preparation.
- The arrived assault army flow creates the battle preparation request.
- The preparation request includes the selected hero, default corps, enemy resource, and deployment side.
- `开战` starts battle from preparation.
- Regression tests assert that v0.1 resources are authored unit definitions, not placeholder-only markers.
- Regression tests assert that the assault battle request does not mirror player units into enemy forces.
- Regression tests assert that resolved assault battles leave only the correct surviving side in the site-local pool.

## Risk Controls

- Do not remove legacy battle handoff unless the new path fully covers the runtime entry in this slice.
- Do not add a second long-term battle model. Migration bridges must be explicit adapters into the accepted snapshot/runtime/settlement architecture, not new business authorities.
- Keep all player-visible labels in Chinese.
