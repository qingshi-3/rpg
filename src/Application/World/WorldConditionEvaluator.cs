using System.Linq;
using Rpg.Definitions.World;
using Rpg.Domain.World;

namespace Rpg.Application.World;

public sealed class WorldConditionEvaluator
{
    private readonly WorldSiteDeploymentService _deploymentService = new();

    public bool AreConditionsMet(
        StrategicWorldState state,
        StrategicWorldDefinitionQueries definitions,
        WorldActionRequest request,
        System.Collections.Generic.IEnumerable<WorldConditionDefinition> conditions,
        out string failureReason)
    {
        failureReason = "";

        if (conditions == null)
        {
            return true;
        }

        foreach (WorldConditionDefinition condition in conditions)
        {
            if (!IsConditionMet(state, definitions, request, condition, out failureReason))
            {
                return false;
            }
        }

        return true;
    }

    public bool IsConditionMet(
        StrategicWorldState state,
        StrategicWorldDefinitionQueries definitions,
        WorldActionRequest request,
        WorldConditionDefinition condition,
        out string failureReason)
    {
        failureReason = condition?.FailureReasonKey ?? "condition_failed";
        if (state == null || definitions == null)
        {
            failureReason = "missing_world_state";
            return false;
        }

        if (condition == null || condition.Kind == WorldConditionKind.Always)
        {
            return true;
        }

        string siteId = ResolveSiteId(condition.SiteId, request);
        WorldSiteState site = ResolveSite(state, siteId);

        bool result = condition.Kind switch
        {
            WorldConditionKind.SiteControlStateIs => site != null && IsControlStateAllowed(site.ControlState, condition),
            WorldConditionKind.SiteOwnerIs => site != null && site.OwnerFactionId == ResolveFactionId(condition.FactionId, request),
            WorldConditionKind.HasResourceAtLeast => state.PlayerResources.GetAvailable(condition.ResourceId) >= condition.Amount,
            WorldConditionKind.HasAvailablePopulation => state.PlayerResources.GetAvailable(StrategicWorldIds.ResourcePopulation) >= condition.Amount,
            WorldConditionKind.HasFacility => site != null && site.Facilities.Any(facility =>
                facility.FacilityId == condition.TargetId &&
                facility.State == condition.FacilityState),
            WorldConditionKind.HasEmptyFacilitySlot => HasEmptyFacilitySlot(site, definitions.GetSite(siteId), condition.TargetId, condition.SlotTag),
            WorldConditionKind.HasGarrisonAtLeast => site != null && site.Garrison.Any(garrison =>
                garrison.UnitTypeId == condition.UnitTypeId &&
                garrison.Count >= condition.Amount),
            WorldConditionKind.ThreatStageIs => ResolveThreat(state, condition.ThreatId, request)?.Stage == condition.ThreatStage,
            WorldConditionKind.NoActiveThreatOfRule => !state.ThreatPlans.Values.Any(threat =>
                threat.RuleId == condition.RuleId &&
                threat.Stage != ThreatStage.Resolved),
            WorldConditionKind.HasGarrisonCapacity => site != null &&
                _deploymentService.CanAcceptGarrison(site, definitions.GetSite(siteId), condition.Amount, out _),
            _ => false
        };

        if (result)
        {
            failureReason = "";
        }

        return result;
    }

    public static string ResolveSiteId(string configuredSiteId, WorldActionRequest request)
    {
        if (!string.IsNullOrWhiteSpace(configuredSiteId))
        {
            return configuredSiteId;
        }

        if (!string.IsNullOrWhiteSpace(request?.TargetSiteId))
        {
            return request.TargetSiteId;
        }

        return request?.SourceSiteId ?? "";
    }

    public static string ResolveFactionId(string configuredFactionId, WorldActionRequest request)
    {
        return string.IsNullOrWhiteSpace(configuredFactionId)
            ? request?.ActorFactionId ?? StrategicWorldIds.FactionPlayer
            : configuredFactionId;
    }

    private static WorldSiteState ResolveSite(StrategicWorldState state, string siteId)
    {
        return !string.IsNullOrWhiteSpace(siteId) && state.SiteStates.TryGetValue(siteId, out WorldSiteState site)
            ? site
            : null;
    }

    private static EnemyThreatPlan ResolveThreat(StrategicWorldState state, string threatId, WorldActionRequest request)
    {
        string resolvedThreatId = string.IsNullOrWhiteSpace(threatId) ? request?.ThreatId : threatId;
        return !string.IsNullOrWhiteSpace(resolvedThreatId) && state.ThreatPlans.TryGetValue(resolvedThreatId, out EnemyThreatPlan threat)
            ? threat
            : null;
    }

    private static bool IsControlStateAllowed(SiteControlState actual, WorldConditionDefinition condition)
    {
        return condition.ControlStates.Count > 0
            ? condition.ControlStates.Contains(actual)
            : actual == condition.ControlState;
    }

    private static bool HasEmptyFacilitySlot(
        WorldSiteState siteState,
        WorldSiteDefinition siteDefinition,
        string facilityId,
        string slotTag)
    {
        if (siteState == null || siteDefinition == null)
        {
            return false;
        }

        return siteDefinition.FacilitySlots.Any(slot =>
            (string.IsNullOrWhiteSpace(facilityId) || slot.AllowedFacilityIds.Contains(facilityId)) &&
            (string.IsNullOrWhiteSpace(slotTag) || slot.Tags.Contains(slotTag)) &&
            siteState.Facilities.All(facility => facility.SlotId != slot.SlotId || facility.State == FacilityState.Destroyed));
    }
}
