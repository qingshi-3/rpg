# Strategic World Initial State Authoring

This document records where designers configure the starting units for the strategic world.

## Resource

Initial site garrisons are authored in:

```text
res://assets/definitions/world/strategic_world_v1_initial_state.tres
```

Open this resource in Godot Inspector. Each `Sites` entry maps one `WorldSite` to its starting garrison:

- `SiteId`: site id, such as `player_camp` or `bonefield`.
- `InitialGarrison`: starting unit stacks inside that site.
- `UnitDefinition`: pick a battle unit resource, such as `res://assets/battle/units/莱昂纳王国/f1_宗师Zir/unit.tres`.
- `Count`: stack count.
- `Morale`: starting morale.

`UnitTypeIdOverride` is only for migration or debugging. Normal authoring should select `UnitDefinition`, not type unit ids by hand.

## Runtime Rule

`StrategicWorldV1DefinitionFactory` loads this resource and converts it into `WorldSiteDefinition.InitialGarrison` before `StrategicWorldService.CreateInitialState()` creates runtime state.

Existing saves remain authoritative. After changing this resource, use the strategic-world reset button or clear `user://saves/strategic_world_v1.json` to see the new starting units in a fresh run.

## Future Migration

When content volume grows, Excel or another table source can generate this same resource/definition shape. Runtime code should continue consuming `WorldSiteDefinition.InitialGarrison`, not direct spreadsheet rows.
