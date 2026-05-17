using System.Collections.Generic;
using System.Linq;
using Godot;
using Rpg.Application.Battle;
using Rpg.Application.World;
using Rpg.Definitions.World;
using Rpg.Domain.World;
using Rpg.Infrastructure.Logging;
using Rpg.Presentation.Battle.Entities;
using Rpg.Presentation.Common;

namespace Rpg.Presentation.World;

public partial class StrategicWorldRoot
{
    private void ConsumeBattleResult()
    {
        if (!BattleSessionHandoff.TryConsumeLastBattleResult(out BattleStartRequest request, out BattleResult result))
        {
            return;
        }

        WorldActionResult applyResult = _battleResultApplier.Apply(State, Definition, request, result);
        StrategicWorldRuntime.LastNotice = applyResult.Message;
        _selectedSiteId = "";
        _selectedThreatId = "";
        _selectedOpportunityId = "";
    }

    private void SelectSite(string siteId)
    {
        if (string.IsNullOrWhiteSpace(siteId) || !State.SiteStates.ContainsKey(siteId))
        {
            return;
        }

        StrategicWorldDefinitionQueries queries = new(Definition);
        if (GetSiteIntelVisibility(queries.GetSite(siteId)) == WorldIntelVisibility.Unknown)
        {
            return;
        }

        _selectedSiteId = siteId;
        _selectedOpportunityId = "";
        _selectedThreatId = State.SiteStates[siteId].PendingThreatIds
            .Select(id => State.ThreatPlans.TryGetValue(id, out EnemyThreatPlan threat) ? threat : null)
            .FirstOrDefault(threat => threat?.Stage == ThreatStage.Attacking)
            ?.Id ?? "";
        RefreshAll();
    }

    private void ClearSelectedWorldDetail(bool clearNotice = false)
    {
        _selectedSiteId = "";
        _selectedThreatId = "";
        _selectedOpportunityId = "";
        if (clearNotice)
        {
            StrategicWorldRuntime.LastNotice = "";
        }
    }

    private void SelectThreat(string threatId)
    {
        if (string.IsNullOrWhiteSpace(threatId) || !State.ThreatPlans.TryGetValue(threatId, out EnemyThreatPlan threat))
        {
            return;
        }

        _selectedThreatId = threatId;
        _selectedSiteId = threat.TargetSiteId;
        _selectedOpportunityId = "";
        RefreshAll();
    }

    private bool TrySelectOpportunityAt(Vector2 screenPosition)
    {
        WorldOpportunityState opportunity = FindActiveOpportunityAt(screenPosition);
        if (opportunity == null)
        {
            return false;
        }

        SelectOpportunity(opportunity.OpportunityId);
        return true;
    }

    private void SelectOpportunity(string opportunityId)
    {
        if (string.IsNullOrWhiteSpace(opportunityId) ||
            !State.OpportunityStates.TryGetValue(opportunityId, out WorldOpportunityState opportunity) ||
            opportunity.Status != WorldOpportunityStatus.Active)
        {
            return;
        }

        StrategicWorldDefinitionQueries queries = new(Definition);
        WorldOpportunityDefinition definition = queries.GetOpportunity(opportunity.DefinitionId);
        _selectedOpportunityId = opportunityId;
        _selectedSiteId = "";
        _selectedThreatId = "";
        _selectedArmyIds.Clear();
        StrategicWorldRuntime.LastNotice = $"发现野外小场域：{definition?.DisplayName ?? opportunity.DefinitionId}。";
        RefreshAll();
    }

    private WorldOpportunityState FindActiveOpportunityAt(Vector2 screenPosition)
    {
        if (State?.OpportunityStates == null)
        {
            return null;
        }

        return State.OpportunityStates.Values
            .Where(opportunity => opportunity.Status == WorldOpportunityStatus.Active && IsMapPositionVisible(opportunity.WorldPosition))
            .OrderBy(opportunity => MapToScreen(opportunity.WorldPosition).DistanceSquaredTo(screenPosition))
            .FirstOrDefault(opportunity => MapToScreen(opportunity.WorldPosition).DistanceTo(screenPosition) <= OpportunityMarkerRadius + 10.0f);
    }

