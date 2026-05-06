using System.Collections.Generic;
using System.Linq;
using Godot;
using Rpg.Definitions.World;
using Rpg.Domain.World;
using Rpg.Infrastructure.Logging;

namespace Rpg.Application.World;

public sealed class WorldExpeditionService
{
    private readonly WorldSiteDeploymentService _deploymentService = new();

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
            RemoveGarrison(sourceSite, unitTypeId, count);
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
        GameLog.Info(nameof(WorldExpeditionService), $"WorldExpeditionCreated army={army.ArmyId} source={sourceSiteId} target={army.TargetSiteId} intent={intent} units={unitCount}");
        return true;
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

    private static void RemoveGarrison(WorldSiteState site, string unitTypeId, int count)
    {
        int remaining = count;
        foreach (GarrisonState garrison in site.Garrison.Where(item => item.UnitTypeId == unitTypeId).ToArray())
        {
            int removed = System.Math.Min(remaining, garrison.Count);
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
