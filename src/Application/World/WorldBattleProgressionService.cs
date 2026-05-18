using System;
using System.Collections.Generic;
using System.Linq;
using Rpg.Application.Battle;
using Rpg.Definitions.World;
using Rpg.Domain.World;
using Rpg.Infrastructure.Logging;

namespace Rpg.Application.World;

public sealed class WorldBattleProgressionService
{
    private readonly WorldSiteDeploymentService _deploymentService = new();
    private readonly WorldSiteModeTransitionService _siteModeTransitions = new();

    public void EnsureBattlesForAttackingThreats(
        StrategicWorldState state,
        StrategicWorldDefinition definition,
        WorldTickResult result)
    {
        if (state?.ThreatPlans == null || definition == null)
        {
            return;
        }

        EnsureBattleStateCollection(state);
        foreach (EnemyThreatPlan threat in state.ThreatPlans.Values
                     .Where(threat => threat.Stage == ThreatStage.Attacking)
                     .OrderBy(threat => threat.CreatedTick))
        {
            if (IsPlayerInvolvedThreat(state, definition, threat))
            {
                continue;
            }

            if (FindActiveBattleForThreat(state, threat.Id) != null)
            {
                continue;
            }

            string battleId = BuildBattleId(threat.Id);
            if (state.WorldBattleStates.TryGetValue(battleId, out WorldBattleState existing) &&
                existing.IsResolved)
            {
                continue;
            }

            WorldBattleState battle = CreateBattleForThreat(state, definition, threat, battleId);
            if (battle == null)
            {
                continue;
            }

            state.WorldBattleStates[battle.BattleId] = battle;
            result?.StartedWorldBattleIds.Add(battle.BattleId);
            result?.Messages.Add(BuildBattleStartedMessage(state, definition, battle));
            AddEvent(result, "WorldBattleStarted", state.WorldTick, battle.BattleId,
                ("threat", battle.ThreatId),
                ("phase", battle.CurrentPhase.ToString()),
                ("projectedOutcome", battle.ProjectedOutcome.ToString()),
                ("attackerPower", battle.AttackerPower.ToString()),
                ("defenderPower", battle.DefenderPower.ToString()));

            if (state.SiteStates.TryGetValue(battle.TargetSiteId, out WorldSiteState site))
            {
                WorldSiteModeTransitionService.AddEvent(
                    result,
                    _siteModeTransitions.EnterWartime(site, state.WorldTick, "world_battle_started", battle.BattleId));
            }

            if (!string.IsNullOrWhiteSpace(threat.WorldArmyId) &&
                state.ArmyStates.TryGetValue(threat.WorldArmyId, out WorldArmyState army))
            {
                army.Status = WorldArmyStatus.Attacking;
                army.ClearNavigationPath();
                army.ClearArrivalApproachOffset();
            }

            GameLog.Info(
                nameof(WorldBattleProgressionService),
                $"WorldBattleStarted battle={battle.BattleId} threat={battle.ThreatId} attacker={battle.AttackerFactionId} defender={battle.DefenderFactionId} phase={battle.CurrentPhase} outcome={battle.ProjectedOutcome} attackPower={battle.AttackerPower} defensePower={battle.DefenderPower} duration={battle.TotalDurationTicks}");
        }
    }

    public void AdvanceWorldBattles(
        StrategicWorldState state,
        StrategicWorldDefinition definition,
        WorldTickResult result)
    {
        if (state?.WorldBattleStates == null || definition == null)
        {
            return;
        }

        foreach (WorldBattleState battle in state.WorldBattleStates.Values
                     .Where(battle => !battle.IsResolved)
                     .OrderBy(battle => battle.StartedTick)
                     .ThenBy(battle => battle.BattleId)
                     .ToArray())
        {
            if (battle.LastAdvancedTick >= state.WorldTick)
            {
                continue;
            }

            WorldBattlePhase previousPhase = battle.CurrentPhase;
            int elapsedTicks = Math.Max(0, state.WorldTick - battle.StartedTick);
            battle.CurrentPhase = ResolvePhase(elapsedTicks, battle.TotalDurationTicks);
            battle.LastAdvancedTick = state.WorldTick;

            if (battle.CurrentPhase != previousPhase)
            {
                result?.PhaseChangedWorldBattleIds.Add(battle.BattleId);
                result?.Messages.Add(BuildPhaseChangedMessage(state, definition, battle));
                AddEvent(result, "WorldBattlePhaseChanged", state.WorldTick, battle.BattleId,
                    ("phase", battle.CurrentPhase.ToString()),
                    ("remainingTicks", GetRemainingTicks(state, battle).ToString()));
                GameLog.Info(
                    nameof(WorldBattleProgressionService),
                    $"WorldBattlePhaseChanged battle={battle.BattleId} phase={battle.CurrentPhase} remaining={GetRemainingTicks(state, battle)}");
            }

            if (elapsedTicks >= battle.TotalDurationTicks)
            {
                ResolveWorldBattle(state, definition, battle, battle.ProjectedOutcome, false, "projected_world_ticks", result);
            }
        }
    }

