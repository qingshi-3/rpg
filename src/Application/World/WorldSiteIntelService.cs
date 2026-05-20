using System;
using System.Collections.Generic;
using System.Linq;
using Rpg.Application.Battle;
using Rpg.Definitions.World;
using Rpg.Domain.World;
using Rpg.Infrastructure.Logging;

namespace Rpg.Application.World;

public static class WorldSiteIntelService
{
    private const string MissingIntelReason = "missing_intel_definition: 场域情报定义缺失。";
    private const string MissingSiteDisplayName = "未知场域";
    private const string MissingSiteReason = "场域定义缺失。";
    private const string UnknownSiteReason = "尚未发现该场域。";
    private const string DefaultHiddenTacticalSummary = "内侧布阵尚未确认。";

    private static readonly object MissingIntelLogSync = new();
    private static readonly HashSet<string> MissingIntelLoggedSiteIds = new(StringComparer.Ordinal);

    public static WorldSiteIntelViewModel BuildCurrentView(
        StrategicWorldState state,
        StrategicWorldDefinition definition,
        string siteId,
        WorldIntelVisibility visibility)
    {
        WorldSiteDefinition siteDefinition = definition?.SiteDefinitions
            .FirstOrDefault(site => site != null && site.Id == siteId);

        if (siteDefinition == null ||
            state == null ||
            !state.SiteStates.TryGetValue(siteId, out WorldSiteState siteState))
        {
            return BuildMissingView(siteId, visibility);
        }

        return BuildCurrentView(siteDefinition, siteState, state.WorldTick, visibility);
    }

    public static WorldSiteIntelSnapshot BuildSnapshot(
        WorldSiteDefinition definition,
        WorldSiteState state,
        int worldTick)
    {
        WorldSiteIntelViewModel view = BuildCurrentView(definition, state, worldTick, WorldIntelVisibility.Visible);

        return new WorldSiteIntelSnapshot
        {
            SiteId = view.SiteId,
            DisplayName = view.DisplayName,
            LastSeenWorldTick = worldTick,
            OwnerFactionId = state?.OwnerFactionId ?? "",
            ControlState = state?.ControlState ?? SiteControlState.Unknown,
            SiteMode = state?.SiteMode ?? WorldSiteMode.Peacetime,
            DamageLevel = state?.DamageLevel ?? 0,
            IntelPolicy = view.Policy.ToString(),
            StrategicSummary = view.StrategicSummary,
            TacticalSummary = view.TacticalSummary,
            HiddenTacticalSummary = view.HiddenTacticalSummary,
            KnownEntranceIds = view.KnownEntranceIds.ToList(),
            UnknownIntelReasons = view.UnknownIntelReasons.ToList(),
            ActiveObscurationSourceIds = view.ActiveObscurationSourceIds.ToList(),
            KnownTacticalTags = view.KnownTacticalTags.ToList(),
            KnownLocalResources = CloneResourceStore(state?.LocalResources),
            KnownFacilities = state?.Facilities.Select(CloneFacility).ToList() ?? new List<FacilityInstance>(),
            KnownGarrison = state?.Garrison.Select(CloneGarrison).ToList() ?? new List<GarrisonState>(),
            KnownPendingThreatIds = state?.PendingThreatIds.ToList() ?? new List<string>()
        };
    }

    public static WorldSiteIntelViewModel BuildViewFromSnapshot(
        WorldSiteIntelSnapshot snapshot,
        WorldIntelVisibility visibility)
    {
        if (snapshot == null)
        {
            return BuildMissingView("", visibility);
        }

        WorldSiteIntelPolicy policy = ParsePolicy(snapshot.IntelPolicy);
        if (visibility == WorldIntelVisibility.Unknown)
        {
            return new WorldSiteIntelViewModel
            {
                SiteId = snapshot.SiteId,
                DisplayName = snapshot.DisplayName,
                Visibility = visibility,
                LastSeenWorldTick = snapshot.LastSeenWorldTick,
                Policy = policy,
                UnknownIntelReasons = { UnknownSiteReason }
            };
        }

        WorldSiteIntelViewModel view = new()
        {
            SiteId = snapshot.SiteId,
            DisplayName = snapshot.DisplayName,
            Visibility = visibility,
            IsStale = visibility == WorldIntelVisibility.Revealed,
            LastSeenWorldTick = snapshot.LastSeenWorldTick,
            Policy = policy,
            StrategicSummary = snapshot.StrategicSummary,
            TacticalSummary = snapshot.TacticalSummary,
            HiddenTacticalSummary = snapshot.HiddenTacticalSummary,
            KnownEntranceIds = snapshot.KnownEntranceIds.ToList(),
            UnknownIntelReasons = snapshot.UnknownIntelReasons.ToList(),
            ActiveObscurationSourceIds = snapshot.ActiveObscurationSourceIds.ToList(),
            KnownTacticalTags = snapshot.KnownTacticalTags.ToList()
        };

        view.CanInspectStrategicSummary = true;
        view.CanInspectSiteMap = true;
        view.CanInspectFullTacticalLayout = CanInspectFullTacticalLayout(
            visibility,
            view.Policy,
            view.ActiveObscurationSourceIds.Count);
        view.AvailableApproaches = BuildApproaches(view.Policy, view.CanInspectFullTacticalLayout);
        return view;
    }

