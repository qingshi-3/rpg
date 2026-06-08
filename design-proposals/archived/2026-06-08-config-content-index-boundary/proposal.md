# Config Content Index Boundary Proposal

Status: Archived

## Relationship Metadata

- Requirement Id: `REQ-config-content-index-boundary`
- Parent Proposal: None
- Supersedes: None
- Superseded By: None
- Amends: None
- Amended By: None
- Affected Authority Documents: `system-design/battle-content-progression-architecture.md`
- Related Implementation Proposals: `gameplay-alignment/implementation-proposals/2026-06-08-config-content-index-boundary.md`

## Current Architecture

First-slice roster and mapping data are split between a Godot `.tres` resource under `assets/definitions/world/` and C# constants in `FirstSliceHeroCompanyIds`. This makes configuration look like authored asset content and turns code into a content list authority.

## Expected Architecture

Repository-level configuration indexes live under `config/` as plain mapping data. `config/` can reference resource ids and paths, but it does not own Godot resources or imported assets. Unit `.tres` files remain in `assets/battle/units/` as authored resources.

Application code reads config indexes and exposes typed query wrappers for existing callers. Runtime continues to consume ids and snapshots, not config files.

## Acceptance

The user accepted this direction in conversation by instructing execution after reviewing the proposed `config/` split.
