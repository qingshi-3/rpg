# Resource Directory Taxonomy Proposal

Status: Archived

## Relationship Metadata

- Requirement Id: RES-TAX-001
- Parent Proposal: None
- Supersedes: None
- Superseded By: None
- Amends: None
- Amended By: None
- Affected Authority Documents:
  - `system-design/resource-authoring-taxonomy.md`
  - `system-design/battle-content-progression-architecture.md`
  - `system-design/README.md`
- Related Implementation Proposals:
  - `gameplay-alignment/implementation-proposals/2026-07-05-resource-directory-migration.md`

## Current Architecture

The current repository mixes multiple Godot authoring roles under `assets/`. Raw imported media, generated import sidecars, Godot-authored content definitions, behavior trees, themes, styleboxes, shaders, tilesets, and atlas textures can all live under the same root.

This makes future resource migration fragile. A later agent can interpret `assets/` as both a raw media library and a gameplay-resource authority, then add new scenes, `.tres` definitions, themes, or behavior trees there. It also creates hidden coupling because resource paths are referenced from scene files, config JSON, C# constants, tests, and documentation.

`system-design/battle-content-progression-architecture.md` currently says `assets/` owns actual Godot resources such as battle unit definitions, visuals, audio, animation sets, and ability effects. That accepted wording now conflicts with the desired directory boundary.

## Expected Architecture

Repository directory ownership should become explicit:

```text
assets/     raw imported media and source-like asset packages
resource/   Godot-authored Resource files, shaders, themes, tilesets, behavior trees, and data resources
scenes/     PackedScene authoring
src/        source code and narrow engine/plugin adapters
config/     plain text indexes and mappings
```

`assets/` should not be the default home for authored `.tres`, `.gdshader`, `Theme`, `StyleBox`, `TileSet`, or behavior-tree resources.

`frames.tres` is a temporary exception. SpriteFrames generated for unit and VFX preview packages may remain beside their source PNG/PLIST files under `assets/` until the visual-content pipeline is mature enough to separate preview convenience from authored resource ownership.

The first implementation should be staged and reversible. It must start with documentation and reference inventory, then migrate small, low-risk resource families before touching high-volume unit resources.

## Non-Goals

- Do not move resources in the design proposal.
- Do not migrate `frames.tres` in the first migration plan.
- Do not move `.tscn` scenes into `resource/`.
- Do not move raw textures, audio, PLIST files, SVGs, fonts, or `.import` sidecars out of `assets/`.
- Do not move all `unit.tres` and `visual.tres` resources in the first migration batch.
- Do not rely on Godot editor auto-rewrite alone; resource path references must be statically audited.

## Acceptance Criteria

- A repository-wide authority document defines `assets/`, `resource/`, `scenes/`, `src/`, and `config/` responsibilities.
- Battle content architecture no longer states that `assets/` owns authored battle definitions.
- The temporary `frames.tres` exception is explicit and bounded.
- Implementation may proceed only through a focused migration proposal that lists batches, touched files, verification commands, and stop conditions.