    public static void ApplySiteIntelToRequest(
        StrategicWorldState state,
        StrategicWorldDefinition definition,
        BattleStartRequest request,
        string siteId)
    {
        if (request == null)
        {
            return;
        }

        WorldSiteIntelViewModel view = BuildCurrentView(
            state,
            definition,
            siteId,
            WorldIntelVisibility.Visible);

        request.SiteIntelPolicyId = view.Policy.ToString();
        request.RevealedEntranceIds.Clear();
        request.RevealedEntranceIds.AddRange(view.KnownEntranceIds);
        request.KnownTacticalTags.Clear();
        request.KnownTacticalTags.AddRange(view.KnownTacticalTags);
        request.ActiveObscurationSourceIds.Clear();
        request.ActiveObscurationSourceIds.AddRange(view.ActiveObscurationSourceIds);
    }

    private static WorldSiteIntelViewModel BuildCurrentView(
        WorldSiteDefinition definition,
        WorldSiteState state,
        int worldTick,
        WorldIntelVisibility visibility)
    {
        if (definition == null || state == null)
        {
            return BuildMissingView(state?.SiteId ?? definition?.Id ?? "", visibility);
        }

        if (definition.Intel == null)
        {
            LogMissingIntelDefinitionOnce(definition.Id);
            return BuildMissingIntelView(definition, state, worldTick, visibility);
        }

        WorldSiteIntelDefinition intel = definition.Intel;
        WorldSiteIntelViewModel view = new()
        {
            SiteId = definition.Id,
            DisplayName = definition.DisplayName,
            Visibility = visibility,
            LastSeenWorldTick = worldTick,
            Policy = intel.Policy
        };

        if (visibility == WorldIntelVisibility.Unknown)
        {
            view.UnknownIntelReasons.Add(UnknownSiteReason);
            return view;
        }

        view.CanInspectStrategicSummary = true;
        view.CanInspectSiteMap = true;
        view.StrategicSummary = intel.StrategicSummary;
        view.TacticalSummary = intel.TacticalSummary;
        view.HiddenTacticalSummary = intel.HiddenTacticalSummary;
        view.ActiveObscurationSourceIds = GetActiveObscurationSources(intel, state)
            .Select(source => source.Id)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        view.KnownTacticalTags = state.Memory.KnownTacticalTags.ToList();
        view.CanInspectFullTacticalLayout = CanInspectFullTacticalLayout(
            visibility,
            intel.Policy,
            view.ActiveObscurationSourceIds.Count);
        view.KnownEntranceIds = BuildKnownEntranceIds(definition, state, intel, view.CanInspectFullTacticalLayout);
        view.UnknownIntelReasons = BuildUnknownIntelReasons(intel, state, view.CanInspectFullTacticalLayout);
        view.AvailableApproaches = BuildApproaches(intel.Policy, view.CanInspectFullTacticalLayout);
        return view;
    }

    private static bool CanInspectFullTacticalLayout(
        WorldIntelVisibility visibility,
        WorldSiteIntelPolicy policy,
        int activeObscurationSourceCount)
    {
        if (visibility != WorldIntelVisibility.Visible || activeObscurationSourceCount > 0)
        {
            return false;
        }

        return policy is WorldSiteIntelPolicy.Transparent or WorldSiteIntelPolicy.Obscured;
    }

    private static WorldSiteIntelViewModel BuildMissingView(string siteId, WorldIntelVisibility visibility)
    {
        return new WorldSiteIntelViewModel
        {
            SiteId = siteId ?? "",
            DisplayName = MissingSiteDisplayName,
            Visibility = visibility,
            UnknownIntelReasons = { MissingSiteReason }
        };
    }

    private static WorldSiteIntelViewModel BuildMissingIntelView(
        WorldSiteDefinition definition,
        WorldSiteState state,
        int worldTick,
        WorldIntelVisibility visibility)
    {
        WorldSiteIntelViewModel view = new()
        {
            SiteId = definition.Id,
            DisplayName = definition.DisplayName,
            Visibility = visibility,
            LastSeenWorldTick = worldTick,
            Policy = WorldSiteIntelPolicy.Partial,
            UnknownIntelReasons = { MissingIntelReason }
        };

        if (visibility == WorldIntelVisibility.Unknown)
        {
            view.UnknownIntelReasons.Clear();
            view.UnknownIntelReasons.Add(UnknownSiteReason);
            return view;
        }

        view.CanInspectStrategicSummary = true;
        view.CanInspectSiteMap = true;
        view.KnownEntranceIds = state.Memory.RevealedEntranceIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        view.KnownTacticalTags = state.Memory.KnownTacticalTags.ToList();
        view.AvailableApproaches = BuildApproaches(view.Policy, view.CanInspectFullTacticalLayout);
        return view;
    }

