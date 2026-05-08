using System.Linq;
using Godot;
using Rpg.Definitions.World;
using Rpg.Domain.World;
using Rpg.Infrastructure.Logging;

namespace Rpg.Application.World;

public sealed class WorldTickService
{
    private readonly WorldConditionEvaluator _conditionEvaluator = new();
    private readonly WorldSiteModeTransitionService _siteModeTransitions = new();
    private readonly WorldOpportunityService _opportunityService = new();
    private readonly WorldBattleProgressionService _worldBattleProgressionService = new();

    public WorldTickResult AdvanceWorldTick(StrategicWorldState state, StrategicWorldDefinition definition)
    {
        StrategicWorldDefinitionQueries queries = new(definition);
        state.WorldTick++;

        WorldTickResult result = new() { WorldTick = state.WorldTick };
        GameLog.Info(nameof(WorldTickService), $"WorldTickStarted tick={state.WorldTick}");

        _siteModeTransitions.ClearAftermathSites(state, result);
        ApplyProduction(state, queries, result);
        GenerateThreats(state, definition, queries, result);
        ProgressThreats(state, result);
        _worldBattleProgressionService.EnsureBattlesForAttackingThreats(state, definition, result);
        _worldBattleProgressionService.AdvanceWorldBattles(state, definition, result);
        _opportunityService.AdvanceOpportunities(state, definition, result);

        result.Events.Add(new GameEvent
        {
            Kind = "WorldTickAdvanced",
            Tick = state.WorldTick
        });

        string activeThreats = string.Join(",", state.ThreatPlans.Values
            .Where(threat => threat.Stage != ThreatStage.Resolved)
            .Select(threat => $"{threat.Id}:{threat.Stage}:{threat.CountdownTicks}"));
        GameLog.Info(
            nameof(WorldTickService),
            $"WorldTickEnded tick={state.WorldTick} population={state.PlayerResources.GetAvailable(StrategicWorldIds.ResourcePopulation)}/{state.PlayerResources.GetAmount(StrategicWorldIds.ResourcePopulation)} economy={state.PlayerResources.GetAmount(StrategicWorldIds.ResourceEconomy)} stone={state.PlayerResources.GetAmount(StrategicWorldIds.ResourceStone)} activeThreats={activeThreats}");
        return result;
    }

    private static void ApplyProduction(StrategicWorldState state, StrategicWorldDefinitionQueries queries, WorldTickResult result)
    {
        foreach (WorldSiteState site in state.SiteStates.Values)
        {
            if (site.OwnerFactionId != state.PlayerFactionId ||
                site.ControlState is not (SiteControlState.PlayerHeld or SiteControlState.Damaged))
            {
                continue;
            }

            foreach (FacilityInstance facility in site.Facilities)
            {
                if (facility.FacilityId != StrategicWorldIds.FacilityMine ||
                    facility.State != FacilityState.Active ||
                    facility.AssignedPopulation < 1)
                {
                    continue;
                }

                state.PlayerResources.Add(StrategicWorldIds.ResourceStone, 2);
                result.Messages.Add($"{queries.GetSite(site.SiteId)?.DisplayName ?? site.SiteId} 矿场产出石材 +2。");
                result.Events.Add(new GameEvent
                {
                    Kind = "ResourceChanged",
                    Tick = state.WorldTick,
                    TargetIds = { StrategicWorldIds.ResourceStone },
                    Payload = { ["amount"] = "2", ["reason"] = "mine_production", ["site"] = site.SiteId }
                });
                GameLog.Info(nameof(WorldTickService), $"Production site={site.SiteId} facility={facility.InstanceId} stone=2");
            }
        }
    }