    public WorldActionResult ApplyPlayerInterventionResult(
        StrategicWorldState state,
        StrategicWorldDefinition definition,
        BattleStartRequest request,
        BattleResult battleResult)
    {
        if (state == null || definition == null || request == null || battleResult == null)
        {
            return WorldActionResult.Failed("battle_result", "missing_world_battle_result", "缺少世界战斗回写上下文。");
        }

        if (string.IsNullOrWhiteSpace(request.WorldBattleId) ||
            state.WorldBattleStates == null ||
            !state.WorldBattleStates.TryGetValue(request.WorldBattleId, out WorldBattleState battle))
        {
            return WorldActionResult.Failed("battle_result", "world_battle_missing", "找不到正在介入的世界战斗。");
        }

        if (battle.IsResolved)
        {
            return WorldActionResult.Failed("battle_result", "world_battle_resolved", "该世界战斗已经结束。");
        }

        bool defenderWon = battleResult.Outcome == BattleOutcome.Victory &&
                           ObjectiveSucceeded(battleResult, "defend_bonefield");
        WorldBattleOutcome resolvedOutcome = defenderWon
            ? WorldBattleOutcome.DefenderHeld
            : battle.ProjectedOutcome == WorldBattleOutcome.AttackerCapturedSite
                ? WorldBattleOutcome.AttackerCapturedSite
                : WorldBattleOutcome.AttackerDamagedSite;

        WorldTickResult tickResult = new() { WorldTick = state.WorldTick };
        battle.PlayerIntervened = true;
        ResolveWorldBattle(state, definition, battle, resolvedOutcome, true, "player_intervention", tickResult);

        return new WorldActionResult
        {
            Success = true,
            ActionId = "battle_result",
            Message = tickResult.Messages.Count == 0
                ? "介入战斗已回写到大世界。"
                : string.Join("\n", tickResult.Messages),
            Events = tickResult.Events
        };
    }

    public static WorldBattleState FindActiveBattleForThreat(StrategicWorldState state, string threatId)
    {
        if (state?.WorldBattleStates == null || string.IsNullOrWhiteSpace(threatId))
        {
            return null;
        }

        return state.WorldBattleStates.Values.FirstOrDefault(battle =>
            !battle.IsResolved &&
            string.Equals(battle.ThreatId, threatId, StringComparison.Ordinal));
    }

    public static int GetRemainingTicks(StrategicWorldState state, WorldBattleState battle)
    {
        if (state == null || battle == null)
        {
            return 0;
        }

        int elapsedTicks = Math.Max(0, state.WorldTick - battle.StartedTick);
        return Math.Max(0, battle.TotalDurationTicks - elapsedTicks);
    }

    public static string GetPhaseLabel(WorldBattlePhase phase)
    {
        return phase switch
        {
            WorldBattlePhase.Opening => "接战",
            WorldBattlePhase.Skirmish => "试探",
            WorldBattlePhase.Engagement => "胶着",
            WorldBattlePhase.Decisive => "决胜",
            WorldBattlePhase.Resolution => "结算",
            _ => phase.ToString()
        };
    }

    public static string GetOutcomeLabel(WorldBattleOutcome outcome)
    {
        return outcome switch
        {
            WorldBattleOutcome.DefenderHeld => "守方稳住",
            WorldBattleOutcome.DefenderHeldDamaged => "守方惨胜",
            WorldBattleOutcome.AttackerDamagedSite => "攻方造成破坏",
            WorldBattleOutcome.AttackerCapturedSite => "攻方夺取场域",
            _ => "未知"
        };
    }

