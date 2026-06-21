using System;
using System.Collections.Generic;
using System.Linq;
using Rpg.Application.Battle;
using Rpg.Definitions.World;
using Rpg.Domain.World;
using Rpg.Infrastructure.Logging;

namespace Rpg.Application.World;

public sealed class WorldActionResolver
{
    private readonly WorldConditionEvaluator _conditionEvaluator = new();
    private readonly WorldTickService _worldTickService = new();
    private readonly WorldBattleRequestBuilder _battleRequestBuilder = new();
    private readonly WorldSiteModeTransitionService _siteModeTransitions = new();
    private readonly WorldGarrisonMutationService _garrisonMutations = new();
    private readonly Func<string, string> _unitDisplayNameResolver;

    public WorldActionResolver(Func<string, string> unitDisplayNameResolver = null)
    {
        _unitDisplayNameResolver = unitDisplayNameResolver;
    }

    public IReadOnlyList<WorldActionViewModel> GetAvailableActions(
        StrategicWorldState state,
        StrategicWorldDefinition definition,
        string selectedSiteId)
    {
        StrategicWorldDefinitionQueries queries = new(definition);
        var viewModels = new List<WorldActionViewModel>();

        foreach (WorldActionDefinition action in definition.ActionDefinitions)
        {
            if (!ShouldShowAction(state, action, selectedSiteId))
            {
                continue;
            }

            WorldActionRequest request = BuildRequestForViewModel(action, selectedSiteId);
            bool enabled = CanApply(state, definition, action, request, out string failureReason);
            viewModels.Add(new WorldActionViewModel
            {
                ActionId = action.Id,
                DisplayName = action.DisplayName,
                Description = action.Description,
                IsEnabled = enabled,
                DisabledReason = enabled ? "" : FormatFailureReason(failureReason, queries),
                CostLines = action.Costs.Select(cost => $"{StrategicWorldDisplayNames.GetResourceLabel(queries, cost.ResourceId)} {cost.Amount}").ToList(),
                EffectLines = BuildEffectLines(action, queries),
                WarningLines = new List<string>(),
                TargetSiteId = request.TargetSiteId
            });
        }

        return viewModels;
    }

    public WorldActionResult Apply(
        StrategicWorldState state,
        StrategicWorldDefinition definition,
        WorldActionRequest request,
        string returnScenePath,
        string siteScenePath)
    {
        StrategicWorldDefinitionQueries queries = new(definition);
        WorldActionDefinition action = queries.GetAction(request.ActionId);
        if (action == null)
        {
            return WorldActionResult.Failed(request.ActionId, "action_missing", "找不到行动定义。");
        }

        if (!CanApply(state, definition, action, request, out string failureReason))
        {
            return WorldActionResult.Failed(action.Id, failureReason, FormatFailureReason(failureReason, queries));
        }

        WorldActionResult result = new()
        {
            Success = true,
            ActionId = action.Id,
            Message = $"{action.DisplayName} 已执行。"
        };

        foreach (ResourceAmountDefinition cost in action.Costs)
        {
            if (!state.PlayerResources.Spend(cost.ResourceId, cost.Amount))
            {
                string spendFailureReason = BuildResourceShortageReason(cost.ResourceId);
                return WorldActionResult.Failed(action.Id, spendFailureReason, FormatFailureReason(spendFailureReason, queries));
            }

            result.Events.Add(new GameEvent
            {
                Kind = "ResourceChanged",
                Tick = state.WorldTick,
                TargetIds = { cost.ResourceId },
                Payload = { ["amount"] = (-cost.Amount).ToString(), ["reason"] = action.Id }
            });
        }

        foreach (WorldEffectDefinition effect in action.Effects)
        {
            ApplyEffect(state, definition, queries, request, effect, result, returnScenePath, siteScenePath);
        }

        if (result.BattleStartRequest != null)
        {
            GameLog.Info(nameof(WorldActionResolver), $"WorldActionStartedBattle action={action.Id} request={result.BattleStartRequest.RequestId}");
            return result;
        }

        if (action.AdvancesWorldTick)
        {
            WorldTickResult tickResult = _worldTickService.AdvanceWorldTick(state, definition);
            result.Events.AddRange(tickResult.Events);
            if (tickResult.Messages.Count > 0)
            {
                result.Message = $"{result.Message}\n{string.Join("\n", tickResult.Messages)}";
            }
        }

        GameLog.Info(nameof(WorldActionResolver), $"WorldActionApplied action={action.Id} tick={state.WorldTick}");
        return result;
    }

