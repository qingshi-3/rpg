# Presentation UI Layout Architecture Proposal

Status: First Phase Implemented

Date: 2026-05-17

## Problem

The current UI implementation has grown through scene-specific HUDs:

- `StrategicWorldRoot` instantiates `StrategicWorldHud.tscn` and owns world-map detail/action UI.
- `WorldSiteRoot` instantiates `WorldSitePeacetimeHud.tscn` and owns site-management, battle-preparation, and recent-feedback UI.
- Battle preparation currently reuses the site management panel lists instead of having a mode-specific panel.

This causes large permanent panels to cover the map, makes mode transitions fragile, and encourages future agents to add one-off windows or duplicate state.

## Scope

This proposal defines a Presentation/UI layout architecture and a phased implementation guide. It changes where UI content is hosted and how future UI code is allowed to bind data, but it does not change gameplay rules, persistent state, runtime combat rules, settlement rules, or command authority.

## Current Design

See `current/system-design/presentation-ui-layout-architecture.md`.

## Expected Design

See `expected/system-design/presentation-ui-layout-architecture.md`.

## Implementation Strategy

Implement in batches. The first implementation batch should move main work panels to a stable left primary workspace while keeping existing data binding and node names as much as possible. Later batches split mode-specific content and introduce view-model/binder boundaries.

## Non-Goals

- Do not replace battle runtime or settlement.
- Do not implement the final light-RTS command UI in this proposal.
- Do not introduce new long-term state for UI.
- Do not make UI a second source of truth for units, deployment, battle outcome, resources, or reports.
- Do not convert every scene to a single global shell in the first batch.

## Acceptance

User accepted the expected architecture and phased implementation on 2026-05-17. Batches 1-4 are implemented and merged into `system-design/`; right-side notification/minimap and future battle command hosts remain future work.
