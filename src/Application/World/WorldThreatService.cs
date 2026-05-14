using System;
using System.Linq;
using Rpg.Definitions.World;
using Rpg.Domain.World;
using Rpg.Infrastructure.Logging;

namespace Rpg.Application.World;

public sealed class WorldThreatService
{
    private readonly WorldSiteModeTransitionService _siteModeTransitions = new();
    private readonly WorldGarrisonMutationService _garrisonMutations = new();
    private readonly Func<string, string> _unitDisplayNameResolver;

    public WorldThreatService(Func<string, string> unitDisplayNameResolver = null)
    {
        _unitDisplayNameResolver = unitDisplayNameResolver;
    }

    public WorldActionResult ResolveRaidAutomatically(StrategicWorldState state, string threatId)
    {
        return ResolveRaidAutomatically(state, null, threatId);
    }

    public WorldActionResult ResolveRaidAutomatically(StrategicWorldState state, StrategicWorldDefinition definition, string threatId)
    {
        if (state == null || string.IsNullOrWhiteSpace(threatId) || !state.ThreatPlans.TryGetValue(threatId, out EnemyThreatPlan threat))
        {
            return WorldActionResult.Failed(StrategicWorldIds.ActionAutoResolveRaid, "threat_missing", "找不到需要结算的威胁。");
        }

        if (threat.Stage != ThreatStage.Attacking)
        {
            return WorldActionResult.Failed(StrategicWorldIds.ActionAutoResolveRaid, "threat_not_attackable", "敌方威胁尚未到达。");
        }

        if (!state.SiteStates.TryGetValue(threat.TargetSiteId, out WorldSiteState site))
        {
            return WorldActionResult.Failed(StrategicWorldIds.ActionAutoResolveRaid, "site_missing", "找不到被攻击的场域。");
        }

        StrategicWorldDefinitionQueries queries = definition == null ? null : new StrategicWorldDefinitionQueries(definition);
        string targetSite = StrategicWorldDisplayNames.GetSiteLabel(queries, site.SiteId, GetLegacySiteFallback(site.SiteId));
        string attackerFaction = StrategicWorldDisplayNames.GetFactionLabel(
            queries,
            ResolveThreatFactionId(state, threat),
            "亡灵");
        string militiaLabel = ResolveUnitLabel(StrategicWorldIds.UnitMilitia);
        string mineLabel = StrategicWorldDisplayNames.GetFacilityLabel(queries, StrategicWorldIds.FacilityMine, "矿场");

        int militiaCount = site.Garrison
            .Where(garrison => garrison.UnitTypeId == StrategicWorldIds.UnitMilitia)
            .Sum(garrison => garrison.Count);
        int activeDefenseTowerCount = site.Facilities.Count(facility =>
            facility.FacilityId == StrategicWorldIds.FacilityDefenseTower &&
            facility.State == FacilityState.Active);

        int siteControlBonus = site.ControlState == SiteControlState.PlayerHeld ? 1 : 0;
        int damagePenalty = site.DamageLevel;
        int defenseScore = militiaCount * 2 + activeDefenseTowerCount * 3 + siteControlBonus - damagePenalty;
        const int attackScore = 5;

        string message;
        if (defenseScore >= attackScore + 2)
        {
            threat.Stage = ThreatStage.Resolved;
            ResolveThreatArmy(state, threat);
            message = $"{targetSite}完全防住了{attackerFaction}袭击。";
        }
        else if (defenseScore >= attackScore)
        {
            threat.Stage = ThreatStage.Resolved;
            ResolveThreatArmy(state, threat);
            _garrisonMutations.Remove(site, StrategicWorldIds.UnitMilitia, 1);
            message = $"{targetSite}勉强防住袭击，但损失了 1 队{militiaLabel}。";
        }
        else if (defenseScore <= attackScore - 3)
        {
            threat.Stage = ThreatStage.Resolved;
            site.ControlState = SiteControlState.Lost;
            site.OwnerFactionId = StrategicWorldIds.FactionUndead;
            site.Garrison.Clear();
            TransferThreatArmyToCapturedSite(state, threat, site);
            state.PlayerResources.ReleaseReservationsBySite(site.SiteId);
            foreach (FacilityInstance facility in site.Facilities)
            {
                facility.AssignedPopulation = 0;
                if (facility.State == FacilityState.Active)
                {
                    facility.State = FacilityState.Damaged;
                }
            }
            message = $"{targetSite}被{attackerFaction}夺回，驻军全灭，敌军残部进驻城中。";
        }
        else
        {
            threat.Stage = ThreatStage.Resolved;
            ResolveThreatArmy(state, threat);
            site.ControlState = SiteControlState.Damaged;
            site.DamageLevel = System.Math.Min(2, site.DamageLevel + 1);
            foreach (FacilityInstance mine in site.Facilities.Where(facility => facility.FacilityId == StrategicWorldIds.FacilityMine))
            {
                mine.State = FacilityState.Damaged;
            }
            message = $"{targetSite}受损，{mineLabel}停止产出。";
        }

        site.PendingThreatIds.Remove(threat.Id);

        GameLog.Info(nameof(WorldThreatService), $"RaidAutoResolved threat={threat.Id} defense={defenseScore} attack={attackScore} state={site.ControlState}");
        WorldActionResult result = new()
        {
            Success = true,
            ActionId = StrategicWorldIds.ActionAutoResolveRaid,
            Message = message,
            Events =
            {
                new GameEvent
                {
                    Kind = "ThreatStageChanged",
                    Tick = state.WorldTick,
                    TargetIds = { threat.Id },
                    Payload = { ["stage"] = nameof(ThreatStage.Resolved), ["defenseScore"] = defenseScore.ToString() }
                }
            }
        };
        WorldSiteModeTransitionService.AddEvent(result, _siteModeTransitions.EnterAftermath(site, state.WorldTick, "raid_auto_resolved", threat.Id));
        return result;
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

    private static string GetLegacySiteFallback(string siteId)
    {
        return siteId == StrategicWorldIds.SiteBonefield ? "埋骨地" : siteId;
    }

    private static void ResolveThreatArmy(StrategicWorldState state, EnemyThreatPlan threat)
    {
        if (state == null || threat == null || string.IsNullOrWhiteSpace(threat.WorldArmyId))
        {
            return;
        }

        if (state.ArmyStates.TryGetValue(threat.WorldArmyId, out WorldArmyState army))
        {
            WorldDefenseRaidResolutionHelper.ResolveThreatArmy(army);
        }
    }

    private static void TransferThreatArmyToCapturedSite(
        StrategicWorldState state,
        EnemyThreatPlan threat,
        WorldSiteState site)
    {
        if (state == null || threat == null || site == null || string.IsNullOrWhiteSpace(threat.WorldArmyId))
        {
            return;
        }

        if (state.ArmyStates.TryGetValue(threat.WorldArmyId, out WorldArmyState army))
        {
            WorldDefenseRaidResolutionHelper.TransferThreatArmyToCapturedSite(army, site);
        }
    }

}