    public static bool HasActiveBattleForThreat(StrategicWorldState state, string threatId)
    {
        return FindActiveBattleForThreat(state, threatId) != null;
    }

    public static bool IsPlayerInvolvedThreat(
        StrategicWorldState state,
        StrategicWorldDefinition definition,
        string threatId)
    {
        if (state?.ThreatPlans == null ||
            string.IsNullOrWhiteSpace(threatId) ||
            !state.ThreatPlans.TryGetValue(threatId, out EnemyThreatPlan threat))
        {
            return false;
        }

        return IsPlayerInvolvedThreat(state, definition, threat);
    }

    public static bool IsPlayerInvolvedThreat(
        StrategicWorldState state,
        StrategicWorldDefinition definition,
        EnemyThreatPlan threat)
    {
        if (state == null || threat == null)
        {
            return false;
        }

        string playerFactionId = !string.IsNullOrWhiteSpace(state.PlayerFactionId)
            ? state.PlayerFactionId
            : !string.IsNullOrWhiteSpace(definition?.PlayerFactionId)
                ? definition.PlayerFactionId
                : StrategicWorldIds.FactionPlayer;
        if (!string.IsNullOrWhiteSpace(threat.WorldArmyId) &&
            state.ArmyStates.TryGetValue(threat.WorldArmyId, out WorldArmyState army) &&
            army.OwnerFactionId == playerFactionId)
        {
            return true;
        }

        return IsPlayerOwnedSite(state, threat.SourceSiteId, playerFactionId) ||
               IsPlayerOwnedSite(state, threat.TargetSiteId, playerFactionId);
    }

    private WorldBattleState CreateBattleForThreat(
        StrategicWorldState state,
        StrategicWorldDefinition definition,
        EnemyThreatPlan threat,
        string battleId)
    {
        if (!state.SiteStates.TryGetValue(threat.TargetSiteId, out WorldSiteState targetSite))
        {
            GameLog.Warn(nameof(WorldBattleProgressionService), $"WorldBattleStartSkipped reason=target_site_missing threat={threat.Id} target={threat.TargetSiteId}");
            return null;
        }

        WorldArmyState attackerArmy = !string.IsNullOrWhiteSpace(threat.WorldArmyId) &&
                                      state.ArmyStates.TryGetValue(threat.WorldArmyId, out WorldArmyState army)
            ? army
            : null;
        if (attackerArmy == null && !TryCreateVirtualThreatArmy(state, definition, threat, out attackerArmy))
        {
            GameLog.Warn(nameof(WorldBattleProgressionService), $"WorldBattleStartSkipped reason=attacker_missing threat={threat.Id}");
            return null;
        }

        StrategicWorldDefinitionQueries queries = new(definition);
        string attackerFactionId = !string.IsNullOrWhiteSpace(attackerArmy.OwnerFactionId)
            ? attackerArmy.OwnerFactionId
            : StrategicWorldIds.FactionUndead;
        string defenderFactionId = !string.IsNullOrWhiteSpace(targetSite.OwnerFactionId)
            ? targetSite.OwnerFactionId
            : state.PlayerFactionId;
        List<GarrisonState> attackerForces = CloneGarrisons(attackerArmy.GarrisonUnits);
        if (attackerForces.Count == 0)
        {
            GameLog.Warn(nameof(WorldBattleProgressionService), $"WorldBattleStartSkipped reason=attacker_force_empty threat={threat.Id} army={attackerArmy.ArmyId}");
            return null;
        }

        List<GarrisonState> defenderForces = CloneGarrisons(targetSite.Garrison);
        int attackerPower = CalculateForcePower(attackerForces) +
                            GetFactionCapabilityValue(queries, attackerFactionId, "world_attack_bonus");
        int defenderPower = CalculateForcePower(defenderForces) +
                            CalculateSiteDefensePower(targetSite) +
                            GetFactionCapabilityValue(queries, defenderFactionId, "world_defense_bonus");
        int randomSwing = BuildDeterministicRandom(state, battleId).Next(-2, 3);
        int margin = attackerPower + randomSwing - defenderPower;
        WorldBattleOutcome projectedOutcome = ResolveProjectedOutcome(margin);
        string projectedWinner = projectedOutcome is WorldBattleOutcome.DefenderHeld or WorldBattleOutcome.DefenderHeldDamaged
            ? defenderFactionId
            : attackerFactionId;
        int totalDuration = Math.Clamp(
            4 + GetFactionCapabilityValue(queries, attackerFactionId, "world_battle_duration_bonus") -
            GetFactionCapabilityValue(queries, defenderFactionId, "world_battle_duration_reduction"),
            3,
            6);

        return new WorldBattleState
        {
            BattleId = battleId,
            ThreatId = threat.Id,
            SourceSiteId = threat.SourceSiteId,
            TargetSiteId = threat.TargetSiteId,
            AttackerFactionId = attackerFactionId,
            DefenderFactionId = defenderFactionId,
            AttackerArmyId = attackerArmy.ArmyId,
            StartedTick = state.WorldTick,
            LastAdvancedTick = state.WorldTick,
            TotalDurationTicks = totalDuration,
            CurrentPhase = WorldBattlePhase.Opening,
            ProjectedOutcome = projectedOutcome,
            ProjectedWinnerFactionId = projectedWinner,
            AttackerPower = attackerPower,
            DefenderPower = defenderPower,
            AttackerLossEstimate = EstimateLosses(attackerForces, projectedOutcome, attackerSide: true, queries, attackerFactionId),
            DefenderLossEstimate = EstimateLosses(defenderForces, projectedOutcome, attackerSide: false, queries, attackerFactionId),
            AttackerForces = attackerForces,
            DefenderForces = defenderForces
        };
    }

