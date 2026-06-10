# Acceptance

Status: Accepted

The user accepted the expected architecture on 2026-06-10 and requested optimization.

Accepted scope:

- Keep Runtime battle hot paths free of flow-field construction and per-unit whole-map A*.
- Upgrade local neighbor movement with Runtime-owned local steering memory.
- Add an explicit obstacle-following state for static topology blockers.
- Use optional coarse route hints for map-authored entrances, lanes, gates, and chokepoints.
- Treat dynamic living-unit blockers as queue/support/hold-pressure cases rather than static obstacles to wander around.
- Preserve Runtime validation as the final authority for topology, footprint, occupancy, reservations, movement commits, attacks, and events.
