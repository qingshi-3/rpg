# 2026-05-04 Site Visual Footprint Recognition

## Background

Strategic `WorldSite` nodes are now authored as TileMap art on `WorldMapRoot/SiteVisualLayer`. The previous code-drawn circular site placeholders no longer match the map production workflow.

## Changes

- Added `StrategicWorldRoot.SiteVisualLayerPath`, defaulting to `WorldMapRoot/SiteVisualLayer`.
- Added runtime footprint building for each `WorldSiteDefinition`.
- Site buttons, hit testing, selection outlines, and labels now use the authored footprint when available.
- The old code-drawn circular site icon remains only as a fallback when the visual layer is missing or the site anchor is not inside a visual tile.
- Strategic camera bounds include `SiteVisualLayer` tiles so site art is not clipped out of the map view.
- Removed the temporary yellow facility dot and blue garrison square.
- Hero count and army count indicators are deferred until a formal UI / icon asset contract is available.

## Authoring Contract

```text
WorldMapRoot
  SiteVisualLayer
  MapAnchors
    Sites
      <site_id>
```

- Put only strategic `WorldSite` art on `SiteVisualLayer`.
- Put `MapAnchors/Sites/<site_id>` inside the matching site art.
- Keep different site artworks disconnected on `SiteVisualLayer`; an edge-connected region is treated as one footprint.
- Site gameplay state remains in `WorldSiteDefinition` and `WorldSiteState`, not in tile metadata.

## Runtime Behavior

For each site, the runtime converts the site anchor to a `SiteVisualLayer` cell and scans occupied cells in 4 directions. The resulting map-space bounds drive the control hit rectangle and UI overlay positions.

This keeps site visuals loose-coupled from tile size and asset shape while preserving a simple authoring rule.

Hero / army count display is intentionally not implemented in this pass. It should be added after the formal strategic icon assets and hero-location model are defined.

## Verification

- Build with `dotnet build rpg.sln`.
- Hover and click each configured site art region.
- Select each site and confirm the outline follows the authored art bounds.
- Confirm no yellow or blue temporary markers remain on configured site art.
- Start expedition targeting and confirm blocked site commands still show the forbidden cursor over the art footprint.
