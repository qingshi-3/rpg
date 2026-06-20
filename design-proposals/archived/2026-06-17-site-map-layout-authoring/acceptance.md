# Acceptance

Status: Accepted

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

The user accepted this proposal on 2026-06-19 by asking to begin creating the first city map. The expected copies should be merged into authority documents, this proposal archived, and implementation should proceed only through `gameplay-alignment/implementation-proposals/2026-06-19-site-map-layout-first-city.md`.