    private static bool IsPlayerOwnedSite(
        StrategicWorldState state,
        string siteId,
        string playerFactionId)
    {
        return !string.IsNullOrWhiteSpace(siteId) &&
               state.SiteStates.TryGetValue(siteId, out WorldSiteState site) &&
               (site.OwnerFactionId == playerFactionId ||
                (site.ControlState is SiteControlState.PlayerHeld or SiteControlState.Damaged &&
                 string.IsNullOrWhiteSpace(site.OwnerFactionId)));
    }

    private static bool TryCreateVirtualThreatArmy(
        StrategicWorldState state,
        StrategicWorldDefinition definition,
        EnemyThreatPlan threat,
        out WorldArmyState army)
    {
        army = null;
        ThreatRuleDefinition rule = definition.ThreatRules.FirstOrDefault(item => item.Id == threat.RuleId);
        if (rule == null)
        {
            return false;
        }

        army = new WorldArmyState
        {
            ArmyId = string.IsNullOrWhiteSpace(threat.WorldArmyId) ? $"{threat.Id}:army" : threat.WorldArmyId,
            OwnerFactionId = definition.EnemyFactionIds.FirstOrDefault() ?? StrategicWorldIds.FactionUndead,
            SourceSiteId = threat.SourceSiteId,
            TargetSiteId = threat.TargetSiteId,
            RelatedThreatId = threat.Id,
            Status = WorldArmyStatus.Attacking,
            Intent = WorldArmyIntent.Raid,
            CreatedTick = state.WorldTick
        };

        foreach (GarrisonDefinition force in rule.EnemyForces.Where(item => item.Count > 0))
        {
            army.GarrisonUnits.Add(new GarrisonState
            {
                UnitTypeId = force.UnitTypeId,
                Count = force.Count,
                Morale = force.Morale
            });
        }

        state.ArmyStates[army.ArmyId] = army;
        threat.WorldArmyId = army.ArmyId;
        return army.GarrisonUnits.Count > 0;
    }

