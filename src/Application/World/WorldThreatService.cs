using System.Linq;
using Rpg.Domain.World;
using Rpg.Infrastructure.Logging;

namespace Rpg.Application.World;

public sealed class WorldThreatService
{
    private readonly WorldSiteModeTransitionService _siteModeTransitions = new();
    private readonly WorldGarrisonMutationService _garrisonMutations = new();

    public WorldActionResult ResolveRaidAutomatically(StrategicWorldState state, string threatId)
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
            message = "埋骨地完全防住了亡灵袭击。";
        }
        else if (defenseScore >= attackScore)
        {
            threat.Stage = ThreatStage.Resolved;
            ResolveThreatArmy(state, threat);
            _garrisonMutations.Remove(site, StrategicWorldIds.UnitMilitia, 1);
            message = "埋骨地勉强防住袭击，但损失了 1 队民兵。";
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
            message = "埋骨地被亡灵夺回，驻军全灭，敌军残部进驻城中。";
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
            message = "埋骨地受损，矿场停止产出。";
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
