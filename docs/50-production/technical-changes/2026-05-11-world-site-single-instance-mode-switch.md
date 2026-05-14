# 2026-05-11 World Site Single-Instance Mode Switch

## Background

`WorldSiteRoot` now serves as the persistent runtime host for a site. Battle and peacetime are two modes of the same site scene instance, not separate scene instances for the same map framework.

## Changes

- Battle entry from site management no longer reloads `WorldSiteRoot.tscn`.
- `WorldSiteRoot` switches between battle runtime and peacetime runtime in place.
- The site map, placement entities, slot state, and battle entities remain attached to the same scene instance across mode changes.

## Rule

- Use one site scene instance per active site session.
- Change mode by updating runtime data and visibility, not by re-instantiating the same site scene to represent another state of the same map.

## Verification

- Starting a site battle from management keeps the same `WorldSiteRoot` instance alive.
- Ending battle returns the site to management without reloading the site scene.
