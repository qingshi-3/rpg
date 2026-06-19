# Acceptance

Status: Pending

The user requested creation of this active design proposal on 2026-06-17 after discussing the desired site-map layout authoring direction:

- Godot inherited base terrain scenes and layout variant scenes;
- layout variants, not base terrain, own bridges, decorations, resources, obstacles, deployment zones, building slots, and other content semantics;
- bridge gameplay is configured through semantic markers instead of inferred from tile art;
- same-height river bridges behave as ordinary walkable ground;
- height bridges use the bridge surface's height and require explicit height connections;
- each grid cell has at most one final standable gameplay surface;
- reusable layouts must not share persistent strategic-location state.
- the first authored module should live under `scenes/city/`;
- the first implementation city should be a small plains-city validation slice with one base terrain scene and one inherited layout scene.

This proposal is still Draft. After the user reviews and accepts the expected documents, merge the expected copies into authority documents, archive the proposal, then create a focused implementation proposal before code, scene, resource, or data changes.
