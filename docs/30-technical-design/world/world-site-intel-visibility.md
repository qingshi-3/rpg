# WorldSite Intel Visibility Technical Contract

## Authority

`WorldSiteDefinition.Intel` owns authored visibility policy. `WorldSiteState` owns true runtime state. `WorldSiteState.Memory` owns discovered site memory. `StrategicWorldIntelState.KnownSites` owns stale snapshots. `WorldSiteIntelService` is the only authority that builds current views, snapshots, and stale views.

## Data Flow

`WorldSiteDefinition.Intel + WorldSiteState + WorldIntelVisibility`
-> `WorldSiteIntelService.BuildCurrentView`
-> `WorldSiteIntelViewModel`
-> Strategic UI / site UI

`WorldSiteDefinition.Intel + WorldSiteState`
-> `WorldSiteIntelService.BuildSnapshot`
-> `StrategicWorldIntelState.KnownSites`
-> `WorldSiteIntelService.BuildViewFromSnapshot`

## Battle Handoff

`BattleStartRequest` carries structured ids and tags only, not Chinese UI prose.

`WorldSiteIntelService.ApplySiteIntelToRequest` is the shared handoff path for site intel fields. Battle builders and exploration battle entry must call it before tactical scene handoff.

`BattleStartRequest.AvailableEntrances` must be filtered through the current intel view for player-side non-garrison entrances. Hidden authored entrances stay unavailable until `WorldSiteState.Memory.RevealedEntranceIds` or full tactical visibility reveals them. Defender and garrison entrances may remain available when they are needed as deployment authorities for defense/runtime placement.

`WorldSiteRoot` exploration entry placement uses the same known player entrance authority. A visiting army's `TargetApproachDirection` is only a preference when a currently known player non-garrison entrance supports that direction; otherwise placement falls back to another known player entrance or fails with a log instead of using hidden entrance deployment priority.

## Exploration Runtime

Authored `SiteExplorationPointDefinition.Actions` are runtime content, not test-only data. `WorldSiteRoot` surfaces known, unresolved, in-range point actions during active exploration and applies non-battle results through `WorldSiteExplorationService.ApplyActionResult`.

Exploration point actions may reveal entrances, exploration points, tactical tags, advantages, facility slots, or cleared hazards. `StartsBattle` actions must not silently fake a battle path; they execute through the real exploration battle handoff by building a `BattleStartRequest`, applying site intel, beginning `BattleSessionHandoff`, and activating the battle runtime with rollback on activation failure.

Direct hostile site entry is allowed only when the intel view can inspect the full tactical layout. Hostile sites that require exploration must be entered through the arrived-party infiltration flow.

## Failure Rule

UI must not invent visibility fallbacks. If a site definition is missing, show a clear missing-definition message and log the failure.

If `WorldSiteDefinition.Intel` is missing, the site is treated as misconfigured restricted intel, not transparent intel. Simple or low-level transparent sites must explicitly author `Intel.Policy = Transparent`.
