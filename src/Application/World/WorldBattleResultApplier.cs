using System.Linq;
using System.Collections.Generic;
using Rpg.Definitions.World;
using Rpg.Application.Battle;
using Rpg.Domain.World;
using Rpg.Infrastructure.Logging;

namespace Rpg.Application.World;

public sealed class WorldBattleResultApplier
{
    private readonly WorldTickService _worldTickService = new();
    private readonly WorldSiteModeTransitionService _siteModeTransitions = new();
    private readonly WorldSiteDeploymentService _deploymentService = new();
    private readonly WorldGarrisonMutationService _garrisonMutations = new();
    private readonly WorldSiteBattleUnitPoolService _battleUnitPool = new();

    public WorldActionResult Apply(
        StrategicWorldState state,
        Rpg.Definitions.World.StrategicWorldDefinition definition,
        BattleStartRequest request,
        BattleResult result)
    {
        if (state == null || definition == null || request == null || result == null)
        {
            return WorldActionResult.Failed("battle_result", "missing_battle_result", "战斗结果缺少必要上下文。");
        }

        if (request.RequestId != result.RequestId)
        {
            return WorldActionResult.Failed("battle_result", "battle_result_mismatch", "战斗结果与发起请求不匹配。");
        }

        WorldActionResult actionResult = request.BattleKind switch
        {
            BattleKind.AssaultSite => ApplyAssaultBonefield(state, definition, request, result),
            BattleKind.FieldIntercept => ApplyFieldIntercept(state, request, result),
            _ => WorldActionResult.Failed("battle_result", "unsupported_battle_kind", "暂不支持该战斗类型回写。")
        };

        if (!actionResult.Success)
        {
            return actionResult;
        }

        WorldTickResult tickResult = _worldTickService.AdvanceWorldTick(state, definition);
        actionResult.Events.AddRange(tickResult.Events);
        if (tickResult.Messages.Count > 0)
        {
            actionResult.Message = $"{actionResult.Message}\n{string.Join("\n", tickResult.Messages)}";
        }

        GameLog.Info(nameof(WorldBattleResultApplier), $"WorldBattleResultApplied kind={request.BattleKind} outcome={result.Outcome} target={request.TargetSiteId}");
        return actionResult;
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

    private static string FormatForcesForLog(IEnumerable<BattleForceRequest> forces)
    {
        return forces == null
            ? "none"
            : string.Join(
                "|",
                forces.Select(force =>
                    $"{force.ForceId}[unit={force.UnitDefinitionId},count={force.Count},source={force.SourceKind}:{force.SourceId},faction={force.FactionId}]"));
    }

    private static string FormatForceResultsForLog(IEnumerable<BattleForceResult> results)
    {
        return results == null
            ? "none"
            : string.Join(
                "|",
                results.Select(result =>
                    $"{result.ForceId}[unit={result.UnitDefinitionId},source={result.SourceKind}:{result.SourceId},initial={result.InitialCount},survived={result.SurvivedCount},defeated={result.DefeatedCount}]"));
    }

    private WorldActionResult ApplyAssaultBonefield(
        StrategicWorldState state,
        StrategicWorldDefinition definition,
        BattleStartRequest request,
        BattleResult result)
    {
        WorldSiteState site = state.SiteStates[StrategicWorldIds.SiteBonefield];
        StrategicWorldDefinitionQueries queries = new(definition);
        string targetSite = StrategicWorldDisplayNames.GetSiteLabel(queries, site.SiteId, "敌方前哨");
        if (result.Outcome == BattleOutcome.Victory && ObjectiveSucceeded(result, "occupy_bonefield"))
        {
            RemoveBattleForcesFromSite(site, request.EnemyForces, result);
            site.OwnerFactionId = state.PlayerFactionId;
            site.ControlState = SiteControlState.PlayerHeld;
            site.LastVisitedTick = state.WorldTick;
            WorldActionResult actionResult = new()
            {
                Success = true,
                ActionId = "battle_result",
                Message = $"{targetSite}已被占领。",
                Events =
                {
                    new GameEvent
                    {
                        Kind = "SiteControlChanged",
                        Tick = state.WorldTick,
                        TargetIds = { site.SiteId },
                        Payload = { ["owner"] = state.PlayerFactionId, ["state"] = nameof(SiteControlState.PlayerHeld) }
                    }
                }
            };
            ResolveAssaultArmy(state, definition, request, result, actionResult, WorldArmyStatus.Garrisoned, site);
            WorldSiteDefinition siteDefinition = new StrategicWorldDefinitionQueries(definition).GetSite(site.SiteId);
            _deploymentService.EnsureGarrisonPlacements(site, siteDefinition);
            WorldSiteModeTransitionService.AddEvent(actionResult, _siteModeTransitions.EnterAftermath(site, state.WorldTick, "assault_victory", request.RequestId));
            return actionResult;
        }

        WorldActionResult failedAssaultResult = new()
        {
            Success = true,
            ActionId = "battle_result",
            Message = $"攻占失败，{targetSite}仍被敌方控制，出征部队被击溃。",
            Events =
            {
                new GameEvent
                {
                    Kind = "BattleResolved",
                    Tick = state.WorldTick,
                    TargetIds = { request.TargetSiteId },
                    Payload = { ["outcome"] = result.Outcome.ToString() }
                }
            }
        };
        ResolveAssaultArmy(state, definition, request, result, failedAssaultResult, WorldArmyStatus.Defeated, site);
        WorldSiteModeTransitionService.AddEvent(failedAssaultResult, _siteModeTransitions.EnterAftermath(site, state.WorldTick, "assault_failed", request.RequestId));
        return failedAssaultResult;
    }

    private void ResolveAssaultArmy(
        StrategicWorldState state,
        StrategicWorldDefinition definition,
        BattleStartRequest request,
        BattleResult battleResult,
        WorldActionResult result,
        WorldArmyStatus status,
        WorldSiteState targetSite)
    {
        if (string.IsNullOrWhiteSpace(request.SourceArmyId) ||
            !state.ArmyStates.TryGetValue(request.SourceArmyId, out WorldArmyState army))
        {
            return;
        }

        if (status == WorldArmyStatus.Garrisoned && targetSite != null)
        {
            if (_battleUnitPool.HasImportedArmy(targetSite, army.ArmyId))
            {
                ApplyImportedArmyCasualties(targetSite, request, battleResult, army);
            }
            else
            {
                bool hasForceResults = HasForceResults(battleResult);
                foreach (BattleForceRequest force in EnumeratePlayerArmyForces(request, army.ArmyId))
                {
                    int transferred = hasForceResults
                        ? GetSurvivedCountForArmyUnit(request, battleResult, army.ArmyId, force.UnitDefinitionId)
                        : force.Count;
                    _garrisonMutations.Add(
                        targetSite,
                        force.UnitDefinitionId,
                        transferred,
                        request.AttackerFactionId,
                        "PlayerArmy",
                        army.ArmyId,
                        ResolveArmyUnitMorale(army, force.UnitDefinitionId));
                }
            }

            WorldSiteDefinition siteDefinition = new StrategicWorldDefinitionQueries(definition).GetSite(targetSite.SiteId);
            RemoveImportedArmyUnitsOutsideRequest(targetSite, request, army);
            RemoveRequestedArmyUnitsFromArmy(army, request);
            int removedTransientPlacements = RemoveResolvedAssaultArmyPlacements(targetSite, army.ArmyId);
            _deploymentService.EnsureGarrisonPlacements(targetSite, siteDefinition);
            GameLog.Info(
                nameof(WorldBattleResultApplier),
                $"AssaultArmyGarrisoned army={army.ArmyId} site={targetSite.SiteId} removedTransientPlacements={removedTransientPlacements}");
        }
        else if (targetSite != null && status == WorldArmyStatus.Defeated)
        {
            // Assault deployment uses site-local VisitingArmy/Attacker placement rows before the
            // runtime starts. Once the army is resolved, those rows are no longer authoritative;
            // survivors are represented by the target garrison and defeated armies by army state.
            int removedTransientPlacements = RemoveResolvedAssaultArmyPlacements(targetSite, army.ArmyId);
            RemoveImportedArmyUnitsForArmy(targetSite, request, army.ArmyId);
            ApplyDeployedArmyCasualtiesToArmy(request, battleResult, army);
            GameLog.Info(
                nameof(WorldBattleResultApplier),
                $"AssaultArmyDefeated army={army.ArmyId} site={targetSite.SiteId} removedTransientPlacements={removedTransientPlacements}");
        }

        army.Status = status;
        army.ClearTargetApproachDirection();
        result.Events.Add(new GameEvent
        {
            Kind = "WorldArmyStateChanged",
            Tick = state.WorldTick,
            TargetIds = { army.ArmyId, request.TargetSiteId },
            Payload = { ["status"] = status.ToString(), ["reason"] = "battle_result" }
        });
    }

    private void ApplyImportedArmyCasualties(
        WorldSiteState targetSite,
        BattleStartRequest request,
        BattleResult battleResult,
        WorldArmyState army)
    {
        if (targetSite == null || request == null || battleResult == null || army == null)
        {
            return;
        }

        foreach (BattleForceRequest force in EnumeratePlayerArmyForces(request, army.ArmyId))
        {
            int survived = HasForceResults(battleResult)
                ? GetSurvivedCountForArmyUnit(request, battleResult, army.ArmyId, force.UnitDefinitionId)
                : force.Count;
            int defeated = System.Math.Max(0, force.Count - survived);
            _garrisonMutations.Remove(
                targetSite,
                force.UnitDefinitionId,
                defeated,
                request.AttackerFactionId,
                "PlayerArmy",
                army.ArmyId);
        }
    }

    private static IEnumerable<BattleForceRequest> EnumeratePlayerArmyForces(BattleStartRequest request, string armyId)
    {
        return request?.PlayerForces?
            .Where(force =>
                force != null &&
                force.Count > 0 &&
                string.Equals(force.SourceKind, "PlayerArmy", System.StringComparison.Ordinal) &&
                string.Equals(force.SourceId, armyId, System.StringComparison.Ordinal) &&
                !string.IsNullOrWhiteSpace(force.UnitDefinitionId)) ??
            Enumerable.Empty<BattleForceRequest>();
    }

    private static int ResolveArmyUnitMorale(WorldArmyState army, string unitTypeId)
    {
        return army?.GarrisonUnits?
            .FirstOrDefault(unit => string.Equals(unit.UnitTypeId, unitTypeId, System.StringComparison.Ordinal))
            ?.Morale ?? 70;
    }

    private void RemoveImportedArmyUnitsOutsideRequest(
        WorldSiteState targetSite,
        BattleStartRequest request,
        WorldArmyState army)
    {
        if (targetSite == null || request == null || army == null)
        {
            return;
        }

        Dictionary<string, int> requestedCounts = EnumeratePlayerArmyForces(request, army.ArmyId)
            .GroupBy(force => force.UnitDefinitionId)
            .ToDictionary(group => group.Key, group => group.Sum(force => System.Math.Max(0, force.Count)));
        foreach (IGrouping<string, GarrisonState> importedGroup in targetSite.Garrison
                     .Where(garrison =>
                         garrison != null &&
                         string.Equals(garrison.SourceKind, "PlayerArmy", System.StringComparison.Ordinal) &&
                         string.Equals(garrison.SourceId, army.ArmyId, System.StringComparison.Ordinal) &&
                         !string.IsNullOrWhiteSpace(garrison.UnitTypeId))
                     .GroupBy(garrison => garrison.UnitTypeId)
                     .ToArray())
        {
            int importedCount = importedGroup.Sum(garrison => System.Math.Max(0, garrison.Count));
            requestedCounts.TryGetValue(importedGroup.Key, out int requestedCount);
            int removeCount = System.Math.Max(0, importedCount - requestedCount);
            if (removeCount <= 0)
            {
                continue;
            }

            _garrisonMutations.Remove(
                targetSite,
                importedGroup.Key,
                removeCount,
                request.AttackerFactionId,
                "PlayerArmy",
                army.ArmyId);
        }
    }

    private void RemoveImportedArmyUnitsForArmy(
        WorldSiteState targetSite,
        BattleStartRequest request,
        string armyId)
    {
        if (targetSite == null || string.IsNullOrWhiteSpace(armyId))
        {
            return;
        }

        foreach (IGrouping<string, GarrisonState> importedGroup in targetSite.Garrison
                     .Where(garrison =>
                         garrison != null &&
                         string.Equals(garrison.SourceKind, "PlayerArmy", System.StringComparison.Ordinal) &&
                         string.Equals(garrison.SourceId, armyId, System.StringComparison.Ordinal) &&
                         !string.IsNullOrWhiteSpace(garrison.UnitTypeId))
                     .GroupBy(garrison => garrison.UnitTypeId)
                     .ToArray())
        {
            int removeCount = importedGroup.Sum(garrison => System.Math.Max(0, garrison.Count));
            if (removeCount <= 0)
            {
                continue;
            }

            _garrisonMutations.Remove(
                targetSite,
                importedGroup.Key,
                removeCount,
                request?.AttackerFactionId ?? "",
                "PlayerArmy",
                armyId);
        }
    }

    private static void RemoveRequestedArmyUnitsFromArmy(WorldArmyState army, BattleStartRequest request)
    {
        if (army == null)
        {
            return;
        }

        foreach (BattleForceRequest force in EnumeratePlayerArmyForces(request, army.ArmyId))
        {
            RemoveArmyUnitCount(army, force.UnitDefinitionId, force.Count);
        }
    }

    private static void ApplyDeployedArmyCasualtiesToArmy(
        BattleStartRequest request,
        BattleResult battleResult,
        WorldArmyState army)
    {
        if (army == null)
        {
            return;
        }

        foreach (BattleForceRequest force in EnumeratePlayerArmyForces(request, army.ArmyId))
        {
            int survived = HasForceResults(battleResult)
                ? GetSurvivedCountForArmyUnit(request, battleResult, army.ArmyId, force.UnitDefinitionId)
                : 0;
            RemoveArmyUnitCount(army, force.UnitDefinitionId, System.Math.Max(0, force.Count - survived));
        }
    }

    private static void RemoveArmyUnitCount(WorldArmyState army, string unitTypeId, int count)
    {
        if (army?.GarrisonUnits == null || string.IsNullOrWhiteSpace(unitTypeId) || count <= 0)
        {
            return;
        }

        int remaining = count;
        foreach (GarrisonState unit in army.GarrisonUnits
                     .Where(unit => string.Equals(unit.UnitTypeId, unitTypeId, System.StringComparison.Ordinal))
                     .ToArray())
        {
            if (remaining <= 0)
            {
                break;
            }

            int removed = System.Math.Min(System.Math.Max(0, unit.Count), remaining);
            unit.Count -= removed;
            remaining -= removed;
        }

        army.GarrisonUnits.RemoveAll(unit => unit == null || unit.Count <= 0);
    }

    private static int RemoveResolvedAssaultArmyPlacements(WorldSiteState site, string armyId)
    {
        if (site == null || string.IsNullOrWhiteSpace(armyId))
        {
            return 0;
        }

        return site.UnitPlacements.RemoveAll(placement =>
            placement != null &&
            placement.SourceKind == "PlayerArmy" &&
            placement.SourceId == armyId &&
            placement.PlacementKind is WorldSiteUnitPlacementKind.VisitingArmy or WorldSiteUnitPlacementKind.Attacker);
    }

    private WorldActionResult ApplyFieldIntercept(StrategicWorldState state, BattleStartRequest request, BattleResult result)
    {
        WorldArmyState playerArmy = !string.IsNullOrWhiteSpace(request.SourceArmyId) &&
                                    state.ArmyStates.TryGetValue(request.SourceArmyId, out WorldArmyState playerValue)
            ? playerValue
            : null;
        WorldArmyState enemyArmy = !string.IsNullOrWhiteSpace(request.TargetArmyId) &&
                                   state.ArmyStates.TryGetValue(request.TargetArmyId, out WorldArmyState enemyValue)
            ? enemyValue
            : null;

        if (playerArmy == null || enemyArmy == null)
        {
            return WorldActionResult.Failed("battle_result", "field_intercept_army_missing", "找不到野外遭遇战相关部队。");
        }

        bool victory = result.Outcome == BattleOutcome.Victory && ObjectiveSucceeded(result, "win_field_intercept");
        WorldActionResult actionResult = new()
        {
            Success = true,
            ActionId = "battle_result",
            Message = victory
                ? "野外遭遇战胜利，敌军被拦截并击溃。"
                : "野外遭遇战失败，玩家部队被击溃，敌军继续行军。"
        };

        if (victory)
        {
            enemyArmy.Status = WorldArmyStatus.Defeated;
            ResumeArmyAfterFieldBattle(playerArmy);
        }
        else
        {
            playerArmy.Status = WorldArmyStatus.Defeated;
            ResumeArmyAfterFieldBattle(enemyArmy);
        }

        actionResult.Events.Add(new GameEvent
        {
            Kind = "FieldInterceptResolved",
            Tick = state.WorldTick,
            TargetIds = { playerArmy.ArmyId, enemyArmy.ArmyId },
            Payload =
            {
                ["outcome"] = result.Outcome.ToString(),
                ["playerArmyStatus"] = playerArmy.Status.ToString(),
                ["enemyArmyStatus"] = enemyArmy.Status.ToString()
            }
        });
        return actionResult;
    }

    private static void ResumeArmyAfterFieldBattle(WorldArmyState army)
    {
        if (army.Status == WorldArmyStatus.Defeated)
        {
            return;
        }

        army.Status = army.Intent is WorldArmyIntent.AssaultSite or WorldArmyIntent.ReinforceSite or WorldArmyIntent.Intercept
            ? WorldArmyStatus.Moving
            : WorldArmyStatus.Idle;
        army.ClearNavigationPath();
    }

    private static bool ObjectiveSucceeded(BattleResult result, string objectiveId)
    {
        if (result.ObjectiveResults.Count == 0)
        {
            return result.Outcome == BattleOutcome.Victory;
        }

        return result.ObjectiveResults.Any(item =>
            item.ObjectiveId == objectiveId &&
            item.State == BattleObjectiveState.Succeeded);
    }

    private static void AddUnique(System.Collections.Generic.List<string> values, string value)
    {
        if (values == null || string.IsNullOrWhiteSpace(value) || values.Contains(value))
        {
            return;
        }

        values.Add(value);
    }

    private void ApplyDefendingGarrisonCasualties(
        WorldSiteState site,
        BattleStartRequest request,
        BattleResult result)
    {
        if (site == null || request == null || !HasForceResults(result))
        {
            return;
        }

        foreach (BattleForceRequest force in EnumerateForces(request)
                     .Where(force => IsSiteGarrisonForce(force, site.SiteId)))
        {
            int defeated = GetDefeatedCount(result, force, 0);
            _garrisonMutations.Remove(site, force.UnitDefinitionId, defeated);
        }
    }

    private void RemoveBattleForcesFromSite(
        WorldSiteState site,
        System.Collections.Generic.IEnumerable<BattleForceRequest> forces,
        BattleResult result)
    {
        if (site == null || forces == null)
        {
            return;
        }

        bool hasForceResults = HasForceResults(result);
        foreach (BattleForceRequest force in forces.Where(item => item.Count > 0 && !string.IsNullOrWhiteSpace(item.UnitDefinitionId)))
        {
            int removeCount = hasForceResults
                ? GetDefeatedCount(result, force, 0)
                : force.Count;
            _garrisonMutations.Remove(
                site,
                force.UnitDefinitionId,
                removeCount,
                force.FactionId,
                IsSiteGarrisonForce(force, site.SiteId) ? "" : force.SourceKind,
                IsSiteGarrisonForce(force, site.SiteId) ? "" : force.SourceId);
        }
    }

    private static bool HasForceResults(BattleResult result)
    {
        return result?.ForceResults != null && result.ForceResults.Count > 0;
    }

    private static int GetSurvivedCountForArmyUnit(
        BattleStartRequest request,
        BattleResult result,
        string armyId,
        string unitDefinitionId)
    {
        BattleForceRequest force = request?.PlayerForces?
            .FirstOrDefault(item =>
                string.Equals(item.SourceId, armyId, System.StringComparison.Ordinal) &&
                string.Equals(item.UnitDefinitionId, unitDefinitionId, System.StringComparison.Ordinal));
        if (force == null)
        {
            return 0;
        }

        BattleForceResult forceResult = FindForceResult(result, force);
        return forceResult == null ? 0 : System.Math.Max(0, forceResult.SurvivedCount);
    }

    private static int GetDefeatedCount(BattleResult result, BattleForceRequest force, int fallback)
    {
        BattleForceResult forceResult = FindForceResult(result, force);
        if (forceResult == null)
        {
            return fallback;
        }

        if (forceResult.DefeatedCount > 0)
        {
            return forceResult.DefeatedCount;
        }

        return System.Math.Max(0, forceResult.InitialCount - forceResult.SurvivedCount);
    }

    private static BattleForceResult FindForceResult(BattleResult result, BattleForceRequest force)
    {
        if (result?.ForceResults == null || force == null)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(force.ForceId))
        {
            BattleForceResult byForceId = result.ForceResults.FirstOrDefault(item =>
                string.Equals(item.ForceId, force.ForceId, System.StringComparison.Ordinal));
            if (byForceId != null)
            {
                return byForceId;
            }
        }

        return result.ForceResults.FirstOrDefault(item =>
            string.Equals(item.SourceKind, force.SourceKind, System.StringComparison.Ordinal) &&
            string.Equals(item.SourceId, force.SourceId, System.StringComparison.Ordinal) &&
            string.Equals(item.UnitDefinitionId, force.UnitDefinitionId, System.StringComparison.Ordinal));
    }

    private static bool IsSiteGarrisonForce(BattleForceRequest force, string siteId)
    {
        return force != null &&
               !string.IsNullOrWhiteSpace(force.UnitDefinitionId) &&
               string.Equals(force.SourceId, siteId, System.StringComparison.Ordinal) &&
               (string.Equals(force.SourceKind, "Garrison", System.StringComparison.Ordinal) ||
                string.Equals(force.SourceKind, "DefenderSite", System.StringComparison.Ordinal));
    }

    private static System.Collections.Generic.IEnumerable<BattleForceRequest> EnumerateForces(BattleStartRequest request)
    {
        if (request?.PlayerForces != null)
        {
            foreach (BattleForceRequest force in request.PlayerForces)
            {
                yield return force;
            }
        }

        if (request?.EnemyForces != null)
        {
            foreach (BattleForceRequest force in request.EnemyForces)
            {
                yield return force;
            }
        }
    }
}
