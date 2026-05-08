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
    private readonly WorldThreatService _threatService = new();
    private readonly WorldBattleRequestBuilder _battleRequestBuilder = new();
    private readonly WorldSiteModeTransitionService _siteModeTransitions = new();

    public IReadOnlyList<WorldActionViewModel> GetAvailableActions(
        StrategicWorldState state,
        StrategicWorldDefinition definition,
        string selectedSiteId,
        string selectedThreatId = "")
    {
        StrategicWorldDefinitionQueries queries = new(definition);
        var viewModels = new List<WorldActionViewModel>();

        foreach (WorldActionDefinition action in definition.ActionDefinitions)
        {
            if (!ShouldShowAction(state, action, selectedSiteId, selectedThreatId))
            {
                continue;
            }

            WorldActionRequest request = BuildRequestForViewModel(action, selectedSiteId, selectedThreatId);
            bool enabled = CanApply(state, definition, action, request, out string failureReason);
            viewModels.Add(new WorldActionViewModel
            {
                ActionId = action.Id,
                DisplayName = action.DisplayName,
                Description = action.Description,
                IsEnabled = enabled,
                DisabledReason = enabled ? "" : FormatFailureReason(failureReason),
                CostLines = action.Costs.Select(cost => $"{GetResourceDisplayName(queries, cost.ResourceId)} {cost.Amount}").ToList(),
                EffectLines = BuildEffectLines(action),
                WarningLines = BuildWarningLines(state, action, selectedThreatId),
                TargetSiteId = request.TargetSiteId,
                TargetSlotId = request.TargetSlotId,
                ThreatId = request.ThreatId
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
            return WorldActionResult.Failed(action.Id, failureReason, FormatFailureReason(failureReason));
        }

        if (action.Id == StrategicWorldIds.ActionAutoResolveRaid)
        {
            if (WorldBattleProgressionService.HasActiveBattleForThreat(state, request.ThreatId))
            {
                return WorldActionResult.Failed(action.Id, "world_battle_in_progress", "战斗正在世界层推演中，可选择介入，或让世界时钟继续推进。");
            }

            return _threatService.ResolveRaidAutomatically(state, request.ThreatId);
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
                return WorldActionResult.Failed(action.Id, "not_enough_resource", FormatFailureReason("not_enough_resource"));
            }

            result.Events.Add(new GameEvent
            {
                Kind = "ResourceChanged",
                Tick = state.WorldTick,
                TargetIds = { cost.ResourceId },
                Payload = { ["amount"] = (-cost.Amount).ToString(), ["reason"] = action.Id }
            });
        }

        string lastFacilityInstanceId = "";
        foreach (WorldEffectDefinition effect in action.Effects)
        {
            ApplyEffect(state, definition, queries, request, effect, result, ref lastFacilityInstanceId, returnScenePath, siteScenePath);
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

        if (action.Scope != WorldActionScope.Threat &&
            HasBlockingAttackingThreat(state, definition))
        {
            failureReason = "attacking_threat_pending";
            return false;
        }

        if (action.Scope is not (WorldActionScope.Run or WorldActionScope.Threat) &&
            IsTargetSiteInWartime(state, request))
        {
            failureReason = "site_in_wartime";
            return false;
        }

        foreach (ResourceAmountDefinition cost in action.Costs)
        {
            if (!state.PlayerResources.CanSpend(cost.ResourceId, cost.Amount))
            {
                failureReason = cost.ResourceId == StrategicWorldIds.ResourcePopulation
                    ? "not_enough_population"
                    : "not_enough_resource";
                return false;
            }
        }

        return _conditionEvaluator.AreConditionsMet(state, queries, request, action.Conditions, out failureReason);
    }

    private static bool ShouldShowAction(StrategicWorldState state, WorldActionDefinition action, string selectedSiteId, string selectedThreatId)
    {
        if (action.Scope == WorldActionScope.Run)
        {
            return true;
        }

        if (action.Scope == WorldActionScope.Threat)
        {
            if (action.Id == StrategicWorldIds.ActionAutoResolveRaid &&
                WorldBattleProgressionService.HasActiveBattleForThreat(state, selectedThreatId))
            {
                return false;
            }

            return !string.IsNullOrWhiteSpace(selectedThreatId) &&
                   state.ThreatPlans.TryGetValue(selectedThreatId, out EnemyThreatPlan threat) &&
                   threat.Stage == ThreatStage.Attacking;
        }

        if (string.IsNullOrWhiteSpace(selectedSiteId))
        {
            return false;
        }

        return action.Id switch
        {
            StrategicWorldIds.ActionBuildMine => selectedSiteId == StrategicWorldIds.SiteBonefield,
            StrategicWorldIds.ActionBuildDefenseTower => selectedSiteId == StrategicWorldIds.SiteBonefield,
            StrategicWorldIds.ActionTrainMilitia => selectedSiteId == StrategicWorldIds.SitePlayerCamp,
            _ => true
        };
    }

    private static WorldActionRequest BuildRequestForViewModel(WorldActionDefinition action, string selectedSiteId, string selectedThreatId)
    {
        string targetSiteId = action.Id switch
        {
            StrategicWorldIds.ActionTrainMilitia => StrategicWorldIds.SitePlayerCamp,
            StrategicWorldIds.ActionBuildMine => StrategicWorldIds.SiteBonefield,
            StrategicWorldIds.ActionBuildDefenseTower => StrategicWorldIds.SiteBonefield,
            _ => selectedSiteId
        };

        return new WorldActionRequest
        {
            ActionId = action.Id,
            ActorFactionId = StrategicWorldIds.FactionPlayer,
            SourceSiteId = selectedSiteId,
            TargetSiteId = targetSiteId,
            ThreatId = selectedThreatId
        };
    }

    private void ApplyEffect(
        StrategicWorldState state,
        StrategicWorldDefinition definition,
        StrategicWorldDefinitionQueries queries,
        WorldActionRequest request,
        WorldEffectDefinition effect,
        WorldActionResult result,
        ref string lastFacilityInstanceId,
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
            case WorldEffectKind.ReserveResource:
                string sourceId = effect.TargetId == "last_facility" ? lastFacilityInstanceId : effect.TargetId;
                if (state.PlayerResources.Reserve(effect.ResourceId, effect.Amount, sourceId, "Facility") &&
                    site != null &&
                    !string.IsNullOrWhiteSpace(lastFacilityInstanceId))
                {
                    FacilityInstance facility = site.Facilities.FirstOrDefault(item => item.InstanceId == sourceId);
                    if (facility != null && effect.ResourceId == StrategicWorldIds.ResourcePopulation)
                    {
                        facility.AssignedPopulation += effect.Amount;
                    }
                }
                AddEvent(result, state, "ResourceReserved", effect.ResourceId, ("amount", effect.Amount.ToString()), ("source", sourceId));
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
            case WorldEffectKind.AddFacility:
                if (site != null)
                {
                    FacilityInstance facility = AddFacility(site, queries.GetSite(site.SiteId), effect.FacilityId, request.TargetSlotId);
                    lastFacilityInstanceId = facility?.InstanceId ?? "";
                    if (facility != null)
                    {
                        AddEvent(result, state, "FacilityBuilt", facility.InstanceId, ("facility", facility.FacilityId), ("site", facility.SiteId));
                    }
                }
                break;
            case WorldEffectKind.SetFacilityState:
                if (site != null)
                {
                    FacilityInstance facility = ResolveFacility(site, effect);
                    if (facility != null)
                    {
                        facility.State = effect.FacilityState;
                        AddEvent(result, state, "FacilityStateChanged", facility.InstanceId, ("state", effect.FacilityState.ToString()));
                    }
                }
                break;
            case WorldEffectKind.AddGarrison:
                if (site != null)
                {
                    AddGarrison(site, effect.UnitTypeId, effect.Amount);
                    AddEvent(result, state, "GarrisonChanged", site.SiteId, ("unit", effect.UnitTypeId), ("amount", effect.Amount.ToString()));
                }
                break;
            case WorldEffectKind.RemoveGarrison:
                if (site != null)
                {
                    RemoveGarrison(site, effect.UnitTypeId, effect.Amount);
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

        if (battleKind == BattleKind.DefenseRaid)
        {
            WorldBattleState battle = WorldBattleProgressionService.FindActiveBattleForThreat(state, request.ThreatId);
            if (battle != null)
            {
                return _battleRequestBuilder.BuildWorldBattleInterventionRequest(
                    state,
                    definition,
                    battle.BattleId,
                    returnScenePath,
                    siteScenePath);
            }

            return _battleRequestBuilder.BuildDefenseRaidRequest(state, definition, request.ThreatId, returnScenePath, siteScenePath);
        }

        return null;
    }

    private static FacilityInstance AddFacility(WorldSiteState siteState, WorldSiteDefinition siteDefinition, string facilityId, string requestedSlotId)
    {
        FacilitySlotDefinition slot = siteDefinition.FacilitySlots.FirstOrDefault(item =>
            (string.IsNullOrWhiteSpace(requestedSlotId) || item.SlotId == requestedSlotId) &&
            item.AllowedFacilityIds.Contains(facilityId) &&
            siteState.Facilities.All(facility => facility.SlotId != item.SlotId || facility.State == FacilityState.Destroyed));

        if (slot == null)
        {
            return null;
        }

        FacilityInstance facility = new()
        {
            InstanceId = StrategicWorldService.BuildFacilityInstanceId(siteState.SiteId, slot.SlotId, facilityId),
            FacilityId = facilityId,
            SiteId = siteState.SiteId,
            SlotId = slot.SlotId,
            Level = 1,
            State = FacilityState.Active
        };
        siteState.Facilities.Add(facility);
        return facility;
    }

    private static FacilityInstance ResolveFacility(WorldSiteState site, WorldEffectDefinition effect)
    {
        if (!string.IsNullOrWhiteSpace(effect.FacilityInstanceId))
        {
            return site.Facilities.FirstOrDefault(item => item.InstanceId == effect.FacilityInstanceId);
        }

        return site.Facilities.FirstOrDefault(item => item.FacilityId == effect.FacilityId);
    }

    private static void AddGarrison(WorldSiteState site, string unitTypeId, int count)
    {
        if (count <= 0)
        {
            return;
        }

        GarrisonState garrison = site.Garrison.FirstOrDefault(item => item.UnitTypeId == unitTypeId);
        if (garrison == null)
        {
            site.Garrison.Add(new GarrisonState { UnitTypeId = unitTypeId, Count = count });
            return;
        }

        garrison.Count += count;
    }

    private static void RemoveGarrison(WorldSiteState site, string unitTypeId, int count)
    {
        int remaining = count;
        foreach (GarrisonState garrison in site.Garrison.Where(item => item.UnitTypeId == unitTypeId).ToArray())
        {
            int removed = Math.Min(remaining, garrison.Count);
            garrison.Count -= removed;
            remaining -= removed;
            if (garrison.Count <= 0)
            {
                site.Garrison.Remove(garrison);
            }

            if (remaining <= 0)
            {
                return;
            }
        }
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

    private static List<string> BuildEffectLines(WorldActionDefinition action)
    {
        return action.Id switch
        {
            StrategicWorldIds.ActionBuildMine => new List<string> { "埋骨地获得矿场", "占用人口 1", "每世界步石材 +2" },
            StrategicWorldIds.ActionBuildDefenseTower => new List<string> { "埋骨地防守 +3", "防守战获得塔支援 1 次" },
            StrategicWorldIds.ActionTrainMilitia => new List<string> { "玩家营地民兵 +1" },
            StrategicWorldIds.ActionDefendRaid => new List<string> { "进入防守战", "胜利后清除 Raid" },
            StrategicWorldIds.ActionAutoResolveRaid => new List<string> { "按驻军和防御塔计算防守结果" },
            StrategicWorldIds.ActionWaitTick => new List<string> { "推进世界步", "结算生产和威胁" },
            _ => action.Effects.Select(effect => effect.Kind.ToString()).ToList()
        };
    }

    private static List<string> BuildWarningLines(StrategicWorldState state, WorldActionDefinition action, string selectedThreatId)
    {
        if (action.Id != StrategicWorldIds.ActionAutoResolveRaid ||
            string.IsNullOrWhiteSpace(selectedThreatId) ||
            !state.ThreatPlans.TryGetValue(selectedThreatId, out EnemyThreatPlan threat) ||
            !state.SiteStates.TryGetValue(threat.TargetSiteId, out WorldSiteState site))
        {
            return new List<string>();
        }

        int militia = site.Garrison.Where(item => item.UnitTypeId == StrategicWorldIds.UnitMilitia).Sum(item => item.Count);
        int towers = site.Facilities.Count(item => item.FacilityId == StrategicWorldIds.FacilityDefenseTower && item.State == FacilityState.Active);
        return new List<string> { $"当前防守：民兵 {militia}，防御塔 {towers}" };
    }

    private static bool HasBlockingAttackingThreat(StrategicWorldState state, StrategicWorldDefinition definition)
    {
        return state?.ThreatPlans.Values.Any(threat =>
            threat.Stage == ThreatStage.Attacking &&
            WorldBattleProgressionService.IsPlayerInvolvedThreat(state, definition, threat) &&
            !WorldBattleProgressionService.HasActiveBattleForThreat(state, threat.Id)) == true;
    }

    private static string GetResourceDisplayName(StrategicWorldDefinitionQueries queries, string resourceId)
    {
        return queries.GetResource(resourceId)?.DisplayName ?? resourceId;
    }

    public static string FormatFailureReason(string reason)
    {
        return reason switch
        {
            "not_enough_resource" => "资源不足",
            "not_enough_population" => "人口不足",
            "site_not_owned" => "场域不属于玩家",
            "missing_facility" => "缺少可用建筑",
            "no_valid_facility_slot" => "没有合法建筑槽位",
            "threat_not_attackable" => "敌方威胁尚未到达",
            "not_enough_garrison" => "驻军不足",
            "no_expedition_units" => "没有可出征英雄或小兵",
            "missing_source_site" => "找不到出发场域",
            "missing_target_site" => "找不到目标场域",
            "source_site_not_owned" => "只能从己方场域出征",
            "target_site_not_owned" => "目标场域不属于玩家",
            "same_site_target" => "目标不能是出发场域",
            "unsupported_expedition_intent" => "不支持的出征意图",
            "garrison_zone_full" => "驻军区已满，无法进驻",
            "garrison_zone_missing" => "未配置默认驻军区",
            "site_not_attackable" => "当前场域不可攻打",
            "attacking_threat_pending" => "敌方正在进攻，必须先处理威胁",
            "site_in_wartime" => "场域正在战时，不能执行经营行动",
            "army_already_en_route" => "已有玩家部队正在执行该目标",
            _ => string.IsNullOrWhiteSpace(reason) ? "无法执行" : reason
        };
    }
}
