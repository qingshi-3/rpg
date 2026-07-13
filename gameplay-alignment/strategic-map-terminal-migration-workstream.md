# StrategicMap Terminal Migration Workstream

Status: Active
Updated: 2026-07-13

## Purpose And Boundary

This record sequences the accepted replacement of the legacy large-world implementation by the final-named `StrategicMap` path. It is durable migration and acceptance memory, not gameplay or architecture authority and not blanket implementation authorization.

Every remaining stage requires its own user-confirmed active work item, scoped implementation, and independent verification. A stage may summarize the accepted authorities below, but it must return to discussion if implementation needs a new gameplay rule, architecture decision, persistent-state owner, cross-system contract, or content assignment.

## Authority Routes

Player-facing rules remain authoritative in:

- `../gameplay-design/content-systems-long-term-design.md`
- `../gameplay-design/details/strategic-world-region-presentation.md`
- `../gameplay-design/details/strategic-region-detail-map-mapping.md`

Implementation ownership and contracts remain authoritative in:

- `../system-design/strategic-world-map-authoring-architecture.md`
- `../system-design/strategic-management-system-architecture.md`
- `../system-design/strategic-region-detail-map-mapping-architecture.md`
- `../system-design/strategic-battle-bridge-architecture.md`
- `../system-design/site-map-layout-architecture.md`
- `../system-design/semantic-map-marker-architecture.md`
- `../system-design/scene-transition-router-architecture.md`
- `../system-design/resource-authoring-taxonomy.md`

When this record and an accepted authority disagree, the authority controls and execution stops for discussion.

## Terminal Definition

The migration is terminally complete only when all of the following are true in the production path:

- the game boots through the replacement large-world scene and production Chunk presentation;
- province and member-city geometry is displayed from canonical `StrategicMap` geography and stable identities;
- city hover, selection, faction treatment, and interaction resolve through the owning `ProvinceId` and `LocationId`;
- a mapped member city enters and returns from its province-owned detailed map with validated semantic context, without the legacy `SiteId`/`StrategicWorldRuntime` visit handoff;
- strategic navigation and army movement operate on the replacement map while preserving Strategic Management as mutable campaign authority;
- battle launch, scene handoff, result settlement, and strategic return preserve the accepted `ProvinceId`, member `BattleLocationId`, and province-owned `LayoutId` lineage;
- the main scene has cut over to the replacement path;
- the legacy strategic-world implementation and every temporary migration adapter are deleted, remaining repository references are removed or explicitly unrelated, and no fallback, compatibility authority, dual runtime path, or dual write remains.

Visual parity, standalone rendering, partial subsystem wiring, or a temporary dual path does not satisfy terminal completion.

## Ordered Stages

| Stage | Name | Status | Dependency |
|---|---|---|---|
| 0 | Greenfield foundation | Completed | None |
| 1 | Production Chunk presentation | Completed | Stage 0 |
| 2 | Strategic Management identity and state convergence | Completed | Stage 1 |
| 2.5 | Multi-map package and publishing pipeline | In progress | Stage 2 |
| 3 | Region interaction and direct detailed-map entry/return | Not started | Stage 2.5 |
| 4 | Strategic navigation and movement | Not started | Stage 3 |
| 5 | Battle and settlement integration | Not started | Stage 4 |
| 6 | Main-scene cutover and legacy deletion | Not started | Stage 5 |

The order is a dependency contract. A later stage may perform preparatory analysis, but it must not absorb implementation or acceptance owned by an earlier unverified stage.

### Stage 0: Greenfield Foundation

Purpose: establish legacy-independent province, member-city, visual-geometry, province-owned layout, visual-chunk, canonical loading, and validation foundations.

Required outputs and acceptance boundary:

- the accepted schema and canonical Qinghe/Chiyan content use province-owned `LayoutId` and one `LocationId`-keyed geometry per member city;
- pure `StrategicMap` definitions, loaders, validators, and focused regressions have no dependency on legacy strategic-world owners;
- the Web workbench and derived artifacts consume the same canonical identity model.

Must not absorb: production presentation, navigation, movement, region interaction, Strategic Management runtime connection, battle integration, cutover, or legacy deletion.

Evidence: completed and independently verified in `../work-items/archived/2026/2026-07-13-strategic-map-greenfield-foundation.md`.

### Stage 1: Production Chunk Presentation

Purpose: turn the accepted static geography and visual-chunk contract into the production large-world presentation boundary.

Required outputs:

- authored production `StrategicMap` Chunk scenes/resources and their final visual-media bindings;
- read-only chunk selection, loading/residency, world-coordinate query, camera-visible presentation, and actionable low-noise failure diagnostics;
- focused verification that canonical chunk identity and location/province geometry display correctly without depending on the visible legacy TileMap.

Acceptance boundary: the replacement map can boot in an isolated production presentation path and display/query canonical Chunks reliably; it does not become the main scene yet.

Must not absorb: strategic mutable state, city interaction, semantic detailed-map entry, navigation or army movement, battle wiring, main-scene cutover, or legacy deletion.

