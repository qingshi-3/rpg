# Strategic World Fog Of War

This document defines the player-facing fog-of-war direction for the strategic map. Technical state and rendering contracts live in `../../30-technical-design/world/strategic-world-fog-of-war.md`.

## Goal

Fog of war should add exploration, uncertainty, and reasons to move across the strategic map. It must not become the core gameplay by itself.

The core pillars remain:

- WorldSite operation and growth.
- Strategic pressure from forces, threats, and faction activity.
- Tactical battles with meaningful world writeback.

Fog supports those pillars by controlling what the player knows about locations, events, enemy movement, and resource opportunities.

## Information States

Strategic-map information uses three player-facing states:

```text
Unknown
Revealed
Visible
```

### Unknown

The player has not explored this area.

- Terrain and content are hidden or heavily obscured.
- Concrete WorldSite, event, army, and resource details are not shown.
- The UI should avoid pretending unknown content is selectable.

### Revealed

The player has explored this area before, but it is not currently visible.

- Terrain and discovered WorldSite positions remain visible.
- The shown site/resource/enemy information is old intelligence.
- UI should communicate that the information may be stale, for example with a last-seen world tick.

### Visible

The area is currently inside player vision.

- Current terrain, discovered WorldSite state, known resources, active events, enemy forces, and threat markers can be shown.
- Hover and detail panels may show authoritative current information.

## First Implementation Scope

The first fog version should implement:

- Three information states: `Unknown`, `Revealed`, `Visible`.
- Current vision from player-owned WorldSites and player-controlled armies or expeditions.
- Previously discovered map areas and WorldSites remaining visible as stale intelligence.
- UI behavior that hides unknown content and marks stale content.
- Fog presentation on the strategic map surface.

The first version should not implement:

- A complex scouting profession, scout unit system, or dedicated reconnaissance economy.
- Random map content assignment.
- Terrain line-of-sight blockers, stealth, detection, false intelligence, or detailed enemy ambush rules.
- Full enemy surprise-attack gameplay from fog. Fog may allow future surprise pressure, but that is not a first-scope requirement.

## Player Decisions

Fog should make these decisions clearer:

- Which direction to move toward next.
- Whether an old piece of intelligence is still trustworthy.
- Whether to invest in a site because it reveals or controls nearby territory.
- Whether to enter a visible threat, ignore it, or prepare first.

It should not force the player to perform repetitive scouting chores before every useful action.

## Randomization Boundary

Map randomization is a separate system.

Fog must work with fixed authored maps, seeded slot maps, or later random content assignment, but it must not own map generation or decide which content exists at a slot.

The fog layer only answers:

```text
What does the player currently know?
How fresh is that knowledge?
What should the UI reveal?
```

