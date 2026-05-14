using System.Linq;
using System;
using System.Collections.Generic;
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
    private readonly WorldSiteDeploymentService _deploymentService = new();

    public WorldTickResult AdvanceWorldTick(StrategicWorldState state, StrategicWorldDefinition definition)
    {
        StrategicWorldDefinitionQueries queries = new(definition);
        state.WorldTick++;

        WorldTickResult result = new() { WorldTick = state.WorldTick };
        GameLog.Info(nameof(WorldTickService), $"WorldTickStarted tick={state.WorldTick}");

        _siteModeTransitions.ClearAftermathSites(state, result);
        ApplyProduction(state, queries, result);
        ApplyAutoGarrisonProduction(state, queries, result);
        GenerateThreats(state, definition, queries, result);
        ProgressThreats(state, queries, result);
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
                result.Messages.Add(
                    $"{StrategicWorldDisplayNames.GetSiteLabel(queries, site.SiteId)} " +
                    $"{StrategicWorldDisplayNames.GetFacilityLabel(queries, facility.FacilityId)}产出{StrategicWorldDisplayNames.GetResourceLabel(queries, StrategicWorldIds.ResourceStone)} +2。");
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

    private void ApplyAutoGarrisonProduction(
        StrategicWorldState state,
        StrategicWorldDefinitionQueries queries,
        WorldTickResult result)
    {
        foreach (WorldSiteDefinition siteDefinition in queries.Sites.Values)
        {
            if (siteDefinition.AutoGarrisonProductions.Count == 0 ||
                !state.SiteStates.TryGetValue(siteDefinition.Id, out WorldSiteState siteState))
            {
                continue;
            }

            foreach (SiteAutoGarrisonProductionDefinition production in siteDefinition.AutoGarrisonProductions)
            {
                if (!ShouldAutoProduceGarrison(state.WorldTick, siteState, production))
                {
                    continue;
                }

                List<GarrisonState> producedUnits = ProduceGarrisonBatch(siteState, production);
                if (producedUnits.Count == 0)
                {
                    continue;
                }

                _deploymentService.EnsureGarrisonPlacements(siteState, siteDefinition);
                string producedSummary = BuildForceSummary(producedUnits);
                foreach (GarrisonState produced in producedUnits)
                {
                    result.Events.Add(new GameEvent
                    {
                        Kind = "GarrisonChanged",
                        Tick = state.WorldTick,
                        TargetIds = { siteState.SiteId },
                        Payload =
                        {
                            ["unit"] = produced.UnitTypeId,
                            ["amount"] = produced.Count.ToString(),
                            ["reason"] = "auto_garrison_production"
                        }
                    });
                }

                result.Events.Add(new GameEvent
                {
                    Kind = "SiteGarrisonProduced",
                    Tick = state.WorldTick,
                    TargetIds = { siteState.SiteId },
                    Payload =
                    {
                        ["faction"] = siteState.OwnerFactionId,
                        ["produced"] = producedSummary,
                        ["total"] = GetTotalGarrisonCount(siteState.Garrison).ToString()
                    }
                });
                GameLog.Info(
                    nameof(WorldTickService),
                    $"SiteGarrisonProduced site={siteState.SiteId} faction={siteState.OwnerFactionId} produced={producedSummary} total={GetTotalGarrisonCount(siteState.Garrison)}");
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

            if (!TryDispatchThreatForcePackage(state, queries, rule, result, out List<GarrisonState> dispatchedForces))
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

            WorldArmyState army = CreateThreatArmy(state, definition, queries, rule, threat, armyId, dispatchedForces);
            state.ArmyStates[armyId] = army;

            if (state.SiteStates.TryGetValue(rule.TargetSiteId, out WorldSiteState targetSite) &&
                !targetSite.PendingThreatIds.Contains(threatId))
            {
                targetSite.PendingThreatIds.Add(threatId);
                WorldSiteModeTransitionService.AddEvent(result, _siteModeTransitions.EnterAlert(targetSite, state.WorldTick, "threat_created", threatId));
            }

            string sourceName = StrategicWorldDisplayNames.GetSiteLabel(queries, rule.SourceSiteId);
            string targetName = StrategicWorldDisplayNames.GetSiteLabel(queries, rule.TargetSiteId);
            string factionName = StrategicWorldDisplayNames.GetFactionLabel(queries, army.OwnerFactionId, "敌方");
            result.Messages.Add($"{sourceName} 派出{factionName}部队，正向 {targetName} 行军。");
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
            GameLog.Info(
                nameof(WorldTickService),
                $"ThreatCreated id={threatId} army={armyId} rule={rule.Id} target={rule.TargetSiteId} forces={BuildForceSummary(dispatchedForces)} countdown={rule.InitialCountdownTicks}");
        }
    }

    private bool TryDispatchThreatForcePackage(
        StrategicWorldState state,
        StrategicWorldDefinitionQueries queries,
        ThreatRuleDefinition rule,
        WorldTickResult result,
        out List<GarrisonState> dispatchedForces)
    {
        dispatchedForces = new List<GarrisonState>();
        if (!state.SiteStates.TryGetValue(rule.SourceSiteId, out WorldSiteState sourceSite))
        {
            GameLog.Warn(
                nameof(WorldTickService),
                $"ThreatDispatchSkipped rule={rule.Id} source={rule.SourceSiteId} reason=source_site_missing");
            return false;
        }

        if (!HasRequiredForces(sourceSite.Garrison, rule.EnemyForces))
        {
            return false;
        }

        foreach (GarrisonDefinition required in rule.EnemyForces.Where(item => item.Count > 0 && !string.IsNullOrWhiteSpace(item.UnitTypeId)))
        {
            GarrisonState dispatched = ExtractGarrison(sourceSite, required.UnitTypeId, required.Count, required.Morale);
            if (dispatched == null)
            {
                GameLog.Error(
                    nameof(WorldTickService),
                    $"ThreatDispatchFailed rule={rule.Id} source={rule.SourceSiteId} unit={required.UnitTypeId} reason=dispatch_extract_failed");
                return false;
            }

            dispatchedForces.Add(dispatched);
            result.Events.Add(new GameEvent
            {
                Kind = "GarrisonChanged",
                Tick = state.WorldTick,
                TargetIds = { sourceSite.SiteId },
                Payload =
                {
                    ["unit"] = dispatched.UnitTypeId,
                    ["amount"] = (-dispatched.Count).ToString(),
                    ["reason"] = "threat_dispatch"
                }
            });
        }

        _deploymentService.EnsureGarrisonPlacements(sourceSite, queries.GetSite(sourceSite.SiteId));
        result.Events.Add(new GameEvent
        {
            Kind = "SiteGarrisonDispatched",
            Tick = state.WorldTick,
            TargetIds = { sourceSite.SiteId, rule.TargetSiteId },
            Payload =
            {
                ["rule"] = rule.Id,
                ["forces"] = BuildForceSummary(dispatchedForces),
                ["remaining"] = BuildForceSummary(sourceSite.Garrison)
            }
        });
        return true;
    }

    private static void ProgressThreats(StrategicWorldState state, StrategicWorldDefinitionQueries queries, WorldTickResult result)
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
            string factionName = StrategicWorldDisplayNames.GetFactionLabel(queries, ResolveThreatFactionId(state, threat), "敌方");
            string targetName = StrategicWorldDisplayNames.GetSiteLabel(queries, threat.TargetSiteId);
            result.Messages.Add($"{factionName} Raid 已抵达 {targetName}，必须处理。");
            GameLog.Info(nameof(WorldTickService), $"ThreatAttacking id={threat.Id} target={threat.TargetSiteId}");
        }
    }

    private static string ResolveThreatFactionId(StrategicWorldState state, EnemyThreatPlan threat)
    {
        if (state != null &&
            !string.IsNullOrWhiteSpace(threat?.WorldArmyId) &&
            state.ArmyStates.TryGetValue(threat.WorldArmyId, out WorldArmyState army) &&
            !string.IsNullOrWhiteSpace(army.OwnerFactionId))
        {
            return army.OwnerFactionId;
        }

        if (state != null &&
            !string.IsNullOrWhiteSpace(threat?.SourceSiteId) &&
            state.SiteStates.TryGetValue(threat.SourceSiteId, out WorldSiteState sourceSite) &&
            !string.IsNullOrWhiteSpace(sourceSite.OwnerFactionId))
        {
            return sourceSite.OwnerFactionId;
        }

        return StrategicWorldIds.FactionUndead;
    }

    private static WorldArmyState CreateThreatArmy(
        StrategicWorldState state,
        StrategicWorldDefinition definition,
        StrategicWorldDefinitionQueries queries,
        ThreatRuleDefinition rule,
        EnemyThreatPlan threat,
        string armyId,
        IEnumerable<GarrisonState> dispatchedForces)
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
        foreach (GarrisonState unit in dispatchedForces.Where(item => item.Count > 0))
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

    private static bool ShouldAutoProduceGarrison(
        int worldTick,
        WorldSiteState siteState,
        SiteAutoGarrisonProductionDefinition production)
    {
        if (siteState == null ||
            production == null ||
            production.BatchUnits.Count == 0)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(production.FactionId) &&
            !string.Equals(siteState.OwnerFactionId, production.FactionId, StringComparison.Ordinal))
        {
            return false;
        }

        int intervalTicks = Math.Max(1, production.IntervalTicks);
        if (worldTick <= 0 || worldTick % intervalTicks != 0)
        {
            return false;
        }

        int batchSize = production.BatchUnits
            .Where(unit => unit.Count > 0)
            .Sum(unit => unit.Count);
        if (batchSize <= 0)
        {
            return false;
        }

        int storedUnits = GetTotalGarrisonCount(siteState.Garrison);
        return production.MaxStoredUnits <= 0 ||
               storedUnits + batchSize <= production.MaxStoredUnits;
    }

    private static List<GarrisonState> ProduceGarrisonBatch(
        WorldSiteState siteState,
        SiteAutoGarrisonProductionDefinition production)
    {
        List<GarrisonState> producedUnits = new();
        foreach (GarrisonDefinition unit in production.BatchUnits.Where(item => item.Count > 0 && !string.IsNullOrWhiteSpace(item.UnitTypeId)))
        {
            AddOrMergeGarrison(siteState, unit.UnitTypeId, unit.Count, unit.Morale);
            producedUnits.Add(new GarrisonState
            {
                UnitTypeId = unit.UnitTypeId,
                Count = unit.Count,
                Morale = unit.Morale
            });
        }

        return producedUnits;
    }

    private static bool HasRequiredForces(
        IEnumerable<GarrisonState> available,
        IEnumerable<GarrisonDefinition> required)
    {
        if (required == null)
        {
            return false;
        }

        return required
            .Where(unit => unit.Count > 0 && !string.IsNullOrWhiteSpace(unit.UnitTypeId))
            .All(unit => GetGarrisonCount(available, unit.UnitTypeId) >= unit.Count);
    }

    private static GarrisonState ExtractGarrison(
        WorldSiteState siteState,
        string unitTypeId,
        int count,
        int fallbackMorale)
    {
        if (siteState == null || string.IsNullOrWhiteSpace(unitTypeId) || count <= 0)
        {
            return null;
        }

        GarrisonState garrison = siteState.Garrison.FirstOrDefault(item => item.UnitTypeId == unitTypeId);
        if (garrison == null || garrison.Count < count)
        {
            return null;
        }

        int morale = garrison.Morale > 0 ? garrison.Morale : fallbackMorale;
        garrison.Count -= count;
        if (garrison.Count <= 0)
        {
            siteState.Garrison.Remove(garrison);
        }

        return new GarrisonState
        {
            UnitTypeId = unitTypeId,
            Count = count,
            Morale = morale
        };
    }

    private static void AddOrMergeGarrison(
        WorldSiteState siteState,
        string unitTypeId,
        int count,
        int morale)
    {
        if (siteState == null || string.IsNullOrWhiteSpace(unitTypeId) || count <= 0)
        {
            return;
        }

        GarrisonState existing = siteState.Garrison.FirstOrDefault(item => item.UnitTypeId == unitTypeId);
        if (existing == null)
        {
            siteState.Garrison.Add(new GarrisonState
            {
                UnitTypeId = unitTypeId,
                Count = count,
                Morale = morale
            });
            return;
        }

        existing.Count += count;
        existing.Morale = Math.Max(existing.Morale, morale);
    }

    private static int GetGarrisonCount(IEnumerable<GarrisonState> garrison, string unitTypeId)
    {
        return garrison?
            .Where(item => item.UnitTypeId == unitTypeId)
            .Sum(item => Math.Max(item.Count, 0)) ?? 0;
    }

    private static int GetTotalGarrisonCount(IEnumerable<GarrisonState> garrison)
    {
        return garrison?.Sum(item => Math.Max(item.Count, 0)) ?? 0;
    }

    private static string BuildForceSummary<TForce>(IEnumerable<TForce> forces)
    {
        if (forces == null)
        {
            return "none";
        }

        List<string> summary = new();
        foreach (TForce force in forces)
        {
            switch (force)
            {
                case GarrisonState state when state.Count > 0 && !string.IsNullOrWhiteSpace(state.UnitTypeId):
                    summary.Add($"{state.UnitTypeId}x{state.Count}");
                    break;
                case GarrisonDefinition definition when definition.Count > 0 && !string.IsNullOrWhiteSpace(definition.UnitTypeId):
                    summary.Add($"{definition.UnitTypeId}x{definition.Count}");
                    break;
            }
        }

        return summary.Count == 0 ? "none" : string.Join(",", summary);
    }

    private static string BuildThreatArmyId(string threatId) => $"{threatId}:army";
}
