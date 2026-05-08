using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Rpg.Definitions.World;
using Rpg.Domain.World;
using Rpg.Infrastructure.Logging;

namespace Rpg.Application.World;

public sealed class WorldOpportunityService
{
    public void AdvanceOpportunities(StrategicWorldState state, StrategicWorldDefinition definition, WorldTickResult result)
    {
        if (state == null || definition == null || result == null)
        {
            return;
        }

        StrategicWorldDefinitionQueries queries = new(definition);
        ExpireOpportunities(state, queries, result);
        TickCooldowns(state);
        GenerateOpportunities(state, definition, queries, result);
    }

    public WorldActionResult CompleteOpportunity(
        StrategicWorldState state,
        StrategicWorldDefinition definition,
        string opportunityId)
    {
        if (state == null || definition == null || string.IsNullOrWhiteSpace(opportunityId))
        {
            return WorldActionResult.Failed("complete_opportunity", "missing_opportunity", "找不到野外机会。");
        }

        if (!state.OpportunityStates.TryGetValue(opportunityId, out WorldOpportunityState opportunity) ||
            opportunity.Status != WorldOpportunityStatus.Active)
        {
            return WorldActionResult.Failed("complete_opportunity", "opportunity_not_active", "野外机会已经消失。");
        }

        StrategicWorldDefinitionQueries queries = new(definition);
        WorldOpportunityDefinition opportunityDefinition = queries.GetOpportunity(opportunity.DefinitionId);
        if (opportunityDefinition == null)
        {
            return WorldActionResult.Failed("complete_opportunity", "missing_opportunity_definition", "野外机会配置缺失。");
        }

        opportunity.Status = WorldOpportunityStatus.Completed;
        string rewardText = ApplyRewards(state, opportunityDefinition);
        string message = string.IsNullOrWhiteSpace(opportunityDefinition.CompletionText)
            ? $"完成{opportunityDefinition.DisplayName}。"
            : opportunityDefinition.CompletionText;
        if (!string.IsNullOrWhiteSpace(rewardText))
        {
            message = $"{message}\n{rewardText}";
        }

        WorldActionResult result = new()
        {
            Success = true,
            ActionId = "complete_opportunity",
            Message = message
        };
        result.Events.Add(new GameEvent
        {
            Kind = "WorldOpportunityCompleted",
            Tick = state.WorldTick,
            TargetIds = { opportunity.OpportunityId, opportunity.DefinitionId },
            Payload = { ["spawnPoint"] = opportunity.SpawnPointId }
        });
        GameLog.Info(nameof(WorldOpportunityService), $"WorldOpportunityCompleted id={opportunity.OpportunityId} definition={opportunity.DefinitionId}");
        return result;
    }

    private static void ExpireOpportunities(
        StrategicWorldState state,
        StrategicWorldDefinitionQueries queries,
        WorldTickResult result)
    {
        foreach (WorldOpportunityState opportunity in state.OpportunityStates.Values)
        {
            if (opportunity.Status != WorldOpportunityStatus.Active || opportunity.ExpiresTick > state.WorldTick)
            {
                continue;
            }

            opportunity.Status = WorldOpportunityStatus.Expired;
            string name = queries.GetOpportunity(opportunity.DefinitionId)?.DisplayName ?? opportunity.DefinitionId;
            result.Messages.Add($"野外机会已消失：{name}。");
            result.Events.Add(new GameEvent
            {
                Kind = "WorldOpportunityExpired",
                Tick = state.WorldTick,
                TargetIds = { opportunity.OpportunityId, opportunity.DefinitionId }
            });
            GameLog.Info(nameof(WorldOpportunityService), $"WorldOpportunityExpired id={opportunity.OpportunityId} definition={opportunity.DefinitionId}");
        }
    }

    private static void TickCooldowns(StrategicWorldState state)
    {
        foreach (string ruleId in state.OpportunityRuleCooldowns.Keys.ToArray())
        {
            int remaining = Math.Max(0, state.OpportunityRuleCooldowns[ruleId] - 1);
            if (remaining <= 0)
            {
                state.OpportunityRuleCooldowns.Remove(ruleId);
            }
            else
            {
                state.OpportunityRuleCooldowns[ruleId] = remaining;
            }
        }
    }

