# Strategic Operation Foundation Proposal

Status: Accepted

## Relationship Metadata

- Requirement Id: `STRAT-OPS-001`
- Parent Proposal: None
- Supersedes: None
- Superseded By: None
- Amends: None
- Amended By: None
- Affected Authority Documents:
  - `gameplay-design/content-systems-long-term-design.md`
  - `gameplay-design/details/cities-and-locations/README.md`
  - `system-design/strategic-management-system-architecture.md`
  - `system-design/strategic-battle-bridge-architecture.md`
- Related Implementation Proposals: Pending after design acceptance and authority merge.

## Goal

Define the first player-visible strategic operation loop after the Strategic Management rebuild. The loop should make city development, resource income, troop/hero growth, expedition, and new-area development visible before adding special routes such as beast taming.

## Current Design Summary

The accepted authority already defines Strategic Management as the long-term owner for resources, city facilities, corps instances, heroes, expeditions, elapsed world-map time, and battle-result writeback. The current playable behavior, however, still feels close to the pre-rebuild slice because the new strategic model has not yet received enough player-facing operation content.

## Complete Foundation Design

Strategic Operation should first prove a light but complete city-operation loop: the player develops a city, gains foundation resources over world-map time, converts city development into reserve soldiers and recruitable corps, forms hero companies, dispatches expeditions, and uses the result to occupy or develop new strategic locations. Beast taming, race-specific sanctuaries, advanced production chains, and specialized route resources are later content routes, not the foundation slice.

The first foundation resource set is `Money`, `Food`, `Wood`, and `Ore` / basic military material. City manpower uses a reserve model: `ActiveForces + ReserveForces <= CityForceCapacity`. Active forces are represented by corps, hero companies, and garrison instances; reserve soldiers are the city's prepared but unassigned soldiers. Reserve soldiers recover over world-map time and are spent on recruitment, replenishment, and later manpower-based local battle support.

City building uses authored, bounded construction regions rather than pure menu slots or unrestricted RTS base-building. The player chooses a building from a construction panel, sees a mouse-attached preview, and places it on a legal snapped grid position inside a compatible region. The first version checks footprint, overlap, bounds, region compatibility, and simple eligibility. It does not include workers, road connectivity, resource pathing, gathering range, or distance-based production efficiency.

Building state is split into definitions, city building instances, construction regions, and optional local battle anchors. Definitions describe building type, costs, upgrade chains, effects, requirements, and support capability. City building instances describe what a specific city has built, including level, construction state, placed region position, enabled state, and support state when relevant. Construction regions own layout constraints only. Optional `BattleAnchorId` values weakly bind specific support-capable buildings to authored battle support points.

The first building batch is deliberately small: `Farm`, `Market`, `Lumber Camp`, `Mine`, `Training Ground`, `Tavern` / hero recruitment office, `Arrow Tower`, and `Medical Shrine` / medical facility. These cover economy, military growth, hero access, and the first local support vocabulary without forcing special routes into the foundation.

Local building battle support is part of the long-term foundation design but not the first implementation slice's completion gate. Support begins as pre-battle selection or confirmation and must preserve a later upgrade path to in-battle manual activation. The bridge should pass support snapshots to Combat, not building ids or full city state. Combat consumes current-battle support entries and reports usage; Strategic Management remains the owner of city buildings, reserve soldiers, charges, resources, and writeback.

The first implementation slice should stop at the basic strategic loop: build, gain resources, raise capacity, recover reserves, recruit or replenish corps, form hero companies, dispatch expeditions, and occupy or develop a new strategic location. Full local building battle support is the next slice.

## Confirmed Decisions

- The first operation loop should be the basic strategic loop, not a special beast route:
  `build basic city facilities -> gain resources over world-map time -> recruit/create corps and heroes -> form hero companies -> dispatch expeditions -> capture/develop new areas`.
- First-version global resources are limited to `Money`, `Food`, `Wood`, and `Ore` / basic military material. Special resources such as beast materials, crystals, or soul dust are later route-specific resources, not first foundation resources.
- First-version city local manpower is simplified to active forces plus reserve forces under a total city force capacity. There is no population, public order, or civilian demographic model in this slice.
- City management uses authored, bounded construction regions with RTS-style preview placement. It is not pure slot/list construction, and it is not unrestricted `n*n` full-map RTS construction.
- Resource features are static map/location traits. They do not deplete, disappear, refresh, or require worker gathering. They support passive income and building eligibility.
- Building placement does not use distance-based gathering efficiency in the first version.
- Buildings may have two separate effect layers:
  - strategic effects such as income, unlocks, capacity, recovery, or recruitment;
  - local battle support effects when a battle occurs at that city or stronghold.
