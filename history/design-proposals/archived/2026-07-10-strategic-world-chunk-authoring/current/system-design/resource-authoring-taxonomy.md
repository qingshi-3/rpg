# Resource Authoring Taxonomy

Status: Accepted Architecture

## Gameplay Authority

This document supports the accepted hero-led light RTS and strategic-management direction by making repository resource ownership explicit. It is an implementation-facing taxonomy; it does not change player-facing gameplay.

## Responsibility

This document owns repository-level directory boundaries for:

- raw imported media and source-like asset packages;
- Godot-authored Resource files;
- PackedScene authoring;
- source code and narrow plugin adapters;
- plain text indexes and mappings;
- resource-migration and exception rules for existing mixed directories.

## Does Not Own

This document does not own:

- individual content balance;
- unit, skill, or UI visual quality;
- scene tree layout contracts beyond top-level directory ownership;
- runtime loading policy beyond path authority and failure expectations;
- external asset-library organization.

## Directory Ownership

```text
assets/     raw imported media and source-like asset packages
resource/   Godot-authored Resource files and reusable data resources
scenes/     PackedScene authoring
src/        source code and narrow engine/plugin adapters
config/     plain text indexes and mappings
```

### `assets/`

`assets/` owns raw or source-like media that can be imported by Godot or used to build authored resources:

- textures such as PNG and SVG;
- audio such as OGG and WAV;
- source animation package files such as PLIST;
- fonts and other raw media;
- `.import` sidecar files for imported media;
- external-pack structure retained for source traceability.

`assets/` should not be the long-term home for newly authored gameplay definitions, behavior trees, themes, styleboxes, tilesets, shaders, or business-facing data resources.

Temporary exception: `frames.tres` may remain beside unit and VFX PNG/PLIST source files under `assets/` while visual preview workflows depend on that proximity. This exception is for preview and content-pipeline convenience only; it does not make `assets/` the general authored-resource root.

### `resource/`

`resource/` owns Godot-authored reusable resources:

- typed gameplay/content definitions such as battle units, skills, heroes, corps, encounters, and future authored data;
- behavior trees and blackboard plans;
- `Theme`, `StyleBoxTexture`, `AtlasTexture`, and other reusable UI resources;
- `TileSet` resources;
- `.gdshader` shader files;
- reusable material or presentation resources that are not source media.

Current top-level shape:

```text
resource/
  battle/
    ai/
    skills/
    units/
  ui/
    themes/
    icons/
  tilesets/
  shaders/
```

The exact subsystem folders may evolve through focused implementation proposals, but new authored resources should default to `resource/` unless an accepted authority document defines a narrower location.

### `scenes/`

`scenes/` owns `.tscn` PackedScene authoring. Scenes remain separate from `resource/` even though PackedScenes are Godot resources in engine terms. This keeps scene-tree ownership distinct from data-resource ownership.

### `src/`

`src/` owns source code. C# files live here. GDScript files may live here only when they are source-code adapters or implementation code, such as narrow plugin bridge tasks that call C# facades.

GDScript adapter placement must follow system ownership. For example, LimboAI custom tasks that only bridge behavior-tree blackboards to C# facades are source adapters, not raw assets and not UI presentation resources.

### `config/`

`config/` owns plain text indexes and mappings such as JSON. Config files may reference resource ids and `res://resource/...` paths. They do not contain Godot-authored Resource objects, imported media, scenes, themes, shaders, or SpriteFrames.

## Migration Principles

- Move one resource family per batch.
- Preserve `.uid` sidecars when moving source-backed scripts or resources that have them.
- Update all reference surfaces in the same batch: `.tscn`, `.tres`, `.json`, C# constants, tests, and documentation.
- Run static reference searches before and after each batch.
- Prefer low-risk resource families before high-volume content libraries.
- Keep `frames.tres` in place until a later accepted proposal changes the visual-content pipeline.
- Do not use compatibility duplicate files as a hidden fallback. If a path changes, update the authoritative reference and fail explicitly on stale paths.

## Current Migrated Families

The completed `resource/` migration makes these families resource-rooted:

- battle behavior-tree resources under `resource/battle/ai/`;
- battle skill definition resources under `resource/battle/skills/`;
- battle unit `unit.tres` and `visual.tres` definitions under `resource/battle/units/`;
- UI theme/stylebox resources under `resource/ui/themes/`;
- reusable AtlasTexture UI icons under `resource/ui/icons/`;
- TileSet resources under `resource/tilesets/`;
- shader resources under `resource/shaders/`.

Known remaining exceptions under `assets/` are raw/source media, `.import` sidecars, source package files, unit `frames.tres` preview resources, and the current unit audio profiles. New authored resource families should not treat those exceptions as precedent.

## Failure Rules

- A moved resource path with stale `res://assets/...` references fails the batch.
- A scene or config file that references a moved resource path must be updated in the same batch.
- Runtime or Presentation code must not silently fall back from `resource/` to old `assets/` paths for moved authored resources.
- Migration scripts, if used, must operate on explicit batch scopes and must not rewrite raw media paths.

## Acceptance

This taxonomy is acceptable when:

- future agents can identify whether a file belongs in `assets/`, `resource/`, `scenes/`, `src/`, or `config/`;
- raw media remains convenient for preview and import workflows;
- authored Godot resources have a stable root outside `assets/`;
- future scene/resource path moves happen only through staged, verified batches;
- `frames.tres` remains an explicit temporary exception instead of an accidental precedent.
