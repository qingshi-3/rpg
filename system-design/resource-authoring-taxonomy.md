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
- repository-local development and authoring tools;
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
tools/      repository-local development and authoring tools
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

Strategic-world reference rasters, final visual chunks, and generated terrain or territory masks are media and therefore belong under focused paths within `assets/textures/world/`. Final chunk art remains separate from reference images and masks. None of these pixels may become navigation, city ownership, fog, hover state, or another mutable strategic fact.

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

The exact subsystem folders may evolve after confirmed discussion and a corresponding authority update, but new authored resources should default to `resource/` unless an accepted authority document defines a narrower location.

Godot-authored strategic-world runtime definitions belong under `resource/world/` when introduced. `StrategicMap` resources use a focused `resource/world/strategic_map/` subtree without moving or duplicating canonical geography. They may reference the canonical plain-text manifest, visual media, derived masks, and navigation scenes, but they must not duplicate canonical geographic facts or contain persistent campaign state.

### `scenes/`

`scenes/` owns `.tscn` PackedScene authoring. Scenes remain separate from `resource/` even though PackedScenes are Godot resources in engine terms. This keeps scene-tree ownership distinct from data-resource ownership.

Strategic-world runtime chunk presentation scenes and per-chunk Godot navigation-authoring scenes belong under `scenes/world/`; final greenfield production scenes use a focused `scenes/world/strategic_map/` subtree when introduced. The local Web workbench is not authored as a Godot scene, and Godot navigation scenes must not become a second geographic-data editor or a hidden full-world runtime fallback.

### `src/`

`src/` owns source code. C# files live here. GDScript files may live here only when they are source-code adapters or implementation code, such as narrow plugin bridge tasks that call C# facades.

The greenfield large-world module uses final-named `StrategicMap` subdirectories under the existing layer roots, including `src/Definitions/StrategicMap/`, `src/Application/StrategicMap/`, and later `src/Presentation/StrategicMap/`. It must not create a second canonical data tree or place compatibility aliases and legacy runtime dependencies inside the new module.

GDScript adapter placement must follow system ownership. For example, LimboAI custom tasks that only bridge behavior-tree blackboards to C# facades are source adapters, not raw assets and not UI presentation resources.

### `config/`

`config/` owns plain text indexes and mappings such as JSON. Config files may reference resource ids and `res://resource/...` paths. They do not contain Godot-authored Resource objects, imported media, scenes, themes, shaders, or SpriteFrames.

Strategic-world editable sources are MapId-scoped under `config/world/maps/<MapId>/source/`, with a catalog under `config/world/maps/`. Immutable published snapshots live under `config/world/published/<MapId>/<Revision>/`, and only a small current-revision pointer may be atomically replaced. Scenario definitions live under a focused `config/strategic/scenarios/` route and reference compatible package identity. Web and Godot never edit a published snapshot, and a generated Godot resource must not silently fork source geography.

Map-scoped final visual media and generated raster artifacts live under focused `assets/textures/world/maps/<MapId>/<Revision>/` paths referenced by the published package. Visual chunks and categorical region masks retain distinct import settings. Godot-authored selection/tuning resources live under `resource/world/strategic_map/`; they may point to a generic selection document or package manifest but cannot duplicate map facts or campaign state.

### `tools/`

`tools/` owns repository-local development, validation, conversion, and authoring tools that are not shipped as gameplay runtime authority.

The strategic-world geographic workbench belongs under `tools/world-map-workbench/`. Its TypeScript front end, local Node service, schemas, narrow generation scripts, and tool-only tests remain together there. The service may read and write only explicitly configured project paths, must keep licensed assets local, and must not encode authoritative geographic data inside tool source files.

## Migration Principles

- Move one resource family per batch.
- Preserve `.uid` sidecars when moving source-backed scripts or resources that have them.
- Update all reference surfaces in the same batch: `.tscn`, `.tres`, `.json`, C# constants, tests, and documentation.
- Run static reference searches before and after each batch.
- Prefer low-risk resource families before high-volume content libraries.
- Keep `frames.tres` in place until a later confirmed discussion updates the visual-content pipeline authority.
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
- A Web-authored canonical world fact and a generated Godot representation must not both become editable authorities.
- Repository-local tools must fail visibly when an attempted read or write escapes their configured project paths.

## Acceptance

This taxonomy is acceptable when:

- future agents can identify whether a file belongs in `assets/`, `resource/`, `scenes/`, `src/`, `config/`, or `tools/`;
- raw media remains convenient for preview and import workflows;
- authored Godot resources have a stable root outside `assets/`;
- the Web workbench, canonical geographic data, derived raster artifacts, Godot navigation scenes, and runtime definitions each have one clear directory owner;
- future scene/resource path moves happen only through staged, verified batches;
- `frames.tres` remains an explicit temporary exception instead of an accidental precedent.