- Local building battle support should be limited and explicit when implemented. Training facilities can provide finite reinforcement; medical or shrine-like facilities can provide limited hero emergency aid; defensive towers can provide local fire support. Support use should be bounded by charges, trigger rules, costs, or battle context.
- Economic buildings are not first-version attack targets. Raiding or destroying economic buildings is a later strategic-action design, not part of the foundation loop.
- City building instances use weak battle-map binding in the first version. A building instance may carry an optional `BattleAnchorId`; only buildings with a resolved anchor participate as concrete battle support points. Economy buildings can remain unbound.
- The first-version building pool uses four categories:
  economy, military, hero/administration, and defense/support.
- The first building batch contains `Farm`, `Market`, `Lumber Camp`, `Mine`, `Training Ground`, `Tavern` / hero recruitment office, `Arrow Tower`, and `Medical Shrine` / medical facility.
- Special-route buildings such as `Beast Pen`, race sanctuaries, mage towers, or advanced production chains are later content, not part of the first foundation batch.
- Building facts are separated into building definitions, city building instances, city construction regions, and optional local battle anchors.
- City construction interaction uses a building panel, mouse-attached preview, grid snapping, and legality checks inside authored construction regions.
- First-version building placement does not include workers, road connectivity, resource pathing, or distance-based production efficiency.
- City force capacity is the total manpower capacity for active corps/garrison plus reserve soldiers. Reserve soldiers recover over world-map time and are consumed by recruitment, replenishment, and relevant local battle support.
- Local building battle support starts as pre-battle support selection or confirmation, and must preserve a later upgrade path to in-battle manual support activation.
- The strategic-to-battle bridge passes local building support as battle support snapshots. Combat must not query or own city building state directly.
- The first implementation slice should prove the basic strategic loop: city construction, resource income, city force capacity, reserve recovery, recruitment/replenishment, hero company formation, expedition, and occupying or developing a new area. Full local building battle support is a follow-up slice.

## Resolved Questions

### Q1: Should city construction regions bind to real battle-map coordinates?

Decision: use weak binding with optional `BattleAnchorId`.

City management presents authored construction regions. The region or placed building footprint is not required to be a concrete battle-map region. When a building should appear or matter in local battle, such as a training ground, medical site, shrine, arrow tower, or other support facility, its instance can reference an authored battle anchor. If no anchor exists, the building remains a strategic facility only.

This keeps first-version implementation stable while preserving a path toward richer city defense and building support battles.

### Q2: What are the first-version building categories and first batch of buildings?

Decision: include a compact but complete foundation batch, including the first local battle support buildings.

The first version separates buildings into four player-facing categories:

- Economy: `Farm`, `Market`, `Lumber Camp`, `Mine`.
- Military: `Training Ground`.
- Hero/Administration: `Tavern` or hero recruitment office.
- Defense/Support: `Arrow Tower`, `Medical Shrine` or medical facility.

The purpose of this batch is to make the basic strategic loop visible without starting special routes too early. Economy buildings produce the four foundation resources. The training ground connects city development to city forces, corps creation, and limited reinforcement support when anchored. The hero recruitment building gives the city a clear hero-growth function. Arrow tower and medical support are included in the first batch so the weak battle-map anchor rule has immediate gameplay meaning.

Buildings outside this batch, such as beast facilities, racial recruitment sanctuaries, mage towers, advanced industry, or specialized route buildings, should wait until the foundation loop is playable.

### Q3: What building facts should the strategic operation system own?

Decision: use separated building definitions, city building instances, construction regions, and optional local battle anchors.

`BuildingDefinition` is the template-level fact. It answers what a building type is: category, construction and upgrade costs, upgrade chain, strategic effects, unlocks, eligibility requirements, and whether the building can provide local battle support.

`CityBuildingInstance` is the city-level fact. It answers what a specific city has built: stable instance id, definition id, level, construction state, assigned construction region and grid position, enabled state, and local support state such as remaining charges when relevant.

City construction regions are layout constraints. They answer what a city can build where, which categories are allowed, and whether an area can resolve a local battle anchor. They should not own production, recruitment, upgrade, or support rules.

`BattleAnchorId` is an optional weak binding from a city building instance to an authored local battle support point. Large-map position, city construction layout, and battle-map anchor are separate concepts.

The exact runtime class/resource format, save serialization, caches, and editor authoring tools are implementation concerns for the follow-up system design and implementation proposal.

### Q4: Should city construction be pure slot selection or bounded RTS-style placement?

