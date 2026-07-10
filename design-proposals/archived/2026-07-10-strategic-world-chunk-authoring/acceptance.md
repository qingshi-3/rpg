# Acceptance

Status: Accepted by user on 2026-07-10 and updated to the local Web workbench direction on 2026-07-10.

## User Direction

The user accepted the following direction after iterative visual, architecture, and scope discussion:

- the final strategic world is much larger than the current prototype and should be designed for a Sanguo Qunying-scale campaign;
- visible world terrain is static chunk art rather than a visible runtime TileMapLayer;
- terrain presentation stays intentionally simple and only communicates strategic geography;
- CraftPix terrain assets already present in the project are the visual reference;
- geographic data editing moves out of the Godot editor into a local Web workbench;
- the Web workbench is limited to layer management, terrain editing, rivers/roads/mountains, strategic locations, and city territories/smaller regions;
- final chunk art and real-world references are comparison layers, not automatic art-generation responsibilities;
- Godot retains navigation authoring, global navigation compilation, runtime chunk presentation, fog and territory interaction, scene transitions, and final in-engine validation;
- Web and Godot use the same world coordinates, chunk manifest, and stable ids;
- navigation remains globally available and supports dynamic access when gates or bridges change state.

This updated direction replaces the earlier accepted statement that a complete-world Godot editor tool owns geographic assembly and navigation-facing world inspection.

## Explicit Web Scope

The accepted first scope contains only:

1. management of the ten configured reference, geography, art, mask, and validation layers;
2. terrain classification editing and chunk-aligned terrain masks;
3. continuous rivers, roads, mountains, and their chunk-edge validation;
4. cities and other strategic-location definitions and placement validation;
5. city territories, smaller regions, hover simulation, topology validation, `territory_mask`, lookup data, and compiled outlines.

Basic local loading, saving, undo/redo, property editing, and error navigation are support infrastructure. CraftPix browsing, final chunk-art generation, generation queues, batch processing, art comparison, and advanced automatic repair are excluded.

## Review State

- Expected authority copies prepared: Yes
- Expected authority copies updated for the local Web direction: Yes
- Merged to authority documents: Yes
- Archived: Yes
- Follow-up implementation proposal: `gameplay-alignment/implementation-proposals/2026-07-10-strategic-world-map-workbench.md`

## Notes

The expected authority copies were merged and verified byte-for-byte against their authority destinations before archive. Implementation proceeds only through the linked focused implementation proposal.
