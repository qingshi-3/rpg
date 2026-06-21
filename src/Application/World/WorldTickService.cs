using System.Linq;
using System;
using System.Collections.Generic;
using Rpg.Definitions.World;
using Rpg.Domain.World;
using Rpg.Infrastructure.Logging;

namespace Rpg.Application.World;

public sealed class WorldTickService
{
    private readonly WorldSiteModeTransitionService _siteModeTransitions = new();
    private readonly WorldOpportunityService _opportunityService = new();
    private readonly WorldSiteDeploymentService _deploymentService = new();

    public WorldTickResult AdvanceWorldTick(StrategicWorldState state, StrategicWorldDefinition definition)
    {
        StrategicWorldDefinitionQueries queries = new(definition);
        state.WorldTick++;

        WorldTickResult result = new() { WorldTick = state.WorldTick };
        GameLog.Info(nameof(WorldTickService), $"WorldTickStarted tick={state.WorldTick}");

        _siteModeTransitions.ClearAftermathSites(state, result);
        ApplyAutoGarrisonProduction(state, queries, result);
        _opportunityService.AdvanceOpportunities(state, definition, result);

        result.Events.Add(new GameEvent
        {
            Kind = "WorldTickAdvanced",
            Tick = state.WorldTick
        });

        GameLog.Info(
            nameof(WorldTickService),
            $"WorldTickEnded tick={state.WorldTick} population={state.PlayerResources.GetAvailable(StrategicWorldIds.ResourcePopulation)}/{state.PlayerResources.GetAmount(StrategicWorldIds.ResourcePopulation)} economy={state.PlayerResources.GetAmount(StrategicWorldIds.ResourceEconomy)} stone={state.PlayerResources.GetAmount(StrategicWorldIds.ResourceStone)}");
        return result;
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

}