    private void ResolveWorldBattle(
        StrategicWorldState state,
        StrategicWorldDefinition definition,
        WorldBattleState battle,
        WorldBattleOutcome outcome,
        bool playerIntervened,
        string reason,
        WorldTickResult result)
    {
        if (battle == null || battle.IsResolved)
        {
            return;
        }

        battle.IsResolved = true;
        battle.ResolvedTick = state.WorldTick;
        battle.ResolvedOutcome = outcome;
        battle.ResolutionReason = reason ?? "";
        battle.PlayerIntervened = battle.PlayerIntervened || playerIntervened;
        battle.ResolvedWinnerFactionId = outcome is WorldBattleOutcome.DefenderHeld or WorldBattleOutcome.DefenderHeldDamaged
            ? battle.DefenderFactionId
            : battle.AttackerFactionId;

        if (state.ThreatPlans.TryGetValue(battle.ThreatId, out EnemyThreatPlan threat))
        {
            threat.Stage = ThreatStage.Resolved;
            threat.CountdownTicks = 0;
        }

        WorldSiteState site = state.SiteStates.TryGetValue(battle.TargetSiteId, out WorldSiteState siteValue)
            ? siteValue
            : null;
        if (site != null)
        {
            site.PendingThreatIds.Remove(battle.ThreatId);
            ApplyOutcomeToSite(state, definition, battle, site, outcome);
            WorldSiteModeTransitionService.AddEvent(
                result,
                _siteModeTransitions.EnterAftermath(site, state.WorldTick, playerIntervened ? "world_battle_intervened" : "world_battle_resolved", battle.BattleId));
        }

        if (!string.IsNullOrWhiteSpace(battle.AttackerArmyId) &&
            state.ArmyStates.TryGetValue(battle.AttackerArmyId, out WorldArmyState attackerArmy))
        {
            attackerArmy.Status = outcome == WorldBattleOutcome.AttackerCapturedSite
                ? WorldArmyStatus.Garrisoned
                : WorldArmyStatus.Defeated;
            if (outcome == WorldBattleOutcome.AttackerCapturedSite)
            {
                attackerArmy.GarrisonUnits.Clear();
            }

            attackerArmy.ClearNavigationPath();
            attackerArmy.ClearArrivalApproachOffset();
            attackerArmy.ClearTargetApproachDirection();
        }

        result?.ResolvedWorldBattleIds.Add(battle.BattleId);
        result?.Messages.Add(BuildBattleResolvedMessage(state, definition, battle));
        AddEvent(result, "WorldBattleResolved", state.WorldTick, battle.BattleId,
            ("threat", battle.ThreatId),
            ("outcome", battle.ResolvedOutcome.ToString()),
            ("winner", battle.ResolvedWinnerFactionId),
            ("playerIntervened", battle.PlayerIntervened.ToString()));
        AddEvent(result, "ThreatStageChanged", state.WorldTick, battle.ThreatId,
            ("stage", nameof(ThreatStage.Resolved)),
            ("reason", reason ?? ""));
        GameLog.Info(
            nameof(WorldBattleProgressionService),
            $"WorldBattleResolved battle={battle.BattleId} outcome={battle.ResolvedOutcome} winner={battle.ResolvedWinnerFactionId} intervened={battle.PlayerIntervened} reason={reason}");
    }

    private void ApplyOutcomeToSite(
        StrategicWorldState state,
        StrategicWorldDefinition definition,
        WorldBattleState battle,
        WorldSiteState site,
        WorldBattleOutcome outcome)
    {
        switch (outcome)
        {
            case WorldBattleOutcome.DefenderHeld:
                if (site.OwnerFactionId == state.PlayerFactionId)
                {
                    site.ControlState = SiteControlState.PlayerHeld;
                }
                RemoveAnyGarrisonUnits(site, Math.Max(0, battle.DefenderLossEstimate / 2));
                break;
            case WorldBattleOutcome.DefenderHeldDamaged:
                if (site.OwnerFactionId == state.PlayerFactionId)
                {
                    site.ControlState = SiteControlState.Damaged;
                }
                ApplySiteDamage(site, severe: false);
                RemoveAnyGarrisonUnits(site, Math.Max(1, battle.DefenderLossEstimate));
                break;
            case WorldBattleOutcome.AttackerDamagedSite:
                site.ControlState = SiteControlState.Damaged;
                ApplySiteDamage(site, severe: false);
                RemoveAnyGarrisonUnits(site, Math.Max(1, battle.DefenderLossEstimate));
                break;
            case WorldBattleOutcome.AttackerCapturedSite:
                site.OwnerFactionId = battle.AttackerFactionId;
                site.ControlState = SiteControlState.Lost;
                site.Garrison.Clear();
                state.PlayerResources.ReleaseReservationsBySite(site.SiteId);
                ApplySiteDamage(site, severe: true);
                foreach (GarrisonState force in battle.AttackerForces.Where(item => item.Count > 0))
                {
                    site.Garrison.Add(CloneGarrison(force));
                }
                break;
        }

        WorldSiteDefinition siteDefinition = new StrategicWorldDefinitionQueries(definition).GetSite(site.SiteId);
        _deploymentService.EnsureGarrisonPlacements(site, siteDefinition);
    }

