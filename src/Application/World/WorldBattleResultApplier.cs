using System.Linq;
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
    private readonly WorldBattleProgressionService _worldBattleProgressionService = new();
    private readonly WorldGarrisonMutationService _garrisonMutations = new();

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

        WorldActionResult actionResult;
        if (!string.IsNullOrWhiteSpace(request.WorldBattleId))
        {
            actionResult = _worldBattleProgressionService.ApplyPlayerInterventionResult(state, definition, request, result);
        }
        else
        {
            actionResult = request.BattleKind switch
        {
            BattleKind.AssaultSite => ApplyAssaultBonefield(state, definition, request, result),
            BattleKind.DefenseRaid => ApplyDefenseRaid(state, definition, request, result),
            BattleKind.FieldIntercept => ApplyFieldIntercept(state, request, result),
            _ => WorldActionResult.Failed("battle_result", "unsupported_battle_kind", "暂不支持该战斗类型回写。")
            };
        }

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

    private WorldActionResult ApplyAssaultBonefield(
        StrategicWorldState state,
        StrategicWorldDefinition definition,
        BattleStartRequest request,
        BattleResult result)
    {
        WorldSiteState site = state.SiteStates[StrategicWorldIds.SiteBonefield];
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
                Message = "埋骨地已被占领，矿场和防御塔槽位已解锁。",
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
            Message = "攻占失败，埋骨地仍被敌方控制，出征部队被击溃。",
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
            bool hasForceResults = HasForceResults(battleResult);
            foreach (GarrisonState unit in army.GarrisonUnits.Where(item => item.Count > 0))
            {
                int transferred = hasForceResults
                    ? GetSurvivedCountForArmyUnit(request, battleResult, army.ArmyId, unit.UnitTypeId)
                    : unit.Count;
                _garrisonMutations.Add(targetSite, unit.UnitTypeId, transferred);
            }

            WorldSiteDefinition siteDefinition = new StrategicWorldDefinitionQueries(definition).GetSite(targetSite.SiteId);
            _deploymentService.EnsureGarrisonPlacements(targetSite, siteDefinition);
            army.GarrisonUnits.Clear();
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

    private WorldActionResult ApplyDefenseRaid(
        StrategicWorldState state,
        StrategicWorldDefinition definition,
        BattleStartRequest request,
        BattleResult result)
    {
        WorldSiteState site = state.SiteStates[request.TargetSiteId];
        EnemyThreatPlan threat = !string.IsNullOrWhiteSpace(request.ThreatId) && state.ThreatPlans.TryGetValue(request.ThreatId, out EnemyThreatPlan value)
            ? value
            : null;

        if (result.Outcome == BattleOutcome.Victory && ObjectiveSucceeded(result, "defend_bonefield"))
        {
            ApplyDefendingGarrisonCasualties(site, request, result);
            if (threat != null)
            {
                threat.Stage = ThreatStage.Resolved;
                site.PendingThreatIds.Remove(threat.Id);
                if (!string.IsNullOrWhiteSpace(threat.WorldArmyId) &&
                    state.ArmyStates.TryGetValue(threat.WorldArmyId, out WorldArmyState threatArmy))
                {
                    WorldDefenseRaidResolutionHelper.ResolveThreatArmy(threatArmy);
                }
            }

            site.ControlState = SiteControlState.PlayerHeld;
            WorldActionResult actionResult = new()
            {
                Success = true,
                ActionId = "battle_result",
                Message = "埋骨地防守成功，亡灵 Raid 已清除。",
                Events =
                {
                    new GameEvent
                    {
                        Kind = "ThreatStageChanged",
                        Tick = state.WorldTick,
                        TargetIds = { request.ThreatId },
                        Payload = { ["stage"] = nameof(ThreatStage.Resolved) }
                    }
                }
            };
            WorldSiteDefinition defenseSiteDefinition = new StrategicWorldDefinitionQueries(definition).GetSite(site.SiteId);
            _deploymentService.EnsureGarrisonPlacements(site, defenseSiteDefinition);
            WorldSiteModeTransitionService.AddEvent(actionResult, _siteModeTransitions.EnterAftermath(site, state.WorldTick, "defense_victory", request.RequestId));
            return actionResult;
        }

        WorldArmyState occupyingArmy = null;
        if (threat != null)
        {
            threat.Stage = ThreatStage.Resolved;
            site.PendingThreatIds.Remove(threat.Id);
            if (!string.IsNullOrWhiteSpace(threat.WorldArmyId) &&
                state.ArmyStates.TryGetValue(threat.WorldArmyId, out WorldArmyState threatArmy))
            {
                occupyingArmy = threatArmy;
            }
        }

        site.OwnerFactionId = request.AttackerFactionId;
        site.ControlState = SiteControlState.Lost;
        site.DamageLevel = System.Math.Min(2, site.DamageLevel + 1);
        WorldDefenseRaidResolutionHelper.ClearDefendingGarrison(site);
        if (occupyingArmy != null)
        {
            WorldDefenseRaidResolutionHelper.TransferThreatArmyToCapturedSite(occupyingArmy, site);
        }

        state.PlayerResources.ReleaseReservationsBySite(site.SiteId);
        foreach (FacilityInstance mine in site.Facilities.Where(facility => facility.FacilityId == StrategicWorldIds.FacilityMine))
        {
            mine.AssignedPopulation = 0;
            mine.State = FacilityState.Damaged;
        }

        WorldSiteDefinition siteDefinition = new StrategicWorldDefinitionQueries(definition).GetSite(site.SiteId);
        _deploymentService.EnsureGarrisonPlacements(site, siteDefinition);

        WorldActionResult failedDefenseResult = new()
        {
            Success = true,
            ActionId = "battle_result",
            Message = "埋骨地防守失败，场域被亡灵夺回，敌军残部进驻城中。",
            Events =
            {
                new GameEvent
                {
                    Kind = "SiteControlChanged",
                    Tick = state.WorldTick,
                    TargetIds = { site.SiteId },
                    Payload = { ["owner"] = site.OwnerFactionId, ["state"] = nameof(SiteControlState.Lost) }
                }
            }
        };
        if (occupyingArmy != null)
        {
            failedDefenseResult.Events.Add(new GameEvent
            {
                Kind = "WorldArmyStateChanged",
                Tick = state.WorldTick,
                TargetIds = { occupyingArmy.ArmyId, site.SiteId },
                Payload = { ["status"] = occupyingArmy.Status.ToString(), ["reason"] = "captured_site" }
            });
        }

        WorldSiteModeTransitionService.AddEvent(failedDefenseResult, _siteModeTransitions.EnterAftermath(site, state.WorldTick, "defense_failed", request.RequestId));
        return failedDefenseResult;
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
            ResolveRelatedThreat(state, enemyArmy, actionResult, "field_intercept_victory");
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

        army.Status = army.Intent is WorldArmyIntent.Raid or WorldArmyIntent.AssaultSite or WorldArmyIntent.ReinforceSite or WorldArmyIntent.Intercept
            ? WorldArmyStatus.Moving
            : WorldArmyStatus.Idle;
        army.ClearNavigationPath();
    }

    private static void ResolveRelatedThreat(
        StrategicWorldState state,
        WorldArmyState army,
        WorldActionResult result,
        string reason)
    {
        if (string.IsNullOrWhiteSpace(army.RelatedThreatId) ||
            !state.ThreatPlans.TryGetValue(army.RelatedThreatId, out EnemyThreatPlan threat))
        {
            return;
        }

        threat.Stage = ThreatStage.Resolved;
        if (state.SiteStates.TryGetValue(threat.TargetSiteId, out WorldSiteState site))
        {
            site.PendingThreatIds.Remove(threat.Id);
        }

        result.Events.Add(new GameEvent
        {
            Kind = "ThreatStageChanged",
            Tick = state.WorldTick,
            TargetIds = { threat.Id, army.ArmyId },
            Payload = { ["stage"] = nameof(ThreatStage.Resolved), ["reason"] = reason }
        });
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
            _garrisonMutations.Remove(site, force.UnitDefinitionId, removeCount);
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

