# Strategic World Region Presentation

## Parent Authority

This detail refines `../content-systems-long-term-design.md` for player-facing strategic-world city and region presentation. It defines a minimum visual baseline, not strategic gameplay rules or implementation architecture.

## Player Promise

Cities read as coherent geographic areas composed of multiple contiguous smaller regions. Their presentation remains calm and legible while preserving clear local interaction and faction ownership cues.

## Minimum Presentation Baseline

- A city visually comprises multiple contiguous smaller regions.
- Region boundaries read as natural, continuous, soft curves rather than sharp corners or straight polygon seams.
- The default treatment is restrained, low-saturation, translucent, and frosted. Chaotic multicolor fills, strong glow, and glossy-glass treatment are outside the baseline.
- A city's outer border communicates faction ownership.
- Hover emphasizes only the smaller region under the pointer.
- Hover, selection, and faction treatment remain visually isolated to their owning city and faction; they must not contaminate another city or faction.
- The user-accepted standalone prototype quality is the minimum baseline. Future formal work may improve it, but must not visibly regress below it.

## Gameplay Boundary

This presentation baseline does not confirm or define vision, discovery, encounter timing, territorial control, or mapping between strategic regions and detailed maps. Those rules require separate gameplay confirmation.

## Implementation Boundary

The standalone prototype scene, code, shader, masks, compiler changes, and resources are reference evidence only. They are neither production runtime authority nor a mandatory technical implementation for formal strategic-world presentation.
