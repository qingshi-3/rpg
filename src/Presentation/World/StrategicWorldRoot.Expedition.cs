using System.Collections.Generic;
using System.Linq;
using Godot;
using Rpg.Application.Battle;
using Rpg.Application.StrategicManagement;
using Rpg.Application.World;
using Rpg.Definitions.StrategicManagement;
using Rpg.Definitions.World;
using Rpg.Domain.StrategicManagement;
using Rpg.Domain.World;
using Rpg.Infrastructure.Logging;

namespace Rpg.Presentation.World;

public partial class StrategicWorldRoot
{
    private void BeginExpeditionDraft()
    {
        if (!CanStartExpeditionFromSite(_selectedSiteId, out string failureReason))
        {
            StrategicWorldRuntime.LastNotice = FormatStrategicExpeditionFailureReason(failureReason);
            RefreshAll();
            return;
        }

        _isExpeditionDrafting = true;
        _isExpeditionTargeting = false;
        _expeditionSourceSiteId = _selectedSiteId;
        _expeditionHeroIds.Clear();
        StrategicHeroCompanyViewModel defaultCompany = GetAvailableExpeditionHeroCompanies(_expeditionSourceSiteId)
            .FirstOrDefault(company => company.CanCreateExpedition);
        if (defaultCompany != null)
        {
            _expeditionHeroIds.Add(defaultCompany.HeroId);
        }

        ClampExpeditionDraftCounts();
        StrategicWorldRuntime.LastNotice = "选择出征英雄公司。英雄会带领已分配编制出征。";
        RefreshAll();
    }

    private void BeginExpeditionTargeting()
    {
        ClampExpeditionDraftCounts();
        if (!HasSelectedExpeditionUnits())
        {
            StrategicWorldRuntime.LastNotice = "请先选择要出征的英雄公司。";
            RefreshAll();
            return;
        }

        _isExpeditionTargeting = true;
        _selectedArmyIds.Clear();
        StrategicWorldRuntime.LastNotice = "选择出征目的地：左键或右键敌方地点为进攻，左键或右键己方地点为进驻，左键或右键空地为移动。";
        RefreshAll();
    }

    private void CancelExpeditionDraft()
    {
        ClearExpeditionDraftSelectionContext("cancel_button");
        StrategicWorldRuntime.LastNotice = "已取消出征。";
        RefreshAll();
    }

    private void ClearExpeditionDraftSelectionContext(string reason)
    {
        if (!_isExpeditionDrafting &&
            !_isExpeditionTargeting &&
            string.IsNullOrWhiteSpace(_expeditionSourceSiteId) &&
            _expeditionHeroIds.Count == 0)
        {
            return;
        }

        // Ordinary map selection leaves the city action context. Expedition
        // target clicks are consumed earlier by TryIssueExpeditionToTarget.
        _isExpeditionDrafting = false;
        _isExpeditionTargeting = false;
        _expeditionSourceSiteId = "";
        _expeditionHeroIds.Clear();
        GameLog.Info(nameof(StrategicWorldRoot), $"StrategicExpeditionDraftCleared reason={reason}");
    }

