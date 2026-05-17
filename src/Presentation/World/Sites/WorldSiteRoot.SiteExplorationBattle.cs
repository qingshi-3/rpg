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
    private void OnSiteExplorationWaitPressed()
    {
        WorldSiteState site = ResolveSiteState(_siteHudSiteId);
        WorldSiteDefinition definition = ResolveSiteDefinition(_siteHudSiteId);
        if (!IsSiteExplorationActive(site, definition) ||
            IsSiteExplorationAlertPaused(site))
        {
            return;
        }

        site.Exploration.IsSimulationPaused = false;
        site.Exploration.PauseReason = "";
        site.Exploration.ActiveAlertPatrolId = "";
        site.Exploration.PendingPathCellKeys.Clear();
        ClearSiteExplorationPathPreview();
        AdvanceSiteExplorationAction(site, definition, waitAction: true);
    }

    private void OnSiteExplorationEngagePressed()
    {
        WorldSiteState site = ResolveSiteState(_siteHudSiteId);
        if (!IsSiteExplorationAlertPaused(site))
        {
            StrategicWorldRuntime.LastNotice = "当前没有可进入的探索遭遇。";
            SetSiteNoticeText(StrategicWorldRuntime.LastNotice);
            GameLog.Warn(
                nameof(WorldSiteRoot),
                $"Exploration engage ignored site={_siteHudSiteId} hasSite={site != null} pause={site?.Exploration?.PauseReason ?? ""} patrol={site?.Exploration?.ActiveAlertPatrolId ?? ""}");
            return;
        }

        GameLog.Info(nameof(WorldSiteRoot), $"Exploration engage requested site={site.SiteId} patrol={site.Exploration.ActiveAlertPatrolId}");
        RequestSiteExplorationBattle(site);
    }

    private void OnSiteExplorationRetreatPressed()
    {
        WorldSiteState site = ResolveSiteState(_siteHudSiteId);
        if (!IsSiteExplorationAlertPaused(site))
        {
            StrategicWorldRuntime.LastNotice = "当前没有需要撤退的探索遭遇。";
            SetSiteNoticeText(StrategicWorldRuntime.LastNotice);
            GameLog.Warn(
                nameof(WorldSiteRoot),
                $"Exploration retreat ignored site={_siteHudSiteId} hasSite={site != null} pause={site?.Exploration?.PauseReason ?? ""} patrol={site?.Exploration?.ActiveAlertPatrolId ?? ""}");
            return;
        }

        GameLog.Info(nameof(WorldSiteRoot), $"Exploration retreat requested site={site.SiteId} patrol={site.Exploration.ActiveAlertPatrolId}");
        RetreatFromSiteExplorationAlert(site);
    }

    private void RequestSiteExplorationPointBattle(
        WorldSiteState site,
        WorldSiteDefinition definition,
        SiteExplorationPointDefinition point,
        SiteExplorationActionDefinition action)
    {
        if (!IsSiteExplorationActive(site, definition) || point == null || action == null)
        {
            StrategicWorldRuntime.LastNotice = "无法进入探索点遭遇战：探索状态已失效。";
            RefreshSiteManagementUi(StrategicWorldRuntime.LastNotice);
            GameLog.Warn(nameof(WorldSiteRoot), $"Cannot enter exploration point battle site={site?.SiteId ?? ""} point={point?.Id ?? ""} action={action?.Id ?? ""} reason=exploration_context_missing");
            return;
        }

        WorldSiteUnitPlacement partyPlacement = ResolveSiteExplorationPartyPlacement(site);
        if (partyPlacement == null ||
            string.IsNullOrWhiteSpace(partyPlacement.ArmyId) ||
            StrategicWorldRuntime.State?.ArmyStates.TryGetValue(partyPlacement.ArmyId, out WorldArmyState army) != true ||
            army.GarrisonUnits.Sum(unit => System.Math.Max(0, unit.Count)) <= 0)
        {
            StrategicWorldRuntime.LastNotice = "无法进入探索点遭遇战：缺少潜入部队。";
            RefreshSiteManagementUi(StrategicWorldRuntime.LastNotice);
            GameLog.Warn(nameof(WorldSiteRoot), $"Cannot enter exploration point battle site={site.SiteId} point={point.Id} action={action.Id} reason=exploration_army_missing placement={partyPlacement?.PlacementId ?? ""}");
            return;
        }

        SiteExplorationPatrolDefinition[] encounterPatrols = ResolveAliveExplorationEncounterPatrols(site, definition);
        GridSurfacePosition entryCell = new(site.Exploration.CurrentCellX, site.Exploration.CurrentCellY, site.Exploration.CurrentCellHeight);
        int alertLevel = System.Math.Clamp(site.Exploration.AlertLevel + action.AlertDelta, 0, 5);
        string encounterId = string.IsNullOrWhiteSpace(action.BattleEncounterId) ? action.Id : action.BattleEncounterId;
        BattleStartRequest request = WorldSiteExplorationService.BuildExplorationBattleRequest(
            site.SiteId,
            point.Id,
            "",
            army,
            encounterPatrols,
            entryCell,
            alertLevel,
            string.IsNullOrWhiteSpace(_siteHudReturnScenePath) ? "res://scenes/world/StrategicWorldRoot.tscn" : _siteHudReturnScenePath,
            string.IsNullOrWhiteSpace(SceneFilePath) ? "res://scenes/world/sites/WorldSiteRoot.tscn" : SceneFilePath,
            encounterId);
        WorldSiteIntelService.ApplySiteIntelToRequest(
            StrategicWorldRuntime.State,
            StrategicWorldRuntime.Definition,
            request,
            site.SiteId);
        if (request.PlayerForces.Count == 0 || request.EnemyForces.Count == 0)
        {
            StrategicWorldRuntime.LastNotice = "无法进入探索点遭遇战：参战单位不完整。";
            RefreshSiteManagementUi(StrategicWorldRuntime.LastNotice);
            GameLog.Warn(
                nameof(WorldSiteRoot),
                $"Cannot enter exploration point battle site={site.SiteId} point={point.Id} action={action.Id} reason=forces_missing playerForces={request.PlayerForces.Count} enemyForces={request.EnemyForces.Count} patrols={encounterPatrols.Length}");
            return;
        }

        GameLog.Info(
            nameof(WorldSiteRoot),
            $"ExplorationPointBattleRequested site={site.SiteId} point={point.Id} action={action.Id} encounter={request.EncounterId} army={army.ArmyId} alert={alertLevel} playerForces={FormatForcesForLog(request.PlayerForces)} enemyForces={FormatForcesForLog(request.EnemyForces)}");

        WorldSiteBattleLaunchRollback rollback = _battleLauncher.CaptureRollback(site);
        _siteModeTransitions.EnterBattleFromExploration(
            site,
            StrategicWorldRuntime.State?.WorldTick ?? site.LastModeChangedTick,
            "exploration_point_battle_requested",
            request.RequestId);
        WorldSiteBattleLaunchResult launch = _battleLauncher.BeginAndActivate(
            StrategicWorldRuntime.State,
            request,
            rollback,
            ApplyBattleStartRequest,
            ActivateBattleRuntime,
            () => _battleStartBlockedReason,
            ClearBattleEntities,
            null,
            enabled => SetBattleRuntimeEnabled(enabled));
        if (!launch.Success)
        {
            StrategicWorldRuntime.LastNotice = "无法进入探索点遭遇战。";
            RefreshSiteManagementUi(StrategicWorldRuntime.LastNotice);
            GameLog.Warn(nameof(WorldSiteRoot), $"Cannot enter exploration point battle site={site.SiteId} point={point.Id} action={action.Id} reason={launch.FailureReason}");
        }
    }

    private static SiteExplorationPatrolDefinition[] ResolveAliveExplorationEncounterPatrols(
        WorldSiteState site,
        WorldSiteDefinition definition)
    {
        if (site?.Exploration == null || definition?.ExplorationPatrols == null)
        {
            return System.Array.Empty<SiteExplorationPatrolDefinition>();
        }

        SiteExplorationPatrolDefinition[] patrols = definition.ExplorationPatrols
            .Where(patrol =>
                patrol != null &&
                WorldSiteExplorationService.HasAlivePatrolPlacement(site, patrol) &&
                !site.Exploration.PatrolUnits.Any(state => state.PatrolId == patrol.Id && state.IsRemoved))
            .ToArray();
        foreach (SiteExplorationPatrolDefinition patrol in patrols)
        {
            SiteExplorationPatrolState patrolState = site.Exploration.PatrolUnits.FirstOrDefault(item => item.PatrolId == patrol.Id);
            WorldSiteUnitPlacement patrolPlacement = site.UnitPlacements.FirstOrDefault(item =>
                item.PlacementId == patrol.SourcePlacementId &&
                item.UnitTypeId == patrol.UnitTypeId);
            if (patrolState == null || patrolPlacement == null)
            {
                continue;
            }

            patrolPlacement.CellX = patrolState.CellX;
            patrolPlacement.CellY = patrolState.CellY;
            patrolPlacement.CellHeight = patrolState.CellHeight;
        }

        return patrols;
    }

    private void RequestSiteExplorationBattle(WorldSiteState site)
    {
        WorldSiteDefinition definition = ResolveSiteDefinition(site?.SiteId);
        if (!IsSiteExplorationActive(site, definition))
        {
            StrategicWorldRuntime.LastNotice = "探索状态已失效，无法进入遭遇战。";
            RefreshSiteManagementUi(StrategicWorldRuntime.LastNotice);
            return;
        }

        string patrolId = site.Exploration.ActiveAlertPatrolId;
        SiteExplorationPatrolDefinition patrolDefinition = definition.ExplorationPatrols.FirstOrDefault(item => item.Id == patrolId);
        if (patrolDefinition == null)
        {
            StrategicWorldRuntime.LastNotice = "无法进入探索遭遇战：缺少触发巡逻配置。";
            RefreshSiteManagementUi(StrategicWorldRuntime.LastNotice);
            GameLog.Warn(nameof(WorldSiteRoot), $"Cannot enter exploration battle site={site.SiteId} patrol={patrolId} reason=patrol_definition_missing");
            return;
        }

        SiteExplorationPatrolState patrolState = site.Exploration.PatrolUnits.FirstOrDefault(item => item.PatrolId == patrolId);
        WorldSiteUnitPlacement patrolPlacement = site.UnitPlacements.FirstOrDefault(item =>
            item.PlacementId == patrolDefinition.SourcePlacementId &&
            item.UnitTypeId == patrolDefinition.UnitTypeId);
        if (patrolState == null || patrolPlacement == null)
        {
            StrategicWorldRuntime.LastNotice = "无法进入探索遭遇战：触发巡逻单位不存在。";
            RefreshSiteManagementUi(StrategicWorldRuntime.LastNotice);
            GameLog.Warn(
                nameof(WorldSiteRoot),
                $"Cannot enter exploration battle site={site.SiteId} patrol={patrolId} reason=patrol_source_missing placement={patrolDefinition.SourcePlacementId} unit={patrolDefinition.UnitTypeId}");
            return;
        }

        // The patrol's source placement is the battle identity authority; sync its current exploration cell
        // before handoff so battle deployment does not use a stale garrison position.
        patrolPlacement.CellX = patrolState.CellX;
        patrolPlacement.CellY = patrolState.CellY;
        patrolPlacement.CellHeight = patrolState.CellHeight;

        WorldSiteUnitPlacement partyPlacement = ResolveSiteExplorationPartyPlacement(site);
        if (partyPlacement == null ||
            string.IsNullOrWhiteSpace(partyPlacement.ArmyId) ||
            StrategicWorldRuntime.State?.ArmyStates.TryGetValue(partyPlacement.ArmyId, out WorldArmyState army) != true ||
            army.GarrisonUnits.Sum(unit => System.Math.Max(0, unit.Count)) <= 0)
        {
            StrategicWorldRuntime.LastNotice = "无法进入探索遭遇战：缺少潜入部队。";
            RefreshSiteManagementUi(StrategicWorldRuntime.LastNotice);
            GameLog.Warn(nameof(WorldSiteRoot), $"Cannot enter exploration battle site={site.SiteId} patrol={patrolId} reason=exploration_army_missing placement={partyPlacement?.PlacementId ?? ""}");
            return;
        }

        GridSurfacePosition entryCell = new(site.Exploration.CurrentCellX, site.Exploration.CurrentCellY, site.Exploration.CurrentCellHeight);
        SiteExplorationPatrolDefinition[] encounterPatrols = definition.ExplorationPatrols
            .Where(patrol => patrol != null && WorldSiteExplorationService.HasAlivePatrolPlacement(site, patrol))
            .ToArray();
        BattleStartRequest request = WorldSiteExplorationService.BuildExplorationBattleRequest(
            site.SiteId,
            $"patrol:{patrolId}",
            patrolId,
            army,
            encounterPatrols,
            entryCell,
            site.Exploration.AlertLevel,
            string.IsNullOrWhiteSpace(_siteHudReturnScenePath) ? "res://scenes/world/StrategicWorldRoot.tscn" : _siteHudReturnScenePath,
            string.IsNullOrWhiteSpace(SceneFilePath) ? "res://scenes/world/sites/WorldSiteRoot.tscn" : SceneFilePath);
        WorldSiteIntelService.ApplySiteIntelToRequest(
            StrategicWorldRuntime.State,
            StrategicWorldRuntime.Definition,
            request,
            site.SiteId);
        if (request.PlayerForces.Count == 0 || request.EnemyForces.Count == 0)
        {
            StrategicWorldRuntime.LastNotice = "无法进入探索遭遇战：参战单位不完整。";
            RefreshSiteManagementUi(StrategicWorldRuntime.LastNotice);
            GameLog.Warn(
                nameof(WorldSiteRoot),
                $"Cannot enter exploration battle site={site.SiteId} patrol={patrolId} reason=forces_missing playerForces={request.PlayerForces.Count} enemyForces={request.EnemyForces.Count}");
            return;
        }

        GameLog.Info(
            nameof(WorldSiteRoot),
            $"ExplorationBattleRequested site={site.SiteId} army={army.ArmyId} armyUnits={FormatArmyUnitsForLog(army)} patrol={patrolId} patrolPlacement={patrolDefinition.SourcePlacementId} playerForces={FormatForcesForLog(request.PlayerForces)} enemyForces={FormatForcesForLog(request.EnemyForces)} sitePlacements={FormatSitePlacementsForLog(site)}");

        WorldSiteBattleLaunchRollback rollback = _battleLauncher.CaptureRollback(site);
        // Exploration confirmation only changes the site runtime mode; auto battle activation starts after the request is accepted.
        _siteModeTransitions.EnterBattleFromExploration(
            site,
            StrategicWorldRuntime.State?.WorldTick ?? site.LastModeChangedTick,
            "exploration_battle_requested",
            request.RequestId);
        WorldSiteBattleLaunchResult launch = _battleLauncher.BeginAndActivate(
            StrategicWorldRuntime.State,
            request,
            rollback,
            ApplyBattleStartRequest,
            ActivateBattleRuntime,
            () => _battleStartBlockedReason,
            ClearBattleEntities,
            null,
            enabled => SetBattleRuntimeEnabled(enabled));
        if (!launch.Success)
        {
            StrategicWorldRuntime.LastNotice = "无法进入探索遭遇战。";
            RefreshSiteManagementUi(StrategicWorldRuntime.LastNotice);
            GameLog.Warn(nameof(WorldSiteRoot), $"Cannot enter exploration battle site={site.SiteId} patrol={patrolId} reason={launch.FailureReason}");
        }
    }

    private void RetreatFromSiteExplorationAlert(WorldSiteState site)
    {
        if (site?.Exploration == null)
        {
            return;
        }

        ClearSiteExplorationPathPreview();
        _siteModeTransitions.RetreatFromExplorationAlert(
            site,
            StrategicWorldRuntime.State?.WorldTick ?? site.LastModeChangedTick,
            site.Exploration.ActiveAlertPatrolId);
        StrategicWorldRuntime.LastNotice = "探索队伍撤退并保持警戒。";
        SetSiteNoticeText(StrategicWorldRuntime.LastNotice);
        RefreshSiteExplorationHud(site, ResolveSiteDefinition(site.SiteId));
    }

    private static bool IsSiteExplorationAlertPaused(WorldSiteState site)
    {
        return site?.Exploration != null &&
               site.Exploration.PauseReason == SiteExplorationPauseAlertRadius &&
               !string.IsNullOrWhiteSpace(site.Exploration.ActiveAlertPatrolId);
    }

    private bool IsSiteExplorationActive(WorldSiteState site, WorldSiteDefinition definition)
    {
        return site?.Exploration != null &&
               definition?.ExplorationPatrols != null &&
               definition.ExplorationPatrols.Count > 0 &&
               _activeGridMap != null &&
               site.OwnerFactionId != StrategicWorldRuntime.State?.PlayerFactionId &&
               ResolveSiteExplorationPartyPlacement(site) != null &&
               definition.ExplorationPatrols.Any(patrol => WorldSiteExplorationService.HasAlivePatrolPlacement(site, patrol));
    }

    private void EnsureSiteExplorationStateReady(WorldSiteState site, WorldSiteDefinition definition)
    {
        if (!IsSiteExplorationActive(site, definition))
        {
            return;
        }

        WorldSiteExplorationService.ReconcilePatrolStates(site, definition);
        if (_activeGridMap.TryGetTopSurfacePosition(
                new GridPosition(site.Exploration.CurrentCellX, site.Exploration.CurrentCellY),
                out GridSurfacePosition current) &&
            current.Height == site.Exploration.CurrentCellHeight)
        {
            return;
        }

        if (TryResolveExplorationEntrySurface(site, definition, out GridSurfacePosition entry))
        {
            site.Exploration.CurrentCellX = entry.X;
            site.Exploration.CurrentCellY = entry.Y;
            site.Exploration.CurrentCellHeight = entry.Height;
            site.Exploration.IsSimulationPaused = true;
            site.Exploration.PauseReason = SiteExplorationPauseReady;
            return;
        }

        GameLog.Warn(
            nameof(WorldSiteRoot),
            $"SiteExplorationEntryUnresolved site={site.SiteId} reason=known_player_entrance_entry_missing");
    }

    private bool TryResolveExplorationEntrySurface(
        WorldSiteState site,
        WorldSiteDefinition definition,
        out GridSurfacePosition entry)
    {
        entry = default;
        if (site == null || definition == null)
        {
            return false;
        }

        WorldSiteUnitPlacement partyPlacement = ResolveSiteExplorationPartyPlacement(site);
        if (partyPlacement != null &&
            IsKnownPlayerEntrancePlacement(site, definition, partyPlacement) &&
            _activeGridMap.TryGetTopSurfacePosition(new GridPosition(partyPlacement.CellX, partyPlacement.CellY), out GridSurfacePosition partySurface) &&
            partySurface.Height == partyPlacement.CellHeight)
        {
            entry = partySurface;
            return true;
        }

        if (partyPlacement == null ||
            string.IsNullOrWhiteSpace(partyPlacement.ArmyId) ||
            StrategicWorldRuntime.State?.ArmyStates.TryGetValue(partyPlacement.ArmyId, out WorldArmyState army) != true)
        {
            return false;
        }

        if (_deploymentCache == null || _deploymentCache.SiteId != site.SiteId)
        {
            RebuildSiteDeploymentRuntimeCache(site.SiteId);
        }

        string unitTypeId = ResolveExplorationPartyUnitTypeId(site);
        bool canEnterWater = false;
        if (_battleUnitFactory.TryGetUnitDefinition(unitTypeId, out BattleUnitDefinition unitDefinition))
        {
            canEnterWater = unitDefinition.CanEnterWater;
        }

        if (!TryResolveKnownPlayerEntranceDeploymentCandidate(
                site,
                definition,
                army.TargetApproachDirection,
                canEnterWater,
                out WorldSiteDeploymentCell candidate,
                out WorldSiteAttackDirection direction,
                out string entranceId))
        {
            GameLog.Warn(nameof(WorldSiteRoot), $"SiteExplorationEntryMissingKnownEntrance site={site.SiteId} army={army.ArmyId} targetDirection={army.TargetApproachDirection}");
            return false;
        }

        // Infiltration starts from a player entrance known through current intel; hidden authored entrances stay unusable until revealed.
        entry = new GridSurfacePosition(candidate.Cell.X, candidate.Cell.Y, candidate.Height);
        partyPlacement.EntranceId = entranceId;
        partyPlacement.AttackDirection = direction;
        partyPlacement.CellX = entry.X;
        partyPlacement.CellY = entry.Y;
        partyPlacement.CellHeight = entry.Height;
        return true;
    }

    private bool TryResolveKnownPlayerEntranceDeploymentCandidate(
        WorldSiteState site,
        WorldSiteDefinition definition,
        WorldSiteAttackDirection preferredDirection,
        bool canEnterWater,
        out WorldSiteDeploymentCell candidate,
        out WorldSiteAttackDirection resolvedDirection,
        out string entranceId)
    {
        candidate = default;
        resolvedDirection = WorldSiteAttackDirection.Any;
        entranceId = "";
        foreach (BattleEntranceDefinition entrance in EnumerateKnownPlayerEntrances(site, definition, preferredDirection))
        {
            WorldSiteAttackDirection direction = entrance.Direction;
            if (TryResolveFirstDeploymentCandidate(direction, canEnterWater, out candidate))
            {
                resolvedDirection = direction;
                entranceId = entrance.EntranceId ?? "";
                return true;
            }
        }

        return false;
    }

    private IEnumerable<BattleEntranceDefinition> EnumerateKnownPlayerEntrances(
        WorldSiteState site,
        WorldSiteDefinition definition,
        WorldSiteAttackDirection preferredDirection)
    {
        if (site == null || definition?.EntranceDefinitions == null)
        {
            return System.Array.Empty<BattleEntranceDefinition>();
        }

        WorldSiteIntelViewModel intel = WorldSiteIntelService.BuildCurrentView(
            StrategicWorldRuntime.State,
            StrategicWorldRuntime.Definition,
            site.SiteId,
            WorldIntelVisibility.Visible);
        var knownEntranceIds = new HashSet<string>(intel.KnownEntranceIds, System.StringComparer.Ordinal);
        string playerFactionId = StrategicWorldRuntime.State?.PlayerFactionId ?? StrategicWorldIds.FactionPlayer;
        List<BattleEntranceDefinition> entrances = definition.EntranceDefinitions
            .Where(entrance => IsKnownPlayerEntrance(entrance, knownEntranceIds, playerFactionId))
            .ToList();
        if (preferredDirection == WorldSiteAttackDirection.Any)
        {
            return entrances;
        }

        return entrances
            .Where(entrance => entrance.Direction == preferredDirection)
            .Concat(entrances.Where(entrance => entrance.Direction != preferredDirection))
            .ToArray();
    }

    private static bool IsKnownPlayerEntrancePlacement(
        WorldSiteState site,
        WorldSiteDefinition definition,
        WorldSiteUnitPlacement placement)
    {
        if (placement == null)
        {
            return false;
        }

        WorldSiteIntelViewModel intel = WorldSiteIntelService.BuildCurrentView(
            StrategicWorldRuntime.State,
            StrategicWorldRuntime.Definition,
            site?.SiteId ?? definition?.Id ?? "",
            WorldIntelVisibility.Visible);
        var knownEntranceIds = new HashSet<string>(intel.KnownEntranceIds, System.StringComparer.Ordinal);
        string playerFactionId = StrategicWorldRuntime.State?.PlayerFactionId ?? StrategicWorldIds.FactionPlayer;
        return definition?.EntranceDefinitions?.Any(entrance =>
            IsKnownPlayerEntrance(entrance, knownEntranceIds, playerFactionId) &&
            ((!string.IsNullOrWhiteSpace(placement.EntranceId) && entrance.EntranceId == placement.EntranceId) ||
             (string.IsNullOrWhiteSpace(placement.EntranceId) && entrance.Direction == placement.AttackDirection))) == true;
    }

    private static bool IsKnownPlayerEntrance(
        BattleEntranceDefinition entrance,
        IReadOnlySet<string> knownEntranceIds,
        string playerFactionId)
    {
        if (entrance == null ||
            string.IsNullOrWhiteSpace(entrance.EntranceId) ||
            knownEntranceIds?.Contains(entrance.EntranceId) != true ||
            string.Equals(entrance.Source, "Garrison", System.StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return string.IsNullOrWhiteSpace(entrance.FactionId) ||
               entrance.FactionId == playerFactionId ||
               entrance.FactionId == StrategicWorldIds.FactionPlayer;
    }

    private bool TryResolveFirstDeploymentCandidate(
        WorldSiteAttackDirection direction,
        bool canEnterWater,
        out WorldSiteDeploymentCell candidate)
    {
        candidate = default;
        foreach (WorldSiteDeploymentCell item in _deploymentCache?.GetCandidates(direction) ?? System.Array.Empty<WorldSiteDeploymentCell>())
        {
            if (!CanUseDeploymentCell(item, canEnterWater))
            {
                continue;
            }

            candidate = item;
            return true;
        }

        return false;
    }

    private static string ResolveExplorationPatrolName(WorldSiteDefinition definition, string patrolId)
    {
        return definition?.ExplorationPatrols.FirstOrDefault(patrol => patrol.Id == patrolId)?.DisplayName ??
               patrolId ??
               "";
    }
}