    private bool CanApply(
        StrategicWorldState state,
        StrategicWorldDefinition definition,
        WorldActionDefinition action,
        WorldActionRequest request,
        out string failureReason)
    {
        StrategicWorldDefinitionQueries queries = new(definition);
        failureReason = "";

        if (action.Scope != WorldActionScope.Run &&
            IsTargetSiteInWartime(state, request))
        {
            failureReason = "site_in_wartime";
            return false;
        }

        foreach (ResourceAmountDefinition cost in action.Costs)
        {
            if (!state.PlayerResources.CanSpend(cost.ResourceId, cost.Amount))
            {
                failureReason = BuildResourceShortageReason(cost.ResourceId);
                return false;
            }
        }

        return _conditionEvaluator.AreConditionsMet(state, queries, request, action.Conditions, out failureReason);
    }

    private static bool ShouldShowAction(StrategicWorldState state, WorldActionDefinition action, string selectedSiteId)
    {
        if (action.Scope == WorldActionScope.Run)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(selectedSiteId))
        {
            return false;
        }

        return action.Id switch
        {
            StrategicWorldIds.ActionTrainMilitia => selectedSiteId == StrategicWorldIds.SitePlayerCamp,
            _ => true
        };
    }

    private static WorldActionRequest BuildRequestForViewModel(
        WorldActionDefinition action,
        string selectedSiteId)
    {
        string targetSiteId = action.Id switch
        {
            StrategicWorldIds.ActionTrainMilitia => StrategicWorldIds.SitePlayerCamp,
            _ => selectedSiteId
        };

        return new WorldActionRequest
        {
            ActionId = action.Id,
            ActorFactionId = StrategicWorldIds.FactionPlayer,
            SourceSiteId = selectedSiteId,
            TargetSiteId = targetSiteId
        };
    }

    private void ApplyEffect(
        StrategicWorldState state,
        StrategicWorldDefinition definition,
        StrategicWorldDefinitionQueries queries,
        WorldActionRequest request,
        WorldEffectDefinition effect,
        WorldActionResult result,
        string returnScenePath,
        string siteScenePath)
    {
        string siteId = WorldConditionEvaluator.ResolveSiteId(effect.SiteId, request);
        WorldSiteState site = !string.IsNullOrWhiteSpace(siteId) && state.SiteStates.TryGetValue(siteId, out WorldSiteState value)
            ? value
            : null;

        switch (effect.Kind)
        {
            case WorldEffectKind.AddResource:
                state.PlayerResources.Add(effect.ResourceId, effect.Amount);
                AddEvent(result, state, "ResourceChanged", effect.ResourceId, ("amount", effect.Amount.ToString()));
                break;
            case WorldEffectKind.SetSiteControlState:
                if (site != null)
                {
                    site.ControlState = effect.ControlState;
                    AddEvent(result, state, "SiteControlChanged", site.SiteId, ("state", effect.ControlState.ToString()));
                }
                break;
            case WorldEffectKind.SetSiteOwner:
                if (site != null)
                {
                    site.OwnerFactionId = effect.FactionId;
                    AddEvent(result, state, "SiteOwnerChanged", site.SiteId, ("owner", effect.FactionId));
                }
                break;
            case WorldEffectKind.AddGarrison:
                if (site != null)
                {
                    _garrisonMutations.Add(site, effect.UnitTypeId, effect.Amount);
                    AddEvent(result, state, "GarrisonChanged", site.SiteId, ("unit", effect.UnitTypeId), ("amount", effect.Amount.ToString()));
                }
                break;
            case WorldEffectKind.RemoveGarrison:
                if (site != null)
                {
                    _garrisonMutations.Remove(site, effect.UnitTypeId, effect.Amount);
                    AddEvent(result, state, "GarrisonChanged", site.SiteId, ("unit", effect.UnitTypeId), ("amount", (-effect.Amount).ToString()));
                }
                break;
            case WorldEffectKind.CreateArmy:
                WorldArmyState army = CreateWorldArmy(state, queries, request, effect);
                if (army != null)
                {
                    state.ArmyStates[army.ArmyId] = army;
                    result.Message = $"{result.Message}\n玩家部队已出发，正在向目标场域行军。";
                    result.Events.Add(new GameEvent
                    {
                        Kind = "WorldArmyCreated",
                        Tick = state.WorldTick,
                        TargetIds = { army.ArmyId, army.SourceSiteId, army.TargetSiteId },
                        Payload =
                        {
                            ["owner"] = army.OwnerFactionId,
                            ["intent"] = army.Intent.ToString(),
                            ["unit"] = effect.UnitTypeId,
                            ["amount"] = effect.Amount.ToString()
                        }
                    });
                    GameLog.Info(nameof(WorldActionResolver), $"WorldArmyCreated army={army.ArmyId} owner={army.OwnerFactionId} source={army.SourceSiteId} target={army.TargetSiteId} intent={army.Intent}");
                }
                break;
            case WorldEffectKind.StartBattle:
                result.BattleStartRequest = BuildBattleRequest(state, definition, effect, request, returnScenePath, siteScenePath);
                if (result.BattleStartRequest != null && site != null)
                {
                    WorldSiteModeTransitionService.AddEvent(
                        result,
                        _siteModeTransitions.EnterWartime(site, state.WorldTick, "battle_requested", result.BattleStartRequest.RequestId));
                    AddEvent(result, state, "BattleRequested", site.SiteId, ("kind", result.BattleStartRequest.BattleKind.ToString()));
                }
                break;
            case WorldEffectKind.AddSiteTag:
                if (site != null && !site.ActiveTags.Contains(effect.Tag))
                {
                    site.ActiveTags.Add(effect.Tag);
                }
                break;
            case WorldEffectKind.RemoveSiteTag:
                site?.ActiveTags.Remove(effect.Tag);
                break;
        }
    }

    private BattleStartRequest BuildBattleRequest(
        StrategicWorldState state,
        StrategicWorldDefinition definition,
        WorldEffectDefinition effect,
        WorldActionRequest request,
        string returnScenePath,
        string siteScenePath)
    {
        if (!Enum.TryParse(effect.BattleKind, out BattleKind battleKind))
        {
            battleKind = BattleKind.Unknown;
        }

        if (battleKind == BattleKind.AssaultSite)
        {
            return _battleRequestBuilder.BuildAssaultBonefieldRequest(state, definition, returnScenePath, siteScenePath);
        }

        return null;
    }

    private static WorldArmyState CreateWorldArmy(
        StrategicWorldState state,
        StrategicWorldDefinitionQueries queries,
        WorldActionRequest request,
        WorldEffectDefinition effect)
    {
        string sourceSiteId = WorldConditionEvaluator.ResolveSiteId(effect.SiteId, request);
        string targetSiteId = !string.IsNullOrWhiteSpace(effect.TargetId)
            ? effect.TargetId
            : request.TargetSiteId;
        WorldSiteDefinition sourceDefinition = queries.GetSite(sourceSiteId);
        WorldSiteDefinition targetDefinition = queries.GetSite(targetSiteId);
        if (sourceDefinition == null || targetDefinition == null)
        {
            GameLog.Warn(nameof(WorldActionResolver), $"WorldArmyCreateSkipped source={sourceSiteId} target={targetSiteId}");
            return null;
        }

        WorldArmyIntent intent = effect.ArmyIntent == WorldArmyIntent.None
            ? WorldArmyIntent.ReinforceSite
            : effect.ArmyIntent;
        WorldArmyState army = new()
        {
            ArmyId = BuildWorldArmyId(state, request.ActionId),
            OwnerFactionId = WorldConditionEvaluator.ResolveFactionId(effect.FactionId, request),
            SourceSiteId = sourceSiteId,
            TargetSiteId = targetSiteId,
            MoveSpeed = 56.0f,
            Radius = 16.0f,
            Status = WorldArmyStatus.Moving,
            Intent = intent,
            CreatedTick = state.WorldTick
        };
        army.WorldPosition = sourceDefinition.MapPosition;
        army.Destination = targetDefinition.MapPosition;
        army.ClearNavigationPath();

        if (!string.IsNullOrWhiteSpace(effect.UnitTypeId) && effect.Amount > 0)
        {
            army.GarrisonUnits.Add(new GarrisonState
            {
                UnitTypeId = effect.UnitTypeId,
                Count = effect.Amount
            });
        }

        return army;
    }

    private static string BuildWorldArmyId(StrategicWorldState state, string actionId)
    {
        string safeActionId = string.IsNullOrWhiteSpace(actionId) ? "world_action" : actionId;
        string baseId = $"{safeActionId}:{state.WorldTick}:army";
        if (!state.ArmyStates.ContainsKey(baseId))
        {
            return baseId;
        }

        int suffix = 2;
        string candidate;
        do
        {
            candidate = $"{baseId}:{suffix}";
            suffix++;
        } while (state.ArmyStates.ContainsKey(candidate));

        return candidate;
    }

    private static void AddEvent(WorldActionResult result, StrategicWorldState state, string kind, string targetId, params (string Key, string Value)[] payload)
    {
        GameEvent gameEvent = new()
        {
            Kind = kind,
            Tick = state.WorldTick,
            TargetIds = { targetId }
        };

        foreach ((string key, string value) in payload)
        {
            gameEvent.Payload[key] = value;
        }

        result.Events.Add(gameEvent);
    }

    private static bool IsTargetSiteInWartime(StrategicWorldState state, WorldActionRequest request)
    {
        string siteId = !string.IsNullOrWhiteSpace(request.TargetSiteId)
            ? request.TargetSiteId
            : request.SourceSiteId;
        return !string.IsNullOrWhiteSpace(siteId) &&
               state.SiteStates.TryGetValue(siteId, out WorldSiteState site) &&
               site.SiteMode == WorldSiteMode.Wartime;
    }

    private List<string> BuildEffectLines(WorldActionDefinition action, StrategicWorldDefinitionQueries queries)
    {
        string playerCamp = StrategicWorldDisplayNames.GetSiteLabel(queries, StrategicWorldIds.SitePlayerCamp, "玩家营地");
        string militia = ResolveUnitLabel(StrategicWorldIds.UnitMilitia);

        return action.Id switch
        {
            StrategicWorldIds.ActionTrainMilitia => new List<string> { $"{playerCamp}{militia} +1" },
            StrategicWorldIds.ActionWaitTick => new List<string> { "推进世界步", "结算机会和驻军动向" },
            _ => action.Effects.Select(effect => effect.Kind.ToString()).ToList()
        };
    }

    private string ResolveUnitLabel(string unitTypeId)
    {
        if (string.IsNullOrWhiteSpace(unitTypeId))
        {
            return "战斗单位";
        }

        string displayName = _unitDisplayNameResolver?.Invoke(unitTypeId);
        if (!string.IsNullOrWhiteSpace(displayName))
        {
            return displayName;
        }

        return unitTypeId == StrategicWorldIds.UnitMilitia ? "民兵" : unitTypeId;
    }

    private static string BuildResourceShortageReason(string resourceId)
    {
        return string.IsNullOrWhiteSpace(resourceId)
            ? "not_enough_resource"
            : $"not_enough_resource:{resourceId}";
    }

    public static string FormatFailureReason(string reason)
    {
        return FormatFailureReason(reason, null);
    }

    public static string FormatFailureReason(string reason, StrategicWorldDefinitionQueries queries)
    {
        return reason switch
        {
            string value when TryGetResourceShortageId(value, out string resourceId) => $"{StrategicWorldDisplayNames.GetResourceLabel(queries, resourceId)}不足",
            "not_enough_resource" => "资源不足",
            "not_enough_population" => $"{StrategicWorldDisplayNames.GetResourceLabel(queries, StrategicWorldIds.ResourcePopulation, "人口")}不足",
            "site_not_owned" => "场域不属于玩家",
            "not_enough_garrison" => "驻军不足",
            "no_expedition_units" => "没有可出征英雄或小兵",
            "no_expedition_hero" => "没有可出征英雄",
            "missing_source_site" => "找不到出发场域",
            "missing_target_site" => "找不到目标场域",
            "source_site_not_owned" => "只能从己方场域出征",
            "target_site_not_owned" => "目标场域不属于玩家",
            "same_site_target" => "目标不能是出发场域",
            "unsupported_expedition_intent" => "不支持的出征意图",
            "garrison_zone_full" => "驻军区已满，无法进驻",
            "garrison_zone_missing" => "未配置默认驻军区",
            "site_not_attackable" => "当前场域不可攻打",
            "site_in_wartime" => "场域正在战时，不能执行经营行动",
            "army_already_en_route" => "已有玩家部队正在执行该目标",
            "expedition_capacity_full" => "出征队列已满",
            _ => string.IsNullOrWhiteSpace(reason) ? "无法执行" : reason
        };
    }

    private static bool TryGetResourceShortageId(string reason, out string resourceId)
    {
        const string prefix = "not_enough_resource:";
        if (!string.IsNullOrWhiteSpace(reason) &&
            reason.StartsWith(prefix, StringComparison.Ordinal))
        {
            resourceId = reason[prefix.Length..];
            return !string.IsNullOrWhiteSpace(resourceId);
        }

        resourceId = "";
        return false;
    }
}
