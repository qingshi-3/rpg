# Strategic World Region Presentation

## Parent Authority

This detail refines `../content-systems-long-term-design.md` for player-facing strategic-world city and region presentation. It defines a minimum visual baseline, not strategic gameplay rules or implementation architecture.

## Player Promise

Provinces read as coherent geographic areas composed of their main and auxiliary cities' contiguous visual regions. Their presentation remains calm and legible while preserving clear local interaction and faction ownership cues.

## Minimum Presentation Baseline

- A province visually comprises the contiguous regions of its main city and auxiliary cities; each city owns exactly one region geometry through its stable `LocationId`.
- Region boundaries read as natural, continuous, soft curves rather than sharp corners or straight polygon seams.
- The default treatment is restrained, low-saturation, translucent, and frosted. Chaotic multicolor fills, strong glow, and glossy-glass treatment are outside the baseline.
- A province's outer border communicates faction ownership.
- Hover emphasizes only the city region under the pointer.
- Hover, selection, and faction treatment remain visually isolated to their owning city and province; they must not contaminate another province.
- The user-accepted standalone prototype quality is the minimum baseline. Future formal work may improve it, but must not visibly regress below it.

## Gameplay Boundary

This presentation baseline does not define vision, discovery, encounter timing, or exact territorial-control rules. The confirmed province/city relationship with detailed maps lives in `strategic-region-detail-map-mapping.md`; presentation preserves `ProvinceId` and `LocationId` without creating an independent region identity or campaign-state owner.

## Implementation Boundary

The standalone prototype scene, code, shader, masks, compiler changes, and resources are reference evidence only. They are neither production runtime authority nor a mandatory technical implementation for formal strategic-world presentation.