    private static void LogMissingIntelDefinitionOnce(string siteId)
    {
        string key = string.IsNullOrWhiteSpace(siteId) ? "<unknown>" : siteId;
        lock (MissingIntelLogSync)
        {
            if (!MissingIntelLoggedSiteIds.Add(key))
            {
                return;
            }
        }

        GameLog.Warn(nameof(WorldSiteIntelService), $"WorldSiteIntelDefinitionMissing site={key}");
    }

    private static List<string> BuildKnownEntranceIds(
        WorldSiteDefinition definition,
        WorldSiteState state,
        WorldSiteIntelDefinition intel,
        bool canInspectFullTacticalLayout)
    {
        List<string> known = new();
        if (canInspectFullTacticalLayout)
        {
            AddRangeUnique(known, definition.EntranceDefinitions.Select(entrance => entrance.EntranceId));
        }
        else
        {
            AddRangeUnique(known, intel.PublicEntranceIds);
            AddRangeUnique(known, state.Memory.RevealedEntranceIds);
        }

        return known;
    }

    private static List<string> BuildUnknownIntelReasons(
        WorldSiteIntelDefinition intel,
        WorldSiteState state,
        bool canInspectFullTacticalLayout)
    {
        List<string> reasons = new();
        foreach (WorldSiteObscurationDefinition source in GetActiveObscurationSources(intel, state))
        {
            string displayName = string.IsNullOrWhiteSpace(source.DisplayName) ? source.Id : source.DisplayName;
            string description = string.IsNullOrWhiteSpace(source.Description) ? DefaultHiddenTacticalSummary : source.Description;
            reasons.Add($"{displayName}: {description}");
        }

        if (!canInspectFullTacticalLayout && intel.Policy == WorldSiteIntelPolicy.Partial)
        {
            reasons.Add(string.IsNullOrWhiteSpace(intel.HiddenTacticalSummary)
                ? DefaultHiddenTacticalSummary
                : intel.HiddenTacticalSummary);
        }

        return reasons;
    }

    private static List<WorldSiteObscurationDefinition> GetActiveObscurationSources(
        WorldSiteIntelDefinition intel,
        WorldSiteState state)
    {
        return intel.ObscurationSources
            .Where(source => source.HidesTacticalLayout)
            .Where(source => !source.DisabledByResolvedPointIds.Any(id => state.Memory.ResolvedPointIds.Contains(id)))
            .Where(source => !source.DisabledByActiveTags.Any(id => state.ActiveTags.Contains(id)))
            .ToList();
    }

    private static List<WorldSiteApproachViewModel> BuildApproaches(
        WorldSiteIntelPolicy policy,
        bool canInspectFullTacticalLayout)
    {
        List<WorldSiteApproachViewModel> approaches = new();
        if (policy is WorldSiteIntelPolicy.Partial or WorldSiteIntelPolicy.Obscured)
        {
            approaches.Add(new WorldSiteApproachViewModel
            {
                ActionId = "direct_assault",
                DisplayName = "直接进攻",
                Description = canInspectFullTacticalLayout
                    ? "依据已确认的战术信息进入战斗。"
                    : "依据已知入口进入战斗，未确认的战术信息仍会保留风险。",
                IsRecommended = canInspectFullTacticalLayout
            });
        }

        return approaches;
    }

    private static WorldSiteIntelPolicy ParsePolicy(string value)
    {
        return Enum.TryParse(value, ignoreCase: true, out WorldSiteIntelPolicy policy)
            ? policy
            : WorldSiteIntelPolicy.Transparent;
    }

    private static ResourceStore CloneResourceStore(ResourceStore source)
    {
        ResourceStore clone = new();
        if (source == null)
        {
            return clone;
        }

        foreach ((string resourceId, int amount) in source.Amounts)
        {
            clone.Amounts[resourceId] = amount;
        }

        clone.Reservations = source.Reservations
            .Select(reservation => new ResourceReservation(
                reservation.ResourceId,
                reservation.Amount,
                reservation.SourceId,
                reservation.SourceKind))
            .ToList();
        return clone;
    }

    private static FacilityInstance CloneFacility(FacilityInstance source)
    {
        return new FacilityInstance
        {
            InstanceId = source.InstanceId,
            FacilityId = source.FacilityId,
            SiteId = source.SiteId,
            SlotId = source.SlotId,
            Level = source.Level,
            State = source.State,
            AssignedPopulation = source.AssignedPopulation,
            ProgressTicks = source.ProgressTicks,
            Cooldowns = source.Cooldowns.ToList(),
            ActiveTags = source.ActiveTags.ToList()
        };
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

    private static void AddRangeUnique(List<string> target, IEnumerable<string> values)
    {
        foreach (string value in values)
        {
            if (!string.IsNullOrWhiteSpace(value) && !target.Contains(value, StringComparer.Ordinal))
            {
                target.Add(value);
            }
        }
    }
}