### Stage 2: Strategic Management Identity And State Convergence

Purpose: make retained Strategic Management the sole mutable campaign authority for the province/member-city identities consumed by the replacement map.

Required outputs:

- Strategic Management definitions, state, commands, initialization, and presentation view models use the accepted `ProvinceId`, `LocationId`, and province-owned `LayoutId` lineage;
- any required legacy-save or first-slice identity translation is explicit, versioned, external to `StrategicMap`, and owns no new facts;
- the replacement presentation reads strategic state through accepted ports without fallback or dual write.

Acceptance boundary: canonical geography identities and Strategic Management state agree, invalid or ambiguous identity migration fails explicitly, and legacy owners no longer supply mutable facts to the replacement presentation.

Must not absorb: city-region input flow, detailed-map scene transition, navigation/movement, battle launch or settlement, main-scene cutover, or broad legacy deletion.

### Stage 2.5: Multi-Map Package And Publishing Pipeline

Purpose: replace the mock single-map/fixed-path coupling with a versioned multi-map authoring, publication, runtime-package, scenario, and save-identity boundary before interaction work begins.

Required outputs:

- MapId-scoped workbench projects support incomplete drafts, explicit validation profiles, and atomic immutable package publication;
- exact chunk-aligned region artifacts support more than 255 locations and resolve only to stable geographic identities;
- generic `StrategicMap` presentation consumes a selected package without current-map paths, dimensions, or identities;
- Strategic Management scenarios own campaign-start facts and saves validate explicit map/scenario compatibility;
- the current mock and one materially different fixture package prove replacement through selection alone.

Acceptance boundary: two different packages load through the same generic production scene/code, invalid drafts cannot publish, and incompatible scenario/save identity fails before partial initialization.

Must not absorb: hover/selection/click, detailed-map entry, navigation runtime or movement, final content/art, main-scene cutover, or legacy deletion.

### Stage 3: Region Interaction And Direct Detailed-Map Entry/Return

Purpose: connect city-region presentation to validated member-city interaction and the existing province-owned detailed-map pipeline.

Required outputs:

- hover, selection, faction treatment, and interaction resolve the actual member-city `LocationId` and owning `ProvinceId` from canonical geometry;
- direct entry resolves the province's authoritative `LayoutId` plus the confirmed member-city semantic mapping and carries typed context through the accepted scene-transition boundary;
- return restores the replacement strategic-map context without using the legacy `SiteId`/`StrategicWorldRuntime` visit handoff;
- the existing detailed-map scene structure, semantic markers, extraction, and validation are reused rather than rewritten.

Acceptance boundary: at least the separately confirmed Stage 3 mapping slice can enter its detailed map, validate identity and semantic context, and return to the replacement map with explicit failure behavior for missing or mismatched mappings.

Must not absorb: broad detailed-map art/content polishing, unconfirmed Chiyan layout or city-to-marker assignments, strategic navigation/movement, battle launch/settlement, main-scene cutover, legacy deletion, or the separate first-city internal-authoring workflow. Stage 3 may consume accepted detailed-map assets and contracts, but it must not gate, pause, resume, redefine, or replace that independent workstream.

### Stage 4: Strategic Navigation And Movement

Purpose: run strategic traversal and army movement against the replacement geography and navigation boundary.

Required outputs:

- compiled or authored navigation data remains distinct from visual Chunks and canonical geography while resolving stable strategic identities;
- Strategic Management-controlled armies request routes, move on world-map time, respect confirmed passage/access rules, and arrive with deterministic location context;
- movement, route failure, and dynamic-access changes expose low-noise diagnostics and do not mutate campaign facts from presentation.

Acceptance boundary: a confirmed movement slice can route, advance, pause with modal strategic flow, and arrive on the replacement map without legacy world navigation or army-state authority.

Must not absorb: unconfirmed vision, discovery, supply, or strategic-AI rules; battle settlement integration; main-scene cutover; or legacy deletion.

### Stage 5: Battle And Settlement Integration

Purpose: connect replacement-map approach and contest context to the retained Strategic Battle Bridge and return accepted results through Strategic Management.

Required outputs:

- battle intent validates and preserves `ProvinceId`, member `BattleLocationId`, authoritative `LayoutId`, and separately identified expedition source;
- semantic entrance/deployment/route context is resolved through the accepted member-city mapping and Bridge Active Context;
- battle preparation/runtime entry, result summary, Strategic Management command writeback, rollback, and return to the replacement map use the accepted bridge and router boundaries;
- missing, stale, or mismatched context fails explicitly without legacy request/result fallback or strategic mutation.

Acceptance boundary: a confirmed end-to-end battle slice launches from the replacement strategic flow, settles through the sole accepted command path, and returns to the replacement map with correct province/city attribution.

Must not absorb: new battle rules, exact unconfirmed content assignments, main-scene cutover, or deletion before the integrated replacement path is independently verified.

### Stage 6: Main-Scene Cutover And Legacy Deletion

Purpose: make the verified replacement path the sole production large world and remove migration-only code and assets.