    private static void GenerateOpportunities(
        StrategicWorldState state,
        StrategicWorldDefinition definition,
        StrategicWorldDefinitionQueries queries,
        WorldTickResult result)
    {
        foreach (OpportunitySpawnRuleDefinition rule in definition.OpportunitySpawnRules)
        {
            if (!CanCheckRule(state, rule))
            {
                continue;
            }

            int activeCount = state.OpportunityStates.Values.Count(item =>
                item.Status == WorldOpportunityStatus.Active && item.SpawnRuleId == rule.Id);
            if (activeCount >= Math.Max(1, rule.MaxActiveCount))
            {
                continue;
            }

            Random random = BuildDeterministicRandom(state, rule, activeCount);
            if (random.Next(0, 1000) >= Math.Clamp(rule.SpawnChancePermille, 0, 1000))
            {
                continue;
            }

            WorldOpportunityDefinition opportunityDefinition = PickOpportunityDefinition(definition, rule, random);
            OpportunitySpawnPointDefinition spawnPoint = PickSpawnPoint(queries, rule, random);
            if (opportunityDefinition == null || spawnPoint == null)
            {
                GameLog.Warn(nameof(WorldOpportunityService), $"WorldOpportunitySpawnSkipped rule={rule.Id} reason=missing_definition_or_spawn_point");
                continue;
            }

            WorldOpportunityState opportunity = CreateOpportunityState(state, rule, opportunityDefinition, spawnPoint, random);
            state.OpportunityStates[opportunity.OpportunityId] = opportunity;
            state.OpportunityRuleCooldowns[rule.Id] = Math.Max(0, rule.CooldownTicks);
            result.Messages.Add($"野外出现：{opportunityDefinition.DisplayName}（{spawnPoint.DisplayName}）。");
            result.Events.Add(new GameEvent
            {
                Kind = "WorldOpportunitySpawned",
                Tick = state.WorldTick,
                TargetIds = { opportunity.OpportunityId, opportunity.DefinitionId, spawnPoint.Id },
                Payload = { ["rule"] = rule.Id, ["pool"] = rule.PoolId }
            });
            GameLog.Info(nameof(WorldOpportunityService), $"WorldOpportunitySpawned id={opportunity.OpportunityId} definition={opportunity.DefinitionId} point={spawnPoint.Id} tick={state.WorldTick}");
        }
    }

    private static bool CanCheckRule(StrategicWorldState state, OpportunitySpawnRuleDefinition rule)
    {
        if (rule == null || string.IsNullOrWhiteSpace(rule.Id) || string.IsNullOrWhiteSpace(rule.PoolId))
        {
            return false;
        }

        if (state.WorldTick < rule.MinWorldTick || state.OpportunityRuleCooldowns.ContainsKey(rule.Id))
        {
            return false;
        }

        int interval = Math.Max(1, rule.CheckIntervalTicks);
        return (state.WorldTick - rule.MinWorldTick) % interval == 0;
    }

    private static Random BuildDeterministicRandom(StrategicWorldState state, OpportunitySpawnRuleDefinition rule, int activeCount)
    {
        int seed = CombineStableHash(
            state.Seed,
            state.WorldTick,
            activeCount,
            StableStringHash(rule.Id),
            StableStringHash(state.RunId));
        return new Random(seed);
    }

    private static int CombineStableHash(params int[] values)
    {
        unchecked
        {
            int hash = 17;
            foreach (int value in values)
            {
                hash = hash * 31 + value;
            }

            return hash;
        }
    }

    private static int StableStringHash(string value)
    {
        unchecked
        {
            int hash = 23;
            foreach (char character in value ?? string.Empty)
            {
                hash = hash * 31 + character;
            }

            return hash;
        }
    }

