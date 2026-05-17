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
    private void BeginExpeditionDraft()
    {
        if (!CanStartExpeditionFromSite(_selectedSiteId, out string failureReason))
        {
            StrategicWorldRuntime.LastNotice = WorldActionResolver.FormatFailureReason(failureReason);
            RefreshAll();
            return;
        }

        _isExpeditionDrafting = true;
        _isExpeditionTargeting = false;
        _expeditionSourceSiteId = _selectedSiteId;
        _expeditionUnitCounts.Clear();
        // V0.1 selects a hero only; the default corps is attached after the world army exists.
        if (GetAvailableUnitCount(_expeditionSourceSiteId, HeroCorpsV0PlayableSliceIds.HeroUnit) > 0)
        {
            _expeditionUnitCounts[HeroCorpsV0PlayableSliceIds.HeroUnit] = 1;
        }
        ClampExpeditionDraftCounts();
        StrategicWorldRuntime.LastNotice = "选择出征英雄。该英雄会自动带上默认兵团。";
        RefreshAll();
    }

    private void BeginExpeditionTargeting()
    {
        ClampExpeditionDraftCounts();
        if (!HasSelectedExpeditionUnits())
        {
            StrategicWorldRuntime.LastNotice = "请先选择要出征的英雄。";
            RefreshAll();
            return;
        }

        _isExpeditionTargeting = true;
        _selectedArmyIds.Clear();
        StrategicWorldRuntime.LastNotice = "选择出征目的地：右键敌方场域为进攻，右键己方场域为进驻，右键空地为移动。";
        RefreshAll();
    }

    private void CancelExpeditionDraft()
    {
        _isExpeditionDrafting = false;
        _isExpeditionTargeting = false;
        _expeditionSourceSiteId = "";
        _expeditionUnitCounts.Clear();
        StrategicWorldRuntime.LastNotice = "已取消出征。";
        RefreshAll();
    }

    private void AdjustExpeditionUnitCount(string unitTypeId, int delta)
    {
        if (string.IsNullOrWhiteSpace(unitTypeId))
        {
            return;
        }

        int available = GetAvailableUnitCount(_expeditionSourceSiteId, unitTypeId);
        _expeditionUnitCounts.TryGetValue(unitTypeId, out int selected);
        selected = System.Math.Clamp(selected + delta, 0, available);
        if (selected <= 0)
        {
            _expeditionUnitCounts.Remove(unitTypeId);
        }
        else
        {
            _expeditionUnitCounts[unitTypeId] = selected;
        }

        RefreshAll();
    }

    private bool TryIssueExpeditionToTarget(Vector2 screenPosition)
    {
        WorldSiteDefinition targetSite = FindSiteAt(screenPosition);
        if (targetSite != null)
        {
            return TryIssueExpeditionToSite(targetSite.Id);
        }

        return TryCreateExpedition("", ScreenToMap(screenPosition), WorldArmyIntent.MoveToPosition);
    }

    private bool TryIssueExpeditionToSite(string siteId)
    {
        if (string.IsNullOrWhiteSpace(siteId) ||
            !State.SiteStates.TryGetValue(siteId, out WorldSiteState site))
        {
            return false;
        }

        StrategicWorldDefinitionQueries queries = new(Definition);
        WorldSiteDefinition siteDefinition = queries.GetSite(siteId);
        if (siteDefinition == null)
        {
            return false;
        }

        if (siteId == _expeditionSourceSiteId)
        {
            StrategicWorldRuntime.LastNotice = "出征目标不能是出发场域。";
            RefreshAll();
            return true;
        }

        if (site.OwnerFactionId == State.PlayerFactionId)
        {
            return TryCreateExpedition(siteId, siteDefinition.MapPosition, WorldArmyIntent.ReinforceSite);
        }

        if (!CanBuildAssaultBattleForSite(siteId))
        {
            StrategicWorldRuntime.LastNotice = BuildUnsupportedAssaultNotice(siteDefinition);
            RefreshAll();
            return true;
        }

        return TryCreateExpedition(siteId, siteDefinition.MapPosition, WorldArmyIntent.AssaultSite);
    }

    private bool TryCreateExpedition(string targetSiteId, Vector2 destination, WorldArmyIntent intent)
    {
        ClampExpeditionDraftCounts();
        Dictionary<string, int> units = BuildSelectedExpeditionUnits();
        if (units.Count == 0)
        {
            StrategicWorldRuntime.LastNotice = "请先选择要出征的英雄。";
            RefreshAll();
            return true;
        }

        if (!TryResolveExpeditionNavigation(
                targetSiteId,
                destination,
                out Vector2 sourceArmyPosition,
                out Vector2 resolvedDestination,
                out Vector2 arrivalApproachOffset,
                out WorldSiteAttackDirection approachDirection,
                out StrategicNavigationPath expeditionPath,
                out bool expeditionNavigationDeferred,
                out string navigationFailureReason))
        {
            ReportWorldArmyCommandNavigationRejected("expedition", navigationFailureReason);
            return true;
        }

        if (!_expeditionService.TryCreateExpedition(
                State,
                Definition,
                _expeditionSourceSiteId,
                sourceArmyPosition,
                targetSiteId,
                resolvedDestination,
                intent,
                units,
                out WorldArmyState army,
                out string failureReason))
        {
            StrategicWorldRuntime.LastNotice = WorldActionResolver.FormatFailureReason(failureReason);
            RefreshAll();
            return true;
        }

        if (expeditionPath?.Points?.Count > 0)
        {
            army.SetNavigationPath(expeditionPath.Points, army.Destination, _strategicNavigationContext.Version);
        }
        else
        {
            army.ClearNavigationPath();
        }
        if (intent == WorldArmyIntent.MoveToPosition)
        {
            army.ClearArrivalApproachOffset();
            army.ClearTargetApproachDirection();
        }
        else
        {
            army.SetArrivalApproachOffset(arrivalApproachOffset);
            army.SetTargetApproachDirection(approachDirection);
        }
        AttachDefaultCorpsToHeroExpedition(army);

        _selectedArmyIds.Clear();
        _selectedArmyIds.Add(army.ArmyId);
        string sourceName = ResolveSiteDisplayName(_expeditionSourceSiteId);
        string targetText = string.IsNullOrWhiteSpace(targetSiteId)
            ? "目标地点"
            : ResolveSiteDisplayName(targetSiteId);
        string expeditionText = BuildExpeditionUnitText();
        StrategicWorldRuntime.LastNotice = intent switch
        {
            WorldArmyIntent.AssaultSite => $"已从{sourceName}派出{expeditionText}进攻{targetText}。",
            WorldArmyIntent.ReinforceSite => $"已从{sourceName}派出{expeditionText}进驻{targetText}。",
            _ => $"已从{sourceName}派出{expeditionText}移动到目标地点。"
        };
        _isExpeditionDrafting = false;
        _isExpeditionTargeting = false;
        _expeditionSourceSiteId = "";
        _expeditionUnitCounts.Clear();
        if (expeditionNavigationDeferred)
        {
            GameLog.Info(nameof(StrategicWorldRoot), $"WorldExpeditionNavigationDeferred army={army.ArmyId} intent={intent} target={targetSiteId}");
        }

        GameLog.Info(nameof(StrategicWorldRoot), $"WorldExpeditionIssued army={army.ArmyId} intent={intent} target={targetSiteId}");
        RefreshAll();
        return true;
    }

    private static void AttachDefaultCorpsToHeroExpedition(WorldArmyState army)
    {
        if (army == null ||
            !army.GarrisonUnits.Any(unit => unit.UnitTypeId == HeroCorpsV0PlayableSliceIds.HeroUnit) ||
            army.GarrisonUnits.Any(unit => unit.UnitTypeId == HeroCorpsV0PlayableSliceIds.DefaultCorpsUnit))
        {
            return;
        }

        army.GarrisonUnits.Add(new GarrisonState
        {
            UnitTypeId = HeroCorpsV0PlayableSliceIds.DefaultCorpsUnit,
            Count = HeroCorpsV0PlayableSliceIds.DefaultCorpsCount,
            Morale = 70
        });
        GameLog.Info(
            nameof(StrategicWorldRoot),
            $"HeroDefaultCorpsAttached army={army.ArmyId} hero={HeroCorpsV0PlayableSliceIds.HeroUnit} corps={HeroCorpsV0PlayableSliceIds.DefaultCorpsUnit}:{HeroCorpsV0PlayableSliceIds.DefaultCorpsCount}");
    }

    private bool TryResolveExpeditionNavigation(
        string targetSiteId,
        Vector2 requestedDestination,
        out Vector2 sourceArmyPosition,
        out Vector2 resolvedDestination,
        out Vector2 arrivalApproachOffset,
        out WorldSiteAttackDirection approachDirection,
        out StrategicNavigationPath path,
        out bool navigationDeferred,
        out string failureReason)
    {
        sourceArmyPosition = default;
        resolvedDestination = requestedDestination;
        arrivalApproachOffset = default;
        approachDirection = WorldSiteAttackDirection.Any;
        path = null;
        navigationDeferred = false;
        failureReason = "";
        if (_strategicNavigationContext == null)
        {
            failureReason = "strategic_navigation_context_missing";
            return false;
        }

        if (!TryResolveSiteExitArmyNavigationPoint(_expeditionSourceSiteId, requestedDestination, out sourceArmyPosition, out failureReason))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(targetSiteId) &&
            !TryResolveSiteArmyNavigationPoint(targetSiteId, sourceArmyPosition, out resolvedDestination, out arrivalApproachOffset, out approachDirection, out failureReason))
        {
            return false;
        }

        return StrategicCommandNavigationService.TryBuildOrDeferPath(
            _strategicNavigationContext,
            _expeditionSourceSiteId,
            sourceArmyPosition,
            resolvedDestination,
            out path,
            out navigationDeferred,
            out failureReason);
    }

    private bool IsSiteBlockedForExpeditionTarget(string siteId)
    {
        if (!_isExpeditionTargeting ||
            string.IsNullOrWhiteSpace(siteId) ||
            !State.SiteStates.TryGetValue(siteId, out WorldSiteState site))
        {
            return false;
        }

        if (siteId == _expeditionSourceSiteId)
        {
            return true;
        }

        if (site.OwnerFactionId != State.PlayerFactionId)
        {
            return false;
        }

        StrategicWorldDefinitionQueries queries = new(Definition);
        return !_deploymentService.CanAcceptGarrison(site, queries.GetSite(siteId), GetSelectedExpeditionUnitCount(), out _);
    }

    private bool CanStartExpeditionFromSite(string siteId, out string failureReason)
    {
        failureReason = "";
        if (HasAttackingThreat())
        {
            failureReason = "attacking_threat_pending";
            return false;
        }

        if (string.IsNullOrWhiteSpace(siteId) ||
            !State.SiteStates.TryGetValue(siteId, out WorldSiteState site))
        {
            failureReason = "missing_source_site";
            return false;
        }

        if (site.OwnerFactionId != State.PlayerFactionId ||
            site.ControlState is not (SiteControlState.PlayerHeld or SiteControlState.Damaged))
        {
            failureReason = "source_site_not_owned";
            return false;
        }

        if (GetAvailableExpeditionUnitCount(siteId) <= 0)
        {
            failureReason = "no_expedition_units";
            return false;
        }

        if (GetAvailableUnitCount(siteId, HeroCorpsV0PlayableSliceIds.HeroUnit) <= 0)
        {
            failureReason = "no_expedition_hero";
            return false;
        }

        return true;
    }

    private void ClampExpeditionDraftCounts()
    {
        Dictionary<string, int> availableUnits = GetAvailableExpeditionUnits(_expeditionSourceSiteId);
        foreach (string unitTypeId in _expeditionUnitCounts.Keys.ToArray())
        {
            int available = availableUnits.TryGetValue(unitTypeId, out int count) ? count : 0;
            int selected = System.Math.Clamp(_expeditionUnitCounts[unitTypeId], 0, available);
            if (selected <= 0)
            {
                _expeditionUnitCounts.Remove(unitTypeId);
            }
            else
            {
                _expeditionUnitCounts[unitTypeId] = selected;
            }
        }
    }

    private bool HasSelectedExpeditionUnits()
    {
        return GetSelectedExpeditionUnitCount() > 0;
    }

    private int GetSelectedExpeditionUnitCount()
    {
        return _expeditionUnitCounts.Values.Sum(count => System.Math.Max(count, 0));
    }

    private int GetAvailableExpeditionUnitCount(string siteId)
    {
        return GetAvailableExpeditionUnits(siteId).Values.Sum(count => System.Math.Max(count, 0));
    }

    private Dictionary<string, int> GetAvailableExpeditionUnits(string siteId)
    {
        if (string.IsNullOrWhiteSpace(siteId) ||
            !State.SiteStates.TryGetValue(siteId, out WorldSiteState site) ||
            site.Garrison == null)
        {
            return new Dictionary<string, int>();
        }

        return site.Garrison
            .Where(unit => !string.IsNullOrWhiteSpace(unit.UnitTypeId) && unit.Count > 0)
            .Where(unit => unit.UnitTypeId == HeroCorpsV0PlayableSliceIds.HeroUnit)
            .GroupBy(unit => unit.UnitTypeId)
            .OrderBy(group => GetUnitSortKey(group.Key))
            .ThenBy(group => GetUnitLabel(group.Key))
            .ToDictionary(
                group => group.Key,
                group => group.Sum(unit => System.Math.Max(unit.Count, 0)));
    }

    private int GetAvailableUnitCount(string siteId, string unitTypeId)
    {
        return !string.IsNullOrWhiteSpace(siteId) &&
               !string.IsNullOrWhiteSpace(unitTypeId) &&
               State.SiteStates.TryGetValue(siteId, out WorldSiteState site)
            ? site.Garrison
                .Where(unit => unit.UnitTypeId == unitTypeId)
                .Sum(unit => System.Math.Max(unit.Count, 0))
            : 0;
    }

    private Dictionary<string, int> BuildSelectedExpeditionUnits()
    {
        return _expeditionUnitCounts
            .Where(item => !string.IsNullOrWhiteSpace(item.Key) && item.Value > 0)
            .ToDictionary(item => item.Key, item => item.Value);
    }

    private string BuildExpeditionUnitText()
    {
        Dictionary<string, int> selectedUnits = BuildSelectedExpeditionUnits();
        if (selectedUnits.Count > 0)
        {
            return string.Join("、", selectedUnits
                .OrderBy(item => GetUnitSortKey(item.Key))
                .ThenBy(item => GetUnitLabel(item.Key))
                .Select(item => $"{GetUnitLabel(item.Key)} x{item.Value}"));
        }

        int _expeditionHeroCount = 0;
        int _expeditionMilitiaCount = 0;
        List<string> parts = new();
        if (_expeditionHeroCount > 0)
        {
            parts.Add($"{GetUnitLabel(StrategicWorldIds.UnitPlayerKnight)} x{_expeditionHeroCount}");
        }

        if (_expeditionMilitiaCount > 0)
        {
            parts.Add($"{GetUnitLabel(StrategicWorldIds.UnitMilitia)} x{_expeditionMilitiaCount}");
        }

        return parts.Count == 0 ? "未选择单位" : string.Join("、", parts);
    }

    private static int GetUnitSortKey(string unitTypeId)
    {
        return unitTypeId switch
        {
            StrategicWorldIds.UnitPlayerKnight => 0,
            StrategicWorldIds.UnitMilitia => 10,
            _ => 20
        };
    }

    private bool IsSiteBlockedForSelectedSiteCommand(string siteId)
    {
        if (_selectedArmyIds.Count == 0 ||
            !State.SiteStates.TryGetValue(siteId, out WorldSiteState site))
        {
            return false;
        }

        WorldArmyState[] selectedArmies = GetSelectedCommandableArmies();
        if (selectedArmies.Length == 0)
        {
            return false;
        }

        if (site.OwnerFactionId != State.PlayerFactionId)
        {
            return false;
        }

        StrategicWorldDefinitionQueries queries = new(Definition);
        int incomingSlots = selectedArmies.Sum(army => _deploymentService.GetArmyGarrisonSlotUsage(army));
        return !_deploymentService.CanAcceptGarrison(site, queries.GetSite(siteId), incomingSlots, out _);
    }
}