    private void OnWorldMapOverlayGuiInput(InputEvent @event)
    {
        if (TryHandleWorldCameraPointerInput(@event))
        {
            _worldMapOverlay?.AcceptEvent();
            return;
        }

        HandleWorldArmyInput(@event, eventIsViewportLocal: true);
    }

    private void HandleWorldArmyInput(InputEvent @event, bool eventIsViewportLocal = false)
    {
        if (_pendingBattleRequest != null || Definition == null || State == null)
        {
            return;
        }

        if (@event is InputEventMouseButton mouseButton)
        {
            // The world viewport owns map events, but selection/command code still
            // compares against root-screen rectangles shared with the HUD.
            Vector2 screenPosition = eventIsViewportLocal ? ToRootScreen(mouseButton.Position) : mouseButton.Position;
            if (mouseButton.ButtonIndex == MouseButton.Left)
            {
                HandleWorldArmyLeftMouse(mouseButton, screenPosition, eventIsViewportLocal);
            }
            else if (mouseButton.ButtonIndex == MouseButton.Right && mouseButton.Pressed)
            {
                if (_isExpeditionTargeting)
                {
                    TryIssueExpeditionToTarget(screenPosition);
                    AcceptWorldMapInput(eventIsViewportLocal);
                    return;
                }

                if (TryCommandSelectedArmies(screenPosition))
                {
                    AcceptWorldMapInput(eventIsViewportLocal);
                }
            }
        }
        else if (@event is InputEventMouseMotion mouseMotion && _isArmyBoxSelecting)
        {
            _armySelectionCurrentScreen = eventIsViewportLocal ? ToRootScreen(mouseMotion.Position) : mouseMotion.Position;
            QueueStrategicOverlayRedraw();
            AcceptWorldMapInput(eventIsViewportLocal);
        }
    }

    private void HandleWorldArmyLeftMouse(
        InputEventMouseButton mouseButton,
        Vector2 screenPosition,
        bool eventIsViewportLocal)
    {
        if (mouseButton.Pressed)
        {
            _isArmyBoxSelecting = true;
            _armySelectionStartScreen = screenPosition;
            _armySelectionCurrentScreen = screenPosition;
            AcceptWorldMapInput(eventIsViewportLocal);
            return;
        }

        if (!_isArmyBoxSelecting)
        {
            return;
        }

        _isArmyBoxSelecting = false;
        _armySelectionCurrentScreen = screenPosition;

        bool append = mouseButton.ShiftPressed;
        if (_armySelectionStartScreen.DistanceTo(_armySelectionCurrentScreen) <= 8.0f)
        {
            if (!_isExpeditionDrafting && TrySelectOpportunityAt(_armySelectionCurrentScreen))
            {
                AcceptWorldMapInput(eventIsViewportLocal);
                return;
            }

            if (TrySelectSiteAt(_armySelectionCurrentScreen))
            {
                AcceptWorldMapInput(eventIsViewportLocal);
                return;
            }

            SelectSingleArmyAt(_armySelectionCurrentScreen, append);
        }
        else
        {
            SelectArmiesInRect(BuildScreenRect(_armySelectionStartScreen, _armySelectionCurrentScreen), append);
        }

        RefreshAll();
        AcceptWorldMapInput(eventIsViewportLocal);
    }

    private void AcceptWorldMapInput(bool eventIsViewportLocal)
    {
        if (eventIsViewportLocal)
        {
            _worldMapOverlay?.AcceptEvent();
            return;
        }

        AcceptEvent();
    }

    private void SelectSingleArmyAt(Vector2 screenPosition, bool append)
    {
        WorldArmyState army = FindSelectableArmyAt(screenPosition);
        if (!append)
        {
            _selectedArmyIds.Clear();
        }

        if (army == null)
        {
            ClearSelectedWorldDetail();
            StrategicWorldRuntime.LastNotice = "未选中小队。";
            return;
        }

        ClearSelectedWorldDetail();
        _selectedArmyIds.Add(army.ArmyId);
        StrategicWorldRuntime.LastNotice = $"已选中小队：{BuildArmyDisplayName(army)}。";
    }

    private bool TrySelectSiteAt(Vector2 screenPosition)
    {
        WorldSiteDefinition site = FindSiteAt(screenPosition);
        if (site == null)
        {
            return false;
        }

        SelectSite(site.Id);
        return true;
    }