Decision: use bounded RTS-style placement inside authored construction regions.

Each city can expose several small construction regions, such as farming, workshop, military, defense, or core districts. The player chooses a building from a construction panel, sees a mouse-attached preview, and places it onto a legal grid position inside a compatible region.

Placement legality checks should cover region type, footprint size, overlap, bounds, and any simple building eligibility rule. The first version should not add workers, construction pathing, road networks, logistics distance, gathering range, or placement-based production efficiency.

This model gives city management more tactical and visual agency than pure slots while keeping the strategic operation layer lighter than an RTS economy. Building position mainly supports city readability, future local battle anchors, defense/support layout, and later optional adjacency rules.

### Q5: How do city force capacity, active forces, and reserve forces work?

Decision: use total city force capacity with active forces plus reserve soldiers.

The first version treats city manpower as a simple reserve model:

`ActiveForces + ReserveForces <= CityForceCapacity`.

`CityForceCapacity` is the city total capacity. It is raised by city development, military buildings such as the training ground, and later possibly city level or defensive infrastructure.

`ActiveForces` are soldiers already committed into corps, hero companies, or local garrison. They should be derived from owned/stationed military instances rather than maintained as a separate mutable pool when possible.

`ReserveForces` are the city's unassigned prepared soldiers. They recover over world-map time up to the remaining capacity after active forces are counted. Creating corps, replenishing damaged corps, or using manpower-based local battle support consumes reserve soldiers and other required resources.

This keeps city manpower strategically meaningful without adding population, civilian demographics, public order, conscription policy, or training queues in the first version.

### Q6: How is local building battle support selected and triggered?

Decision: start with pre-battle support selection or confirmation, and reserve in-battle manual activation as a required future extension.

When a battle is triggered at a city or stronghold, the pre-battle confirmation should surface available local building support from that location. First-version support can be selected, confirmed, or automatically included from that pre-battle step depending on the specific support type, but the player should see which support is entering the battle and what it may consume.

Support consumption should be explicit. It can spend building charges, reserve soldiers, resources, cooldown state, or battle-context availability. The post-battle report should show what support was used and what was consumed.

The design must not close the door on in-battle manual activation. Later, tower fire, reinforcement calls, emergency healing, shrine effects, and similar local supports should be able to appear in the battle HUD as player-triggered tactical tools. First-version implementation should therefore treat support as bridge-delivered support entries rather than hardwired hidden battle rules.

### Q7: What should the first-version battle bridge pass for local building support?

Decision: pass battle support snapshots rather than building ids or full city state.

Strategic Management owns city state, building instances, reserve soldiers, support charges, and building eligibility. When a battle is triggered at a city or stronghold, the bridge translates eligible local building support into battle-readable support entries for that battle.

Each support snapshot should carry only the facts combat needs for the current battle, such as support type, source city id, source building instance id, display name, trigger mode, available charges, optional `BattleAnchorId`, cost or consumption preview, and effect parameters. The exact field names and serialization shape belong to the follow-up implementation proposal.

Combat should consume these snapshots as current-battle capabilities. It should not query Strategic Management for building definitions, city construction state, reserve soldier recovery, or economic facts. After battle, combat reports which support entries were used and the bridge writes the resulting consumption back to Strategic Management.

This keeps the boundary clean: Strategic Management decides what exists and what can be offered; the bridge translates it; Combat decides how the current battle executes support effects.

### Q8: What is the acceptance scope for the first implementation slice?

Decision: the first implementation slice should run the basic strategic loop, while full local building battle support remains the next slice.

The first slice should prove that Strategic Operation creates new player-facing gameplay beyond the current rebuilt shell:

- build basic city facilities inside bounded construction regions;
- gain `Money`, `Food`, `Wood`, and `Ore` over world-map time;
- use training-ground and city development to raise city force capacity;
- recover reserve soldiers over world-map time;
- recruit or replenish corps from reserve soldiers and resources;
- form hero companies;
- dispatch expeditions from a city;
- occupy or develop a new strategic location.

Local building battle support should not be part of the first slice's completion requirement beyond keeping the data and bridge direction compatible. The next slice should connect support snapshots into pre-battle selection, battle execution or reporting, and strategic writeback.

## Open Design Questions

All initial outline questions have been resolved, the expected authority document copies have been updated, and the user accepted the proposal. After authority merge and archival, create a follow-up implementation proposal before code, scene, resource, or data work begins.

## Proposal Workflow

This proposal has been accepted for authority merge and archival. Code, scene, resource, and data implementation must wait for a follow-up implementation proposal after authority merge and proposal archival.