    private void AdjustExpeditionHeroCompanySelection(string heroId, int delta)
    {
        if (string.IsNullOrWhiteSpace(heroId))
        {
            return;
        }

        if (delta > 0)
        {
            if (_expeditionHeroIds.Count < StrategicManagementRules.FirstSliceMaxHeroCompaniesPerExpedition)
            {
                _expeditionHeroIds.Add(heroId);
            }
        }
        else if (delta < 0)
        {
            _expeditionHeroIds.Remove(heroId);
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
            StrategicWorldRuntime.LastNotice = "出征目标不能是出发地点。";
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
        IReadOnlyList<string> selectedHeroIds = BuildSelectedExpeditionHeroIds();
        if (selectedHeroIds.Count == 0)
        {
            StrategicWorldRuntime.LastNotice = "请先选择要出征的英雄公司。";
            RefreshAll();
            return true;
        }

        if (!StrategicManagementRuntime.LocationMappings.TryResolveCityIdForMapSite(_expeditionSourceSiteId, out string sourceLocationId))
        {
            StrategicWorldRuntime.LastNotice = FormatStrategicExpeditionFailureReason(StrategicFailureReasons.MissingCity);
            RefreshAll();
            return true;
        }

        if (!TryResolveStrategicExpeditionTargetLocationId(targetSiteId, intent, out string targetLocationId, out string targetFailureReason))
        {
            StrategicWorldRuntime.LastNotice = FormatStrategicExpeditionFailureReason(targetFailureReason);
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

        StrategicExpeditionIntent strategicIntent = ToStrategicExpeditionIntent(intent);
        StrategicCommandResult strategicResult = StrategicManagementRuntime.Commands.CreateExpedition(
            StrategicManagementRuntime.State,
            sourceLocationId,
            targetLocationId,
            strategicIntent,
            selectedHeroIds);
        if (!strategicResult.Success ||
            !StrategicManagementRuntime.State.Expeditions.TryGetValue(strategicResult.CreatedEntityId, out StrategicExpeditionState expedition))
        {
            StrategicWorldRuntime.LastNotice = FormatStrategicExpeditionFailureReason(strategicResult.FailureReason);
            RefreshAll();
            return true;
        }

        WorldArmyState army = _strategicExpeditionWorldArmyAdapter.CreateWorldArmy(
            StrategicManagementRuntime.Definitions,
            StrategicManagementRuntime.State,
            expedition,
            _expeditionSourceSiteId,
            targetSiteId,
            sourceArmyPosition,
            resolvedDestination,
            intent,
            State.WorldTick);
        if (army == null)
        {
            StrategicWorldRuntime.LastNotice = "战略出征无法创建地图移动对象。";
            RefreshAll();
            return true;
        }

        State.ArmyStates[army.ArmyId] = army;
        WorldArmyCommandResult commandStateResult = _armyCommandService.ApplyCreatedExpeditionCommandState(
            army,
            intent,
            expeditionPath,
            _strategicNavigationContext.Version,
            arrivalApproachOffset,
            approachDirection,
            State?.PlayerFactionId);
        if (!commandStateResult.Success)
        {
            StrategicWorldRuntime.LastNotice = WorldActionResolver.FormatFailureReason(commandStateResult.FailureReason);
            GameLog.Warn(nameof(StrategicWorldRoot), $"WorldExpeditionCommandStateRejected army={army.ArmyId} reason={commandStateResult.FailureReason}");
            RefreshAll();
            return true;
        }

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
        ClearExpeditionDraftSelectionContext("expedition_issued");
        if (expeditionNavigationDeferred)
        {
            GameLog.Info(nameof(StrategicWorldRoot), $"WorldExpeditionNavigationDeferred army={army.ArmyId} intent={intent} target={targetSiteId}");
        }

        GameLog.Info(
            nameof(StrategicWorldRoot),
            $"StrategicExpeditionIssued expedition={expedition.ExpeditionId} army={army.ArmyId} intent={intent} target={targetSiteId}");
        RefreshAll();
        return true;
    }

    private bool TryResolveStrategicExpeditionTargetLocationId(
        string targetSiteId,
        WorldArmyIntent intent,
        out string targetLocationId,
        out string failureReason)
    {
        targetLocationId = "";
        failureReason = "";
        if (intent == WorldArmyIntent.MoveToPosition)
        {
            return true;
        }

        if (!StrategicManagementRuntime.LocationMappings.TryResolveLocationIdForMapSite(targetSiteId, out targetLocationId))
        {
            failureReason = StrategicFailureReasons.MissingLocation;
            return false;
        }

        return true;
    }

    private static StrategicExpeditionIntent ToStrategicExpeditionIntent(WorldArmyIntent intent)
    {
        return intent switch
        {
            WorldArmyIntent.AssaultSite => StrategicExpeditionIntent.AssaultLocation,
            WorldArmyIntent.ReinforceSite => StrategicExpeditionIntent.ReinforceLocation,
            WorldArmyIntent.MoveToPosition => StrategicExpeditionIntent.MoveToPosition,
            _ => StrategicExpeditionIntent.Unknown
        };
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
        IReadOnlyList<StrategicHeroCompanyViewModel> companies = GetAvailableExpeditionHeroCompanies(siteId);
        if (companies.Count == 0)
        {
            failureReason = StrategicFailureReasons.HeroHasNoAssignedCorps;
            return false;
        }

        StrategicHeroCompanyViewModel firstDispatchable = companies.FirstOrDefault(company => company.CanCreateExpedition);
        if (firstDispatchable != null)
        {
            return true;
        }

        failureReason = companies.Select(company => company.DisabledReason).FirstOrDefault(reason => !string.IsNullOrWhiteSpace(reason)) ??
                        StrategicFailureReasons.MissingHero;
        return false;
    }

    private void ClampExpeditionDraftCounts()
    {
        HashSet<string> available = GetAvailableExpeditionHeroCompanies(_expeditionSourceSiteId)
            .Where(company => company.CanCreateExpedition)
            .Select(company => company.HeroId)
            .ToHashSet(System.StringComparer.Ordinal);
        foreach (string heroId in _expeditionHeroIds.ToArray())
        {
            if (!available.Contains(heroId))
            {
                _expeditionHeroIds.Remove(heroId);
            }
        }

        if (_expeditionHeroIds.Count > StrategicManagementRules.FirstSliceMaxHeroCompaniesPerExpedition)
        {
            string[] kept = _expeditionHeroIds
                .OrderBy(id => id)
                .Take(StrategicManagementRules.FirstSliceMaxHeroCompaniesPerExpedition)
                .ToArray();
            _expeditionHeroIds.Clear();
            foreach (string heroId in kept)
            {
                _expeditionHeroIds.Add(heroId);
            }
        }
    }

    private bool HasSelectedExpeditionUnits()
    {
        return _expeditionHeroIds.Count > 0;
    }

    private int GetSelectedExpeditionUnitCount()
    {
        return _expeditionHeroIds.Count;
    }

    private IReadOnlyList<StrategicHeroCompanyViewModel> GetAvailableExpeditionHeroCompanies(string siteId)
    {
        StrategicManagementRuntime.EnsureInitialized();
        if (string.IsNullOrWhiteSpace(siteId) ||
            !StrategicManagementRuntime.LocationMappings.TryResolveCityIdForMapSite(siteId, out string cityId))
        {
            return System.Array.Empty<StrategicHeroCompanyViewModel>();
        }

        StrategicManagementDashboardViewModel dashboard = StrategicManagementRuntime.BuildDashboard(
            StrategicManagementIds.FactionPlayer,
            cityId);
        return dashboard.SelectedCity.HeroCompanies;
    }

    private IReadOnlyList<string> BuildSelectedExpeditionHeroIds()
    {
        return _expeditionHeroIds
            .Where(heroId => !string.IsNullOrWhiteSpace(heroId))
            .OrderBy(heroId => heroId)
            .ToArray();
    }

    private string BuildExpeditionUnitText()
    {
        StrategicHeroCompanyViewModel[] selected = GetAvailableExpeditionHeroCompanies(_expeditionSourceSiteId)
            .Where(company => _expeditionHeroIds.Contains(company.HeroId))
            .OrderBy(company => company.HeroDisplayName)
            .ToArray();

        return selected.Length == 0
            ? "未选择英雄公司"
            : string.Join("、", selected.Select(company => $"{company.HeroDisplayName} + {company.CorpsDisplayName}"));
    }

    private static string FormatStrategicExpeditionFailureReason(string reason)
    {
        return reason switch
        {
            StrategicFailureReasons.MissingCity => "缺少可出征城市",
            StrategicFailureReasons.MissingLocation => "缺少战略地点",
            StrategicFailureReasons.MissingHero => "缺少可出征英雄",
            StrategicFailureReasons.HeroHasNoAssignedCorps => "没有已分配编制的英雄公司",
            StrategicFailureReasons.CorpsNotAssignedToHero => "编制没有分配给该英雄",
            StrategicFailureReasons.HeroAlreadyOnExpedition => "英雄已经在出征中",
            StrategicFailureReasons.CorpsAlreadyOnExpedition => "编制已经在出征中",
            StrategicFailureReasons.SourceLocationNotOwned => "出发地点不受玩家控制",
            StrategicFailureReasons.SameLocationTarget => "目标不能是出发地点",
            StrategicFailureReasons.TargetLocationNotOwned => "目标地点不受玩家控制",
            StrategicFailureReasons.TargetLocationNotAttackable => "目标地点不可攻击",
            StrategicFailureReasons.ExpeditionCapacityFull => "出征队列已满",
            StrategicFailureReasons.ExpeditionNotCommandable => "远征当前不能改派",
            StrategicFailureReasons.UnsupportedExpeditionIntent => "不支持的出征目标",
            StrategicFailureReasons.MissingBattleEntryMetadata => "目标地点缺少战斗入口配置",
            "" => "无法创建出征",
            null => "无法创建出征",
            _ => WorldActionResolver.FormatFailureReason(reason)
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