    private void SelectArmiesInRect(Rect2 rect, bool append)
    {
        if (!append)
        {
            _selectedArmyIds.Clear();
        }

        foreach (WorldArmyState army in State.ArmyStates.Values.Where(CanSelectWorldArmy))
        {
            if (rect.HasPoint(MapToScreen(army.WorldPosition)))
            {
                _selectedArmyIds.Add(army.ArmyId);
            }
        }

        StrategicWorldRuntime.LastNotice = _selectedArmyIds.Count == 0
            ? "未圈选到小队。"
            : $"已选中 {_selectedArmyIds.Count} 支小队。";
    }

    private bool TryCommandSelectedArmies(Vector2 screenPosition)
    {
        WorldArmyState[] selectedArmies = GetSelectedCommandableArmies();
        if (selectedArmies.Length == 0)
        {
            return false;
        }

        WorldSiteDefinition targetSite = FindSiteAt(screenPosition);
        if (targetSite != null)
        {
            return TryCommandSelectedArmiesToSite(targetSite.Id);
        }

        Vector2 mapDestination = ScreenToMap(screenPosition);
        if (!TryBuildCommandPaths(
                selectedArmies,
                mapDestination,
                out Dictionary<string, StrategicNavigationPath> commandPaths,
                out bool navigationDeferred,
                out string navigationFailureReason))
        {
            ReportWorldArmyCommandNavigationRejected("move", navigationFailureReason);
            return true;
        }

        foreach (WorldArmyState army in selectedArmies)
        {
            army.TargetSiteId = "";
            army.Destination = mapDestination;
            army.Intent = WorldArmyIntent.MoveToPosition;
            army.Status = WorldArmyStatus.Moving;
            army.ClearArrivalApproachOffset();
            army.ClearTargetApproachDirection();
            ApplyCommandNavigationPath(army, commandPaths, mapDestination);
        }

        StrategicWorldRuntime.LastNotice = $"已命令 {selectedArmies.Length} 支小队移动。";
        GameLog.Info(nameof(StrategicWorldRoot), $"WorldArmyCommandMove count={selectedArmies.Length} destination={mapDestination}");
        RefreshAll();
        return true;
    }

    private WorldArmyState[] GetSelectedCommandableArmies()
    {
        return _selectedArmyIds
            .Select(id => State.ArmyStates.TryGetValue(id, out WorldArmyState army) ? army : null)
            .Where(CanCommandWorldArmy)
            .ToArray();
    }

    private WorldArmyState FindSelectableArmyAt(Vector2 screenPosition)
    {
        return State.ArmyStates.Values
            .Where(CanSelectWorldArmy)
            .OrderBy(army => MapToScreen(army.WorldPosition).DistanceSquaredTo(screenPosition))
            .FirstOrDefault(army => MapToScreen(army.WorldPosition).DistanceTo(screenPosition) <= Mathf.Max(army.Radius + 10.0f, 24.0f));
    }

    private WorldSiteDefinition FindSiteAt(Vector2 screenPosition)
    {
        return Definition.SiteDefinitions
            .Where(site => State.SiteStates.ContainsKey(site.Id))
            .FirstOrDefault(site =>
            {
                return GetSiteHitRect(site).HasPoint(screenPosition);
            });
    }

    private static bool CanSelectWorldArmy(WorldArmyState army)
    {
        return army != null &&
               army.OwnerFactionId == StrategicWorldIds.FactionPlayer &&
               army.Status is not (WorldArmyStatus.Defeated or WorldArmyStatus.Garrisoned);
    }

    private static bool CanCommandWorldArmy(WorldArmyState army)
    {
        return CanSelectWorldArmy(army) &&
               army.Status != WorldArmyStatus.Attacking;
    }

    private string BuildArmyDisplayName(WorldArmyState army)
    {
        if (army == null)
        {
            return "未知小队";
        }

        int unitCount = army.GarrisonUnits.Sum(unit => unit.Count);
        return unitCount > 0 ? $"{army.ArmyId} ({unitCount})" : army.ArmyId;
    }

    private string ResolveSiteDisplayName(string siteId)
    {
        StrategicWorldDefinitionQueries queries = new(Definition);
        WorldSiteDefinition definition = queries.GetSite(siteId);
        return string.IsNullOrWhiteSpace(definition?.DisplayName) ? siteId : definition.DisplayName;
    }
}
