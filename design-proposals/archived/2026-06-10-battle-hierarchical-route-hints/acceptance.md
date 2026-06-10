# Acceptance

Status: Accepted

The user accepted the expected architecture on 2026-06-10.

Accepted scope:

- Add a static hierarchical route-hint layer compiled from battle navigation topology.
- Use chunk/sector and portal-style route topology for map-scale static barriers such as rivers, gates, and chokepoints.
- Query route hints at low frequency for battle groups or group action zones, not per actor per movement decision.
- Route by footprint clearance profile, using the largest required group footprint in the first implementation.
- Keep actor movement local: units still choose and validate neighboring cells through Runtime topology, footprint, occupancy, and reservations.
- Preserve local attack, support, queue, pressure, and combat-slot behavior as local-combat logic rather than global route logic.
- Do not restore Runtime flow fields, Presentation path authority, or per-unit whole-map A* hot paths.