    private static void ApplySiteDamage(WorldSiteState site, bool severe)
    {
        site.DamageLevel = Math.Min(2, site.DamageLevel + (severe ? 2 : 1));
        foreach (FacilityInstance facility in site.Facilities)
        {
            if (severe || facility.FacilityId == StrategicWorldIds.FacilityMine)
            {
                facility.AssignedPopulation = severe ? 0 : facility.AssignedPopulation;
                if (facility.State == FacilityState.Active)
                {
                    facility.State = FacilityState.Damaged;
                }
            }
        }
    }

    private static void RemoveAnyGarrisonUnits(WorldSiteState site, int count)
    {
        if (site == null || count <= 0)
        {
            return;
        }

        int remaining = count;
        foreach (GarrisonState garrison in site.Garrison.Where(item => item.Count > 0).ToArray())
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

    private static WorldBattlePhase ResolvePhase(int elapsedTicks, int totalDurationTicks)
    {
        if (elapsedTicks <= 0)
        {
            return WorldBattlePhase.Opening;
        }

        if (elapsedTicks >= totalDurationTicks)
        {
            return WorldBattlePhase.Resolution;
        }

        float progress = elapsedTicks / (float)Math.Max(1, totalDurationTicks);
        if (progress < 0.34f)
        {
            return WorldBattlePhase.Skirmish;
        }

        return progress < 0.67f
            ? WorldBattlePhase.Engagement
            : WorldBattlePhase.Decisive;
    }

    private static WorldBattleOutcome ResolveProjectedOutcome(int margin)
    {
        if (margin >= 8)
        {
            return WorldBattleOutcome.AttackerCapturedSite;
        }

        if (margin >= 2)
        {
            return WorldBattleOutcome.AttackerDamagedSite;
        }

        return margin <= -4
            ? WorldBattleOutcome.DefenderHeld
            : WorldBattleOutcome.DefenderHeldDamaged;
    }

    private static int CalculateForcePower(IEnumerable<GarrisonState> forces)
    {
        return forces?
            .Where(force => force.Count > 0)
            .Sum(force => force.Count * (GetUnitPower(force.UnitTypeId) + Math.Clamp(force.Morale, 0, 100) / 25))
               ?? 0;
    }

    private static int CalculateSiteDefensePower(WorldSiteState site)
    {
        if (site == null)
        {
            return 0;
        }

        int towerPower = site.Facilities.Count(facility =>
            facility.FacilityId == StrategicWorldIds.FacilityDefenseTower &&
            facility.State == FacilityState.Active) * 3;
        int controlBonus = site.ControlState == SiteControlState.PlayerHeld ? 1 : 0;
        return Math.Max(0, towerPower + controlBonus - site.DamageLevel);
    }

    private static int GetUnitPower(string unitTypeId)
    {
        return unitTypeId switch
        {
            StrategicWorldIds.UnitPlayerKnight => 7,
            StrategicWorldIds.UnitMilitia => 3,
            StrategicWorldIds.UnitGraveShadow => 5,
            StrategicWorldIds.UnitGraveMarksman => 5,
            StrategicWorldIds.UnitDeathBlighter => 6,
            _ => 4
        };
    }

    private static int EstimateLosses(
        IReadOnlyCollection<GarrisonState> forces,
        WorldBattleOutcome outcome,
        bool attackerSide,
        StrategicWorldDefinitionQueries queries,
        string attackerFactionId)
    {
        int count = forces?.Where(force => force.Count > 0).Sum(force => force.Count) ?? 0;
        if (count <= 0)
        {
            return 0;
        }

        int attritionBonus = GetFactionCapabilityValue(queries, attackerFactionId, "world_attrition_bonus");
        int percent = outcome switch
        {
            WorldBattleOutcome.DefenderHeld => attackerSide ? 70 : 15,
            WorldBattleOutcome.DefenderHeldDamaged => attackerSide ? 55 : 35,
            WorldBattleOutcome.AttackerDamagedSite => attackerSide ? 30 : 55,
            WorldBattleOutcome.AttackerCapturedSite => attackerSide ? 20 : 85,
            _ => 30
        };
        percent += attritionBonus * (attackerSide ? 2 : 5);
        return Math.Clamp((int)Math.Ceiling(count * percent / 100.0), 0, count);
    }

    private static int GetFactionCapabilityValue(
        StrategicWorldDefinitionQueries queries,
        string factionId,
        string key)
    {
        FactionDefinition faction = queries.GetFaction(factionId);
        if (faction == null || string.IsNullOrWhiteSpace(key))
        {
            return 0;
        }

        return faction.Capabilities
            .Where(capability => capability?.Values != null && capability.Values.TryGetValue(key, out _))
            .Sum(capability => capability.Values[key]);
    }

    private static Random BuildDeterministicRandom(StrategicWorldState state, string battleId)
    {
        return new Random(HashCode.Combine(state.Seed, state.WorldTick, battleId ?? ""));
    }

    private static List<GarrisonState> CloneGarrisons(IEnumerable<GarrisonState> source)
    {
        return source?
            .Where(item => item.Count > 0 && !string.IsNullOrWhiteSpace(item.UnitTypeId))
            .Select(CloneGarrison)
            .ToList() ?? new List<GarrisonState>();
    }

    private static GarrisonState CloneGarrison(GarrisonState source)
    {
        return new GarrisonState
        {
            UnitTypeId = source.UnitTypeId,
            Count = source.Count,
            SourceFacilityId = source.SourceFacilityId,
            Morale = source.Morale,
            DamageLevel = source.DamageLevel
        };
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

    private static void EnsureBattleStateCollection(StrategicWorldState state)
    {
        state.WorldBattleStates ??= new Dictionary<string, WorldBattleState>();
    }

    private static string BuildBattleId(string threatId) => $"world_battle:{threatId}";

    private static string BuildBattleStartedMessage(
        StrategicWorldState state,
        StrategicWorldDefinition definition,
        WorldBattleState battle)
    {
        string target = ResolveSiteName(definition, battle.TargetSiteId);
        return $"{target} 爆发世界战斗：{GetOutcomeLabel(battle.ProjectedOutcome)}，预计 {battle.TotalDurationTicks} 个世界步后结算，可在过程中介入。";
    }

    private static string BuildPhaseChangedMessage(
        StrategicWorldState state,
        StrategicWorldDefinition definition,
        WorldBattleState battle)
    {
        string target = ResolveSiteName(definition, battle.TargetSiteId);
        return $"{target} 战事进入{GetPhaseLabel(battle.CurrentPhase)}阶段，剩余 {GetRemainingTicks(state, battle)} 个世界步。";
    }

    private static string BuildBattleResolvedMessage(
        StrategicWorldState state,
        StrategicWorldDefinition definition,
        WorldBattleState battle)
    {
        string target = ResolveSiteName(definition, battle.TargetSiteId);
        string source = battle.PlayerIntervened ? "玩家介入" : "自动推演";
        return $"{target} 战斗结束（{source}）：{GetOutcomeLabel(battle.ResolvedOutcome)}。";
    }

    private static string ResolveSiteName(StrategicWorldDefinition definition, string siteId)
    {
        return definition?.SiteDefinitions.FirstOrDefault(site => site.Id == siteId)?.DisplayName ?? siteId;
    }

    private static void AddEvent(
        WorldTickResult result,
        string kind,
        int tick,
        string targetId,
        params (string Key, string Value)[] payload)
    {
        if (result == null)
        {
            return;
        }

        GameEvent gameEvent = new()
        {
            Kind = kind,
            Tick = tick,
            TargetIds = { targetId }
        };

        foreach ((string key, string value) in payload)
        {
            if (!string.IsNullOrWhiteSpace(key))
            {
                gameEvent.Payload[key] = value ?? "";
            }
        }

        result.Events.Add(gameEvent);
    }
}
