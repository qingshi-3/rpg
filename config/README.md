# Config

This directory stores gameplay configuration indexes and mappings.

Config files may reference resource ids and `res://` resource paths, but they do not contain Godot-authored resources, imported art, scenes, themes, shaders, SpriteFrames, or `.tres` resource bodies. Actual authored resources remain under `assets/`, `scenes/`, and other resource directories.

Current entries:

- `battle/first_slice_hero_companies.json`: first-slice hero-company, default corps, skill, and Bonefield roster mappings.
- `battle/unit_definition_index.json`: curated battle unit id to authored `unit.tres` resource path index.
- `strategic_management/economy/resources.json`: foundation Strategic Management resource definitions.
- `strategic_management/economy/conscription_policies.json`: foundation reserve-soldier conscription policies.
- `strategic_management/cities/buildings_foundation.json`: foundation city building definitions.
- `strategic_management/military/corps_common.json`: foundation common corps definitions.
- `world/strategic_world_v1_initial_state.json`: strategic initial site roster data.