    private void GenerateThreats(
        StrategicWorldState state,
        StrategicWorldDefinition definition,
        StrategicWorldDefinitionQueries queries,
        WorldTickResult result)
    {
        foreach (ThreatRuleDefinition rule in definition.ThreatRules)
        {
            var request = new WorldActionRequest
            {
                ActorFactionId = state.PlayerFactionId,
                SourceSiteId = rule.SourceSiteId,
                TargetSiteId = rule.TargetSiteId
            };

            if (!_conditionEvaluator.AreConditionsMet(state, queries, request, rule.TriggerConditions, out _))
            {
                continue;
            }

            string threatId = $"{rule.Id}:{state.WorldTick}";
            string armyId = BuildThreatArmyId(threatId);
            EnemyThreatPlan threat = new()
            {
                Id = threatId,
                RuleId = rule.Id,
                SourceSiteId = rule.SourceSiteId,
                TargetSiteId = rule.TargetSiteId,
                WorldArmyId = armyId,
                ThreatType = rule.ThreatType,
                Stage = ThreatStage.Marching,
                InitialCountdownTicks = rule.InitialCountdownTicks,
                CountdownTicks = rule.InitialCountdownTicks,
                EnemyGroupId = rule.EnemyGroupId,
                CreatedTick = state.WorldTick
            };
            state.ThreatPlans[threatId] = threat;

            WorldArmyState army = CreateThreatArmy(state, definition, queries, rule, threat, armyId);
            state.ArmyStates[armyId] = army;

            if (state.SiteStates.TryGetValue(rule.TargetSiteId, out WorldSiteState targetSite) &&
                !targetSite.PendingThreatIds.Contains(threatId))
            {
                targetSite.PendingThreatIds.Add(threatId);
                WorldSiteModeTransitionService.AddEvent(result, _siteModeTransitions.EnterAlert(targetSite, state.WorldTick, "threat_created", threatId));
            }

            result.Messages.Add($"{queries.GetSite(rule.SourceSiteId)?.DisplayName ?? rule.SourceSiteId} 派出敌军，正向 {queries.GetSite(rule.TargetSiteId)?.DisplayName ?? rule.TargetSiteId} 行军。");
            result.Events.Add(new GameEvent
            {
                Kind = "ThreatCreated",
                Tick = state.WorldTick,
                TargetIds = { threatId, armyId, rule.TargetSiteId },
                Payload = { ["rule"] = rule.Id, ["army"] = armyId, ["countdown"] = rule.InitialCountdownTicks.ToString() }
            });
            result.Events.Add(new GameEvent
            {
                Kind = "WorldArmyCreated",
                Tick = state.WorldTick,
                TargetIds = { armyId, rule.SourceSiteId, rule.TargetSiteId },
                Payload =
                {
                    ["owner"] = army.OwnerFactionId,
                    ["intent"] = army.Intent.ToString(),
                    ["relatedThreat"] = threatId
                }
            });
            GameLog.Info(nameof(WorldTickService), $"ThreatCreated id={threatId} army={armyId} rule={rule.Id} target={rule.TargetSiteId} countdown={rule.InitialCountdownTicks}");
        }
    }

    private static void ProgressThreats(StrategicWorldState state, WorldTickResult result)
    {
        foreach (EnemyThreatPlan threat in state.ThreatPlans.Values)
        {
            if (threat.Stage is ThreatStage.Resolved or ThreatStage.Attacking ||
                threat.CreatedTick == state.WorldTick)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(threat.WorldArmyId) &&
                state.ArmyStates.ContainsKey(threat.WorldArmyId))
            {
                continue;
            }

            threat.CountdownTicks = System.Math.Max(0, threat.CountdownTicks - 1);
            result.Events.Add(new GameEvent
            {
                Kind = "ThreatStageChanged",
                Tick = state.WorldTick,
                TargetIds = { threat.Id },
                Payload = { ["countdown"] = threat.CountdownTicks.ToString() }
            });

            if (threat.CountdownTicks > 0)
            {
                continue;
            }

            threat.Stage = ThreatStage.Attacking;
            result.AttackingThreatIds.Add(threat.Id);
            result.Messages.Add("敌方 Raid 已到达，必须处理。");
            GameLog.Info(nameof(WorldTickService), $"ThreatAttacking id={threat.Id} target={threat.TargetSiteId}");
        }
    }

    private static WorldArmyState CreateThreatArmy(
        StrategicWorldState state,
        StrategicWorldDefinition definition,
        StrategicWorldDefinitionQueries queries,
        ThreatRuleDefinition rule,
        EnemyThreatPlan threat,
        string armyId)
    {
        WorldSiteDefinition source = queries.GetSite(rule.SourceSiteId);
        WorldSiteDefinition target = queries.GetSite(rule.TargetSiteId);
        string ownerFactionId = state.SiteStates.TryGetValue(rule.SourceSiteId, out WorldSiteState sourceState) &&
                                !string.IsNullOrWhiteSpace(sourceState.OwnerFactionId)
            ? sourceState.OwnerFactionId
            : definition.EnemyFactionIds.FirstOrDefault() ?? StrategicWorldIds.FactionUndead;

        WorldArmyState army = new()
        {
            ArmyId = armyId,
            OwnerFactionId = ownerFactionId,
            SourceSiteId = rule.SourceSiteId,
            TargetSiteId = rule.TargetSiteId,
            RelatedThreatId = threat.Id,
            MoveSpeed = 42.0f,
            Radius = 18.0f,
            Status = WorldArmyStatus.Moving,
            Intent = WorldArmyIntent.Raid,
            CreatedTick = state.WorldTick
        };
        army.WorldPosition = source?.MapPosition ?? Vector2.Zero;
        army.Destination = target?.MapPosition ?? army.WorldPosition;
        army.ClearNavigationPath();
        foreach (GarrisonDefinition unit in rule.EnemyForces.Where(item => item.Count > 0))
        {
            army.GarrisonUnits.Add(new GarrisonState
            {
                UnitTypeId = unit.UnitTypeId,
                Count = unit.Count,
                Morale = unit.Morale
            });
        }

        return army;
    }

    private static string BuildThreatArmyId(string threatId) => $"{threatId}:army";
}
