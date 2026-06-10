using System.Collections.Generic;
using System.Linq;
using Godot;
using Rpg.Definitions.World;
using Rpg.Domain.World;
using Rpg.Infrastructure.Logging;

namespace Rpg.Application.World;

public sealed class WorldExpeditionService
{
    // Faction command capacity is not modeled yet. Keep the first-slice queue cap
    // in Application so Presentation and expedition mutation share one authority.
    public const int FirstSliceMaxActivePlayerExpeditions = 3;

    private readonly WorldSiteDeploymentService _deploymentService = new();
    private readonly WorldGarrisonMutationService _garrisonMutations = new();

    public bool TryCreateExpedition(
        StrategicWorldState state,
        StrategicWorldDefinition definition,
        string sourceSiteId,
        Vector2 sourcePosition,
        string targetSiteId,
        Vector2 destination,
        WorldArmyIntent intent,
        IReadOnlyDictionary<string, int> units,
        out WorldArmyState army,
        out string failureReason)
    {
        army = null;
        failureReason = "";
        if (state == null || definition == null)
        {
            failureReason = "missing_world_state";
            return false;
        }

        StrategicWorldDefinitionQueries queries = new(definition);
        if (string.IsNullOrWhiteSpace(sourceSiteId) ||
            !state.SiteStates.TryGetValue(sourceSiteId, out WorldSiteState sourceSite) ||
            queries.GetSite(sourceSiteId) is not { } sourceDefinition)
        {
            failureReason = "missing_source_site";
            return false;
        }

        if (sourceSite.OwnerFactionId != state.PlayerFactionId ||
            sourceSite.ControlState is not (SiteControlState.PlayerHeld or SiteControlState.Damaged))
        {
            failureReason = "source_site_not_owned";
            return false;
        }

        Dictionary<string, int> requestedUnits = NormalizeUnits(units);
        if (requestedUnits.Count == 0)
        {
            failureReason = "no_expedition_units";
            return false;
        }

        if (!HasAvailablePlayerExpeditionCapacity(state, out _, out _))
        {
            failureReason = "expedition_capacity_full";
            return false;
        }

        foreach ((string unitTypeId, int count) in requestedUnits)
        {
            int available = sourceSite.Garrison
                .Where(unit => unit.UnitTypeId == unitTypeId)
                .Sum(unit => System.Math.Max(unit.Count, 0));
            if (available < count)
            {
                failureReason = "not_enough_garrison";
                return false;
            }
        }

        WorldSiteState targetSite = null;
        WorldSiteDefinition targetDefinition = null;
        if (!string.IsNullOrWhiteSpace(targetSiteId))
        {
            if (!state.SiteStates.TryGetValue(targetSiteId, out targetSite) ||
                queries.GetSite(targetSiteId) is not { } resolvedTargetDefinition)
            {
                failureReason = "missing_target_site";
                return false;
            }

            targetDefinition = resolvedTargetDefinition;
            if (targetSiteId == sourceSiteId)
            {
                failureReason = "same_site_target";
                return false;
            }
        }

        int unitCount = requestedUnits.Sum(item => item.Value);
        if (intent == WorldArmyIntent.ReinforceSite)
        {
            if (targetSite == null || targetSite.OwnerFactionId != state.PlayerFactionId)
            {
                failureReason = "target_site_not_owned";
                return false;
            }

            if (!_deploymentService.CanAcceptGarrison(targetSite, targetDefinition, unitCount, out failureReason))
            {
                return false;
            }
        }
        else if (intent == WorldArmyIntent.AssaultSite)
        {
            if (targetSite == null || targetSite.OwnerFactionId == state.PlayerFactionId)
            {
                failureReason = "site_not_attackable";
                return false;
            }
        }
        else if (intent != WorldArmyIntent.MoveToPosition)
        {
            failureReason = "unsupported_expedition_intent";
            return false;
        }

        foreach ((string unitTypeId, int count) in requestedUnits)
        {
            _garrisonMutations.Remove(sourceSite, unitTypeId, count);
        }

        army = new WorldArmyState
        {
            ArmyId = BuildWorldArmyId(state, sourceSiteId),
            OwnerFactionId = state.PlayerFactionId,
            SourceSiteId = sourceSiteId,
            TargetSiteId = targetSiteId ?? "",
            MoveSpeed = 56.0f,
            Radius = 16.0f,
            Status = WorldArmyStatus.Moving,
            Intent = intent,
            CreatedTick = state.WorldTick
        };
        army.WorldPosition = sourcePosition;
        army.Destination = destination;
        army.ClearNavigationPath();

        foreach ((string unitTypeId, int count) in requestedUnits)
        {
            army.GarrisonUnits.Add(new GarrisonState
            {
                UnitTypeId = unitTypeId,
                Count = count
            });
        }

        state.ArmyStates[army.ArmyId] = army;
        GameLog.Info(
            nameof(WorldExpeditionService),
            $"WorldExpeditionCreated army={army.ArmyId} source={sourceSiteId} target={army.TargetSiteId} intent={intent} status={army.Status} units={FormatUnits(army.GarrisonUnits)} sourceGarrisonAfter={FormatUnits(sourceSite.Garrison)}");
        return true;
    }

    public bool HasAvailablePlayerExpeditionCapacity(
        StrategicWorldState state,
        out int activeCount,
        out int maxCount)
    {
        return HasAvailablePlayerExpeditionCapacity(state, 1, out activeCount, out maxCount);
    }

    public bool HasAvailablePlayerExpeditionCapacity(
        StrategicWorldState state,
        int requestedCount,
        out int activeCount,
        out int maxCount)
    {
        maxCount = FirstSliceMaxActivePlayerExpeditions;
        activeCount = CountActivePlayerExpeditions(state);
        return activeCount + System.Math.Max(0, requestedCount) <= maxCount;
    }

    public static int CountActivePlayerExpeditions(StrategicWorldState state)
    {
        if (state?.ArmyStates == null)
        {
            return 0;
        }

        string playerFactionId = string.IsNullOrWhiteSpace(state.PlayerFactionId)
            ? StrategicWorldIds.FactionPlayer
            : state.PlayerFactionId;
        return state.ArmyStates.Values.Count(army =>
            army != null &&
            string.Equals(army.OwnerFactionId, playerFactionId, System.StringComparison.Ordinal) &&
            army.Status is not (WorldArmyStatus.Garrisoned or WorldArmyStatus.Defeated));
    }

    private static string FormatUnits(IEnumerable<GarrisonState> units)
    {
        return units == null
            ? "none"
            : string.Join(",", units.Where(unit => unit != null).Select(unit => $"{unit.UnitTypeId}:{unit.Count}"));
    }

    private static Dictionary<string, int> NormalizeUnits(IReadOnlyDictionary<string, int> units)
    {
        Dictionary<string, int> normalized = new();
        if (units == null)
        {
            return normalized;
        }

        foreach ((string unitTypeId, int count) in units)
        {
            if (string.IsNullOrWhiteSpace(unitTypeId) || count <= 0)
            {
                continue;
            }

            normalized[unitTypeId] = normalized.TryGetValue(unitTypeId, out int existing)
                ? existing + count
                : count;
        }

        return normalized;
    }

    private static string BuildWorldArmyId(StrategicWorldState state, string sourceSiteId)
    {
        string safeSourceId = string.IsNullOrWhiteSpace(sourceSiteId) ? "site" : sourceSiteId;
        string baseId = $"expedition:{safeSourceId}:{state.WorldTick}:army";
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
}