Required outputs:

- startup and all accepted return routes target the replacement `StrategicMap` scene;
- the legacy strategic-world scene/runtime/state, visible legacy TileMap path, fixed legacy site identities/markers, obsolete visit handoff, and temporary migration adapters are deleted;
- repository scans, focused regressions, build checks, and manual flow QA cover every terminal criterion and find no fallback, dual runtime path, dual write, or live legacy reference.

Acceptance boundary: every item in the terminal definition passes together in the cut-over production path, and independent verification confirms deletion and reference cleanup.

Must not absorb: unrelated refactors, new strategic features, content expansion, or redesign of retained Strategic Management, Strategic Battle Bridge, detailed-map, battle Runtime, settlement, or scene-router authority.

## Detailed-Map Direct-Integration Boundary

The existing detailed-map assets and contracts are reusable inputs: authored base/layout scenes, semantic markers, extracted marker data, topology, height connections, and stable `LayoutId`/`MarkerId` validation. They remain owned by their accepted detailed-map authorities.

The terminal connection must originate from canonical province/member-city identities and the province-owned layout. A member-city mapping may select a semantic entrance, deployment, route, objective, or other confirmed context inside that layout; it cannot select another layout, project large-world geometry into detailed-map cells, or make marker data own campaign identity.

The legacy visit path based on `SiteId`, `StrategicWorldRuntime`, or equivalent legacy world-flow authority is not reusable as the terminal connection. A narrowly scoped transition carrier may exist only while its owning stage requires it and must be deleted at Stage 6.

### Independent First-City Internal Authoring

First-city detailed-map internal authoring is independent from and non-conflicting with this terminal-migration workstream. Its own task owns reusable base/layout inheritance, internal scene and content authoring, and rapid iteration. This workstream owns only how the replacement large world resolves and hands off `ProvinceId`, member-city `LocationId`, province-owned `LayoutId`, and validated semantic context into and back from the accepted detailed-map boundary.

Neither workstream replaces, absorbs, blocks, or redefines the other. First-city internal authoring may proceed under its own confirmed scope without waiting for a StrategicMap stage, while a StrategicMap integration stage may reuse accepted detailed-map assets and contracts without taking ownership of their internal authoring. Any future interface-specific content requirement still needs its own confirmed task boundary; this record does not change the status, resume rules, or scope of `../work-items/active/2026-07-12-first-city-site-map-layout-authoring.md`.

## Legacy Retirement Rules

- `StrategicMap`, Strategic Management, Strategic Battle Bridge, detailed-map layouts/markers, battle Runtime, settlement, and the scene router retain only their accepted responsibilities.
- A temporary migration adapter may exist only outside `StrategicMap`, translate at one explicit boundary, own no business fact, and provide no alias, fallback, reverse synchronization, or dual write.
- No stage may add new behavior to `StrategicWorldRuntime`, `StrategicWorldState`, `WorldArmyState`, `StrategicWorldRoot`, fixed legacy site markers, the visible legacy TileMap, or legacy battle/site handoffs.
- Stage 6 deletes the legacy implementation and every temporary adapter only after the complete replacement flow is verified; deletion must not remove reusable detailed-map assets merely because a legacy path once entered them.
- A remaining legacy reference is acceptable only when a scoped scan proves it is historical, test-fixture-only, editor-only, or otherwise outside the live production path. It must be named in verification evidence rather than silently ignored.

## Deferred Decisions And Non-Goals

This workstream does not decide:

- exact Chiyan detailed-layout content;
- exact member-city-to-marker assignments beyond a separately confirmed stage slice;
- vision, discovery, supply, or strategic-AI rules;
- final auxiliary-city display names or balance identities;
- detailed-map art/content polishing not required by an accepted integration slice.

It also does not authorize code, scenes, resources, config, tests, authority changes, or any remaining stage. Those changes require the stage's own confirmed active work item.

## Current Progress And Next Gate

- Stage 0 is complete and archived with independent verification.
- Stage 1 production Chunk presentation is complete and independently verified; its archived task records all 35 deterministic production import sidecars, bounded native threaded residency, stale/failure semantics, focused regressions, workbench compatibility, project build, forbidden scans, and diff hygiene.
- Stage 2 is complete and independently verified. Strategic Management uses canonical main-city identities, initializes all eleven canonical city-control records without inventing auxiliary content, exposes immutable replacement-map control views, migrates supported saves atomically to version 4, and keeps the retained main scene behind one temporary fixed-site adapter.
- Stage 2.5 multi-map package and publishing work is in progress under `../work-items/active/2026-07-13-strategic-map-multi-map-package-pipeline.md`. Stage 3 is gated on its independent completion; Stages 3-6 remain unstarted.
- First-city detailed-map internal authoring remains an independent, non-conflicting workstream whose status and execution gates are owned only by its own active task; this record neither waits on it nor makes it wait on StrategicMap migration.

## GodotPrompter Skills

No installed skill applies to this documentation-only workstream record. Each implementation-stage task must name the skills applicable to its actual subsystem and scope.