    private static WorldOpportunityDefinition PickOpportunityDefinition(
        StrategicWorldDefinition definition,
        OpportunitySpawnRuleDefinition rule,
        Random random)
    {
        WorldOpportunityDefinition[] candidates = definition.OpportunityDefinitions
            .Where(item => item != null && item.PoolId == rule.PoolId && item.Weight > 0)
            .ToArray();
        if (candidates.Length == 0)
        {
            return null;
        }

        int totalWeight = candidates.Sum(item => Math.Max(0, item.Weight));
        int roll = random.Next(0, totalWeight);
        foreach (WorldOpportunityDefinition candidate in candidates)
        {
            roll -= Math.Max(0, candidate.Weight);
            if (roll < 0)
            {
                return candidate;
            }
        }

        return candidates[^1];
    }

    private static OpportunitySpawnPointDefinition PickSpawnPoint(
        StrategicWorldDefinitionQueries queries,
        OpportunitySpawnRuleDefinition rule,
        Random random)
    {
        OpportunitySpawnPointDefinition[] candidates = rule.SpawnPointIds
            .Select(queries.GetOpportunitySpawnPoint)
            .Where(item => item != null)
            .ToArray();
        return candidates.Length == 0 ? null : candidates[random.Next(0, candidates.Length)];
    }

    private static WorldOpportunityState CreateOpportunityState(
        StrategicWorldState state,
        OpportunitySpawnRuleDefinition rule,
        WorldOpportunityDefinition definition,
        OpportunitySpawnPointDefinition spawnPoint,
        Random random)
    {
        Vector2 offset = RandomOffset(random, Math.Min(Math.Max(0.0f, rule.PositionJitterRadius), Math.Max(0.0f, spawnPoint.Radius)));
        WorldOpportunityState opportunity = new()
        {
            OpportunityId = BuildOpportunityId(state, definition, spawnPoint),
            DefinitionId = definition.Id,
            SpawnRuleId = rule.Id,
            SpawnPointId = spawnPoint.Id,
            CreatedTick = state.WorldTick,
            ExpiresTick = state.WorldTick + Math.Max(1, definition.DurationTicks),
            Status = WorldOpportunityStatus.Active,
            Tags = definition.Tags.ToList()
        };
        opportunity.WorldPosition = spawnPoint.MapPosition + offset;
        return opportunity;
    }

    private static Vector2 RandomOffset(Random random, float radius)
    {
        if (radius <= 0.0f)
        {
            return Vector2.Zero;
        }

        double angle = random.NextDouble() * Math.PI * 2.0;
        double distance = Math.Sqrt(random.NextDouble()) * radius;
        return new Vector2((float)(Math.Cos(angle) * distance), (float)(Math.Sin(angle) * distance));
    }

    private static string BuildOpportunityId(
        StrategicWorldState state,
        WorldOpportunityDefinition definition,
        OpportunitySpawnPointDefinition spawnPoint)
    {
        string baseId = $"opportunity:{definition.Id}:{spawnPoint.Id}:{state.WorldTick}";
        if (!state.OpportunityStates.ContainsKey(baseId))
        {
            return baseId;
        }

        int suffix = 2;
        string candidate;
        do
        {
            candidate = $"{baseId}:{suffix}";
            suffix++;
        } while (state.OpportunityStates.ContainsKey(candidate));

        return candidate;
    }

    private static string ApplyRewards(StrategicWorldState state, WorldOpportunityDefinition definition)
    {
        List<string> lines = new();
        foreach (ResourceAmountDefinition reward in definition.CompletionRewards.Where(item => item.Amount != 0 && !string.IsNullOrWhiteSpace(item.ResourceId)))
        {
            state.PlayerResources.Add(reward.ResourceId, reward.Amount);
            lines.Add($"{GetResourceLabel(reward.ResourceId)} {(reward.Amount > 0 ? "+" : "")}{reward.Amount}");
        }

        return lines.Count == 0 ? "" : $"获得：{string.Join("，", lines)}。";
    }

    private static string GetResourceLabel(string resourceId)
    {
        return resourceId switch
        {
            StrategicWorldIds.ResourcePopulation => "人口",
            StrategicWorldIds.ResourceEconomy => "经济",
            StrategicWorldIds.ResourceStone => "石材",
            _ => resourceId
        };
    }
}
