using System.Collections.Generic;
using System.Linq;
using Godot;
using Rpg.Application.Battle;
using Rpg.Application.Battle.Reports;
using Rpg.Application.World;
using Rpg.Definitions.Battle;
using Rpg.Definitions.World;
using Rpg.Domain.Battle.Grid;
using Rpg.Domain.World;
using Rpg.Infrastructure.Logging;
using Rpg.Presentation.Battle;
using Rpg.Presentation.Battle.Entities;
using Rpg.Presentation.Battle.Preview;
using Rpg.Presentation.Battle.Rules;
using Rpg.Presentation.Common;
using Rpg.Presentation.World;

namespace Rpg.Presentation.World.Sites;

public partial class WorldSiteRoot
{
    private bool TryHandleSiteExplorationInput(InputEvent @event)
    {
        if (TryHandleSiteExplorationHudInput(@event))
        {
            return true;
        }

        if (_battleRuntimeEnabled ||
            !string.IsNullOrWhiteSpace(_draggedPlacementId) ||
            @event is not InputEventMouseButton { Pressed: true } mouseButton ||
            IsPointerOverSiteHud(@event) ||
            IsPointerOverSiteExplorationHud(@event))
        {
            return false;
        }

        if (mouseButton.ButtonIndex == MouseButton.Right)
        {
            return TryCancelSiteExplorationMovePreview();
        }

        if (mouseButton.ButtonIndex != MouseButton.Left)
        {
            return false;
        }

        WorldSiteState site = ResolveSiteState(_siteHudSiteId);
        WorldSiteDefinition definition = ResolveSiteDefinition(_siteHudSiteId);
        if (!IsSiteExplorationActive(site, definition) ||
            !TryGetMouseGridPosition(out GridPosition destination))
        {
            return false;
        }

        EnsureSiteExplorationStateReady(site, definition);
        if (IsSiteExplorationAlertPaused(site))
        {
            StrategicWorldRuntime.LastNotice = $"巡逻单位接近：{ResolveExplorationPatrolName(definition, site.Exploration.ActiveAlertPatrolId)}。";
            SetSiteNoticeText(StrategicWorldRuntime.LastNotice);
            RefreshSiteExplorationHud(site, definition);
            GetViewport().SetInputAsHandled();
            return true;
        }

        if (!_activeGridMap.TryGetTopSurfacePosition(destination, out GridSurfacePosition target) ||
            (target.X == site.Exploration.CurrentCellX &&
             target.Y == site.Exploration.CurrentCellY &&
             target.Height == site.Exploration.CurrentCellHeight))
        {
            ClearSiteExplorationMovePreview(site);
            GetViewport().SetInputAsHandled();
            return true;
        }

        string destinationKey = WorldSiteExplorationService.ToCellKey(target);
        if (IsConfirmingSiteExplorationMove(site, destinationKey))
        {
            site.Exploration.IsSimulationPaused = false;
            site.Exploration.PauseReason = "";
            site.Exploration.ActiveAlertPatrolId = "";
            ClearSiteExplorationPathPreview();
            AdvanceSiteExplorationAction(site, definition, waitAction: false);
            GetViewport().SetInputAsHandled();
            return true;
        }

        if (!WorldSiteExplorationService.TryBuildPartyPath(
                site.Exploration,
                definition,
                _activeGridMap,
                destination,
                out IReadOnlyList<GridSurfacePosition> path,
                out string failureReason))
        {
            StrategicWorldRuntime.LastNotice = $"探索移动失败：{failureReason}";
            SetSiteNoticeText(StrategicWorldRuntime.LastNotice);
            ClearSiteExplorationMovePreview(site);
            GetViewport().SetInputAsHandled();
            return true;
        }

        _selectedPlacementId = "";
        _selectedFacilitySlotId = "";
        site.Exploration.IsSimulationPaused = true;
        site.Exploration.PauseReason = SiteExplorationPauseMovePreview;
        site.Exploration.ActiveAlertPatrolId = "";
        SetSiteExplorationPendingPath(site, path);
        ShowSiteExplorationPathPreview(path);
        StrategicWorldRuntime.LastNotice = path.Count > 1
            ? $"探索移动已规划：{path.Count - 1} 格。再次点击目标格确认，右键取消。"
            : "探索队伍已在目标位置。";
        // Valid exploration clicks should not rebuild site-management UI; rebuilding recreates/binds unit presentation and resets animations.
        SetSiteNoticeText(StrategicWorldRuntime.LastNotice);
        GameLog.Info(nameof(WorldSiteRoot), $"Site exploration move intent site={site.SiteId} destination={destination} pathCells={path.Count}");
        GetViewport().SetInputAsHandled();
        return true;
    }

    private bool TryHandleSiteExplorationHudInput(InputEvent @event)
    {
        if (@event is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } mouseButton ||
            _siteExplorationHud?.Visible != true)
        {
            return false;
        }

        Vector2 screenPosition = mouseButton.Position;
        if (TryDispatchSiteExplorationButtonAt(_siteExplorationEngageButton, screenPosition, "engage", OnSiteExplorationEngagePressed) ||
            TryDispatchSiteExplorationButtonAt(_siteExplorationRetreatButton, screenPosition, "retreat", OnSiteExplorationRetreatPressed) ||
            TryDispatchSiteExplorationButtonAt(_siteExplorationWaitButton, screenPosition, "wait", OnSiteExplorationWaitPressed))
        {
            GetViewport().SetInputAsHandled();
            return true;
        }

        return false;
    }

    private bool TryDispatchSiteExplorationButtonAt(Button button, Vector2 screenPosition, string role, System.Action pressed)
    {
        if (button?.Visible != true ||
            button.Disabled ||
            !IsScreenPointInsideControl(button, screenPosition))
        {
            return false;
        }

        DispatchSiteExplorationButton(role, pressed, "manual_hit");
        return true;
    }

    private static bool IsConfirmingSiteExplorationMove(WorldSiteState site, string destinationKey)
    {
        // PendingPathCellKeys is reused after confirmation for automatic step-by-step execution.
        // Only the explicit preview pause state is confirmable; a moving path must not accept a second click as a new confirmation.
        return site?.Exploration?.PendingPathCellKeys?.Count > 0 &&
               site.Exploration.IsSimulationPaused &&
               site.Exploration.PauseReason == SiteExplorationPauseMovePreview &&
               !string.IsNullOrWhiteSpace(destinationKey) &&
               site.Exploration.PendingPathCellKeys[^1] == destinationKey;
    }

    private static void SetSiteExplorationPendingPath(WorldSiteState site, IReadOnlyList<GridSurfacePosition> path)
    {
        if (site?.Exploration == null)
        {
            return;
        }

        site.Exploration.PendingPathCellKeys.Clear();
        if (path == null)
        {
            return;
        }

        foreach (GridSurfacePosition cell in path.Skip(1))
        {
            site.Exploration.PendingPathCellKeys.Add(WorldSiteExplorationService.ToCellKey(cell));
        }
    }

    private void EnterSiteAlertModeForVisit(WorldSiteState site, string armyId)
    {
        if (site == null)
        {
            return;
        }

        // Scene entry projects an already-arrived army into the site; the application service owns the mode transition.
        _siteModeTransitions.EnterExploration(
            site,
            StrategicWorldRuntime.State?.WorldTick ?? site.LastModeChangedTick,
            "infiltration_visit",
            armyId);
    }

    private static void LogSiteUnitState(string phase, WorldSiteState site, string armyId = "")
    {
        GameLog.Info(
            nameof(WorldSiteRoot),
            $"{phase} site={site?.SiteId ?? ""} mode={site?.SiteMode.ToString() ?? ""} army={armyId ?? ""} placements={FormatSitePlacementsForLog(site)}");
    }

    private static string FormatSitePlacementsForLog(WorldSiteState site)
    {
        return site?.UnitPlacements == null
            ? "none"
            : string.Join(
                "|",
                site.UnitPlacements
                    .OrderBy(placement => placement.PlacementId)
                    .Select(placement =>
                        $"{placement.PlacementId}[unit={placement.UnitTypeId},kind={placement.PlacementKind},source={placement.SourceKind}:{placement.SourceId},army={placement.ArmyId},faction={placement.FactionId},cell={placement.CellX}:{placement.CellY}:{placement.CellHeight}]"));
    }

    private static string FormatArmyUnitsForLog(WorldArmyState army)
    {
        return army?.GarrisonUnits == null
            ? "none"
            : string.Join(",", army.GarrisonUnits.Where(unit => unit != null).Select(unit => $"{unit.UnitTypeId}:{unit.Count}"));
    }

    private static string FormatForcesForLog(IEnumerable<BattleForceRequest> forces)
    {
        return forces == null
            ? "none"
            : string.Join(
                "|",
                forces.Select(force =>
                    $"{force.ForceId}[unit={force.UnitDefinitionId},count={force.Count},source={force.SourceKind}:{force.SourceId},faction={force.FactionId},placements={FormatForcePlacementsForLog(force)}]"));
    }

    private static string FormatForcePlacementsForLog(BattleForceRequest force)
    {
        return force?.PreferredPlacements == null
            ? "none"
            : string.Join(",", force.PreferredPlacements.Select(placement => $"{placement.PlacementId}@{placement.CellX}:{placement.CellY}:{placement.CellHeight}"));
    }

    private static string FormatBattleForceResultsForLog(IEnumerable<BattleForceResult> results)
    {
        return results == null
            ? "none"
            : string.Join(
                "|",
                results.Select(result =>
                    $"{result.ForceId}[unit={result.UnitDefinitionId},source={result.SourceKind}:{result.SourceId},initial={result.InitialCount},survived={result.SurvivedCount},defeated={result.DefeatedCount}]"));
    }

    private bool TryCancelSiteExplorationMovePreview()
    {
        WorldSiteState site = ResolveSiteState(_siteHudSiteId);
        if (site?.Exploration == null || site.Exploration.PendingPathCellKeys.Count == 0)
        {
            return false;
        }

        ClearSiteExplorationMovePreview(site);
        StrategicWorldRuntime.LastNotice = "探索移动已取消。";
        SetSiteNoticeText(StrategicWorldRuntime.LastNotice);
        GetViewport().SetInputAsHandled();
        return true;
    }

    private void ClearSiteExplorationMovePreview(WorldSiteState site)
    {
        if (site?.Exploration != null &&
            site.Exploration.PauseReason == SiteExplorationPauseMovePreview)
        {
            site.Exploration.PendingPathCellKeys.Clear();
            site.Exploration.IsSimulationPaused = true;
            site.Exploration.PauseReason = SiteExplorationPauseReady;
            site.Exploration.ActiveAlertPatrolId = "";
        }

        ClearSiteExplorationPathPreview();
    }

    private void ShowSiteExplorationPathPreview(IReadOnlyList<GridSurfacePosition> path)
    {
        // BattleGridHighlightOverlay.SetPath owns start-cell filtering and arrow drawing.
        // Passing the full path keeps one-cell movement visible instead of double-skipping the target cell.
        _highlightOverlay?.SetPath(
            path?.Select(cell => new GridPosition(cell.X, cell.Y)) ?? System.Array.Empty<GridPosition>());
    }

    private void ClearSiteExplorationPathPreview()
    {
        _highlightOverlay?.ClearCells(BattleGridHighlightKind.Path);
    }

    private void ContinueConfirmedSiteExplorationMoveIfReady()
    {
        WorldSiteState site = ResolveSiteState(_siteHudSiteId);
        WorldSiteDefinition definition = ResolveSiteDefinition(_siteHudSiteId);
        if (!IsSiteExplorationActive(site, definition))
        {
            return;
        }

        EnsureSiteExplorationStateReady(site, definition);
        if (_unitRoot?.HasActiveMovementTweens == true)
        {
            return;
        }

        if (site.Exploration.IsSimulationPaused || site.Exploration.PendingPathCellKeys.Count == 0)
        {
            return;
        }

        AdvanceSiteExplorationAction(site, definition, waitAction: false);
    }

    private void AdvanceSiteExplorationAction(WorldSiteState site, WorldSiteDefinition definition, bool waitAction)
    {
        if (!IsSiteExplorationActive(site, definition) ||
            _unitRoot?.HasActiveMovementTweens == true)
        {
            return;
        }

        SiteExplorationTickResult result = WorldSiteExplorationService.AdvanceTick(
            site.Exploration,
            definition,
            _activeGridMap,
            waitAction: waitAction);
        PresentSiteExplorationTickResult(site, result);
        RefreshSiteExplorationAlertRangePresentation(site, definition);
        if (result.Paused && result.PauseReason == SiteExplorationPauseAlertRadius)
        {
            site.Exploration.PendingPathCellKeys.Clear();
            ClearSiteExplorationPathPreview();
            StrategicWorldRuntime.LastNotice = $"巡逻单位接近：{ResolveExplorationPatrolName(definition, result.AlertPatrolId)}。";
            SetSiteNoticeText(StrategicWorldRuntime.LastNotice);
        }
        RefreshSiteExplorationHud(site, definition);
    }

    private bool EnsureVisitingArmyPlacement(WorldSiteState site, WorldSiteDefinition definition, string armyId)
    {
        if (site == null ||
            definition == null ||
            string.IsNullOrWhiteSpace(armyId) ||
            StrategicWorldRuntime.State?.ArmyStates.TryGetValue(armyId, out WorldArmyState army) != true)
        {
            return false;
        }

        if (_deploymentCache == null || _deploymentCache.SiteId != site.SiteId)
        {
            RebuildSiteDeploymentRuntimeCache(site.SiteId);
        }

        bool createdAny = false;
        bool placedAny = false;
        WorldSiteUnitPlacement refreshedPartyPlacement = null;
        int unitIndex = 0;
        foreach (GarrisonState unit in army.GarrisonUnits.Where(item => item.Count > 0 && !string.IsNullOrWhiteSpace(item.UnitTypeId)))
        {
            for (int count = 0; count < unit.Count; count++)
            {
                unitIndex++;
                string placementId = BuildVisitingArmyPlacementId(army.ArmyId, unit.UnitTypeId, unitIndex);
                bool canEnterWater = _battleUnitFactory.TryGetUnitDefinition(unit.UnitTypeId, out BattleUnitDefinition unitDefinition) &&
                                     unitDefinition.CanEnterWater;
                if (!TryResolveKnownPlayerEntranceDeploymentCandidate(
                        site,
                        definition,
                        army.TargetApproachDirection,
                        canEnterWater,
                        out WorldSiteDeploymentCell candidate,
                        out WorldSiteAttackDirection direction,
                        out string entranceId))
                {
                    GameLog.Warn(nameof(WorldSiteRoot), $"VisitingArmyPlacementSkipped site={site.SiteId} army={army.ArmyId} unit={unit.UnitTypeId} reason=known_player_entrance_deployment_candidate_missing targetDirection={army.TargetApproachDirection}");
                    continue;
                }

                WorldSiteUnitPlacement existing = site.UnitPlacements.FirstOrDefault(item => item.PlacementId == placementId);
                if (existing != null)
                {
                    ApplyVisitingArmyPlacementMetadata(existing, army, unit.UnitTypeId, unitIndex, direction, entranceId);
                    existing.CellX = candidate.Cell.X;
                    existing.CellY = candidate.Cell.Y;
                    existing.CellHeight = candidate.Height;
                    placedAny = true;
                    if (refreshedPartyPlacement == null || existing.UnitIndex < refreshedPartyPlacement.UnitIndex)
                    {
                        refreshedPartyPlacement = existing;
                    }
                    continue;
                }

                WorldSiteUnitPlacement placement = new()
                {
                    PlacementId = placementId,
                    UnitTypeId = unit.UnitTypeId,
                    UnitIndex = unitIndex,
                    FactionId = string.IsNullOrWhiteSpace(army.OwnerFactionId) ? StrategicWorldIds.FactionPlayer : army.OwnerFactionId,
                    PlacementKind = WorldSiteUnitPlacementKind.VisitingArmy,
                    SourceKind = "PlayerArmy",
                    SourceId = army.ArmyId,
                    ArmyId = army.ArmyId,
                    EntranceId = entranceId,
                    AttackDirection = direction,
                    CellX = candidate.Cell.X,
                    CellY = candidate.Cell.Y,
                    CellHeight = candidate.Height
                };
                site.UnitPlacements.Add(placement);
                createdAny = true;
                placedAny = true;
                if (refreshedPartyPlacement == null || placement.UnitIndex < refreshedPartyPlacement.UnitIndex)
                {
                    refreshedPartyPlacement = placement;
                }
            }
        }

        WorldSiteUnitPlacement partyPlacement = refreshedPartyPlacement ?? ResolveSiteExplorationPartyPlacement(site);
        if (partyPlacement != null &&
            (ReferenceEquals(partyPlacement, refreshedPartyPlacement) ||
             IsKnownPlayerEntrancePlacement(site, definition, partyPlacement)))
        {
            site.Exploration.CurrentCellX = partyPlacement.CellX;
            site.Exploration.CurrentCellY = partyPlacement.CellY;
            site.Exploration.CurrentCellHeight = partyPlacement.CellHeight;
            site.Exploration.IsSimulationPaused = true;
            site.Exploration.PauseReason = SiteExplorationPauseReady;
        }
        else if (partyPlacement != null)
        {
            GameLog.Warn(
                nameof(WorldSiteRoot),
                $"VisitingArmyExplorationCellCopySkipped site={site.SiteId} army={army.ArmyId} placement={partyPlacement.PlacementId} reason=stale_or_unknown_entrance");
        }

        if (createdAny)
        {
            GameLog.Info(nameof(WorldSiteRoot), $"VisitingArmyPlacementEnsured site={site.SiteId} army={army.ArmyId} placements={unitIndex} armyUnits={FormatArmyUnitsForLog(army)} sitePlacements={FormatSitePlacementsForLog(site)}");
        }

        return placedAny;
    }

    private static string BuildVisitingArmyPlacementId(string armyId, string unitTypeId, int index)
    {
        return $"site_army:PlayerArmy:{armyId}:{unitTypeId}:{index}";
    }

    private static void ApplyVisitingArmyPlacementMetadata(
        WorldSiteUnitPlacement placement,
        WorldArmyState army,
        string unitTypeId,
        int index,
        WorldSiteAttackDirection direction,
        string entranceId)
    {
        placement.UnitTypeId = unitTypeId;
        placement.UnitIndex = index;
        placement.FactionId = string.IsNullOrWhiteSpace(army.OwnerFactionId) ? StrategicWorldIds.FactionPlayer : army.OwnerFactionId;
        placement.PlacementKind = WorldSiteUnitPlacementKind.VisitingArmy;
        placement.SourceKind = "PlayerArmy";
        placement.SourceId = army.ArmyId;
        placement.ArmyId = army.ArmyId;
        placement.EntranceId = entranceId ?? "";
        placement.AttackDirection = direction;
    }

    private static bool IsPlayerArmySitePlacement(WorldSiteUnitPlacement placement)
    {
        return placement != null &&
               placement.SourceKind == "PlayerArmy" &&
               !string.IsNullOrWhiteSpace(placement.ArmyId) &&
               placement.PlacementKind is WorldSiteUnitPlacementKind.VisitingArmy or WorldSiteUnitPlacementKind.Attacker;
    }

    private WorldSiteUnitPlacement ResolveSiteExplorationPartyPlacement(WorldSiteState site)
    {
        return site?.UnitPlacements
            .Where(IsPlayerArmySitePlacement)
            .OrderBy(placement => placement.UnitIndex)
            .ThenBy(placement => placement.PlacementId)
            .FirstOrDefault();
    }

    private void SyncSiteExplorationPartyPlacement(WorldSiteState site)
    {
        WorldSiteUnitPlacement placement = ResolveSiteExplorationPartyPlacement(site);
        if (placement == null || site?.Exploration == null)
        {
            return;
        }

        placement.CellX = site.Exploration.CurrentCellX;
        placement.CellY = site.Exploration.CurrentCellY;
        placement.CellHeight = site.Exploration.CurrentCellHeight;
    }
}
