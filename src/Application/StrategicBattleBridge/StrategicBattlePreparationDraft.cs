using System;
using System.Collections.Generic;
using System.Linq;
using Rpg.Application.Battle;
using Rpg.Application.Battle.Snapshots;

namespace Rpg.Application.StrategicBattleBridge;

public sealed class StrategicBattlePreparationDraft : BattleStartRequest
{
    public string DraftId { get; init; } = "";
    public string SessionId { get; set; } = "";
    public long Revision { get; init; } = 1;
}

internal static class StrategicBattlePreparationDraftAdapter
{
    public static StrategicBattlePreparationDraft Create(
        StrategicBattleSession session,
        BattleStartRequest preparationSeed)
    {
        if (session == null || preparationSeed == null || string.IsNullOrWhiteSpace(session.SessionId))
        {
            return null;
        }

        StrategicBattlePreparationDraft draft = new()
        {
            DraftId = $"draft:{session.SessionId}:{Guid.NewGuid():N}",
            SessionId = session.SessionId,
            Revision = 1
        };
        Copy(preparationSeed, draft);
        return draft;
    }

    public static BattleStartRequest CreateCompatibilityProjection(StrategicBattlePreparationDraft draft)
    {
        if (draft == null)
        {
            return null;
        }

        BattleStartRequest projection = new();
        Copy(draft, projection);
        return projection;
    }

    private static void Copy(BattleStartRequest source, BattleStartRequest target)
    {
        target.RequestId = source.RequestId ?? "";
        target.ContextId = source.ContextId ?? "";
        target.BattleKind = source.BattleKind;
        target.EncounterId = source.EncounterId ?? "";
        target.SourceArmyId = source.SourceArmyId ?? "";
        target.TargetArmyId = source.TargetArmyId ?? "";
        target.SourceSiteId = source.SourceSiteId ?? "";
        target.TargetSiteId = source.TargetSiteId ?? "";
        target.StrategicBattleSessionId = source.StrategicBattleSessionId ?? "";
        target.StrategicExpeditionId = source.StrategicExpeditionId ?? "";
        target.StrategicSourceLocationId = source.StrategicSourceLocationId ?? "";
        target.StrategicTargetLocationId = source.StrategicTargetLocationId ?? "";
        target.KnownTacticalTags = (source.KnownTacticalTags ?? new List<string>()).ToList();
        target.AttackerFactionId = source.AttackerFactionId ?? "";
        target.DefenderFactionId = source.DefenderFactionId ?? "";
        target.AttackDirection = source.AttackDirection;
        target.MapDefinitionId = source.MapDefinitionId ?? "";
        target.ObjectiveIds = (source.ObjectiveIds ?? new List<string>()).ToList();
        target.ObjectiveZones = (source.ObjectiveZones ?? new List<BattleObjectiveZoneSnapshot>())
            .Where(item => item != null)
            .Select(CloneObjectiveZone)
            .ToList();
        target.PlayerBattleGroupPlan = ClonePlan(source.PlayerBattleGroupPlan);
        target.PlayerBattleGroupPlans = ClonePlans(source.PlayerBattleGroupPlans);
        target.EnemyBattleGroupPlan = ClonePlan(source.EnemyBattleGroupPlan);
        target.EnemyBattleGroupPlans = ClonePlans(source.EnemyBattleGroupPlans);
        target.EnemyTacticalIntentPlan = CloneTacticalIntent(source.EnemyTacticalIntentPlan);
        target.EnemyTacticalIntentPlans = CloneTacticalIntents(source.EnemyTacticalIntentPlans);
        target.AvailableEntrances = (source.AvailableEntrances ?? new List<BattleEntranceRequest>())
            .Where(item => item != null)
            .Select(CloneEntrance)
            .ToList();
        target.PlayerForces = (source.PlayerForces ?? new List<BattleForceRequest>())
            .Where(item => item != null)
            .Select(CloneForce)
            .ToList();
        target.EnemyForces = (source.EnemyForces ?? new List<BattleForceRequest>())
            .Where(item => item != null)
            .Select(CloneForce)
            .ToList();
        target.NavigationSurfaces = (source.NavigationSurfaces ?? new List<BattleNavigationSurfaceSnapshot>())
            .Where(item => item != null)
            .Select(item => new BattleNavigationSurfaceSnapshot
            {
                X = item.X,
                Y = item.Y,
                Height = item.Height,
                MoveCost = item.MoveCost
            })
            .ToList();
        target.NavigationConnections = (source.NavigationConnections ?? new List<BattleNavigationConnectionSnapshot>())
            .Where(item => item != null)
            .Select(item => new BattleNavigationConnectionSnapshot
            {
                FromX = item.FromX,
                FromY = item.FromY,
                FromHeight = item.FromHeight,
                ToX = item.ToX,
                ToY = item.ToY,
                ToHeight = item.ToHeight,
                MoveCost = item.MoveCost
            })
            .ToList();
        target.NavigationTopology = source.NavigationTopology?.Clone() ?? new();
        target.BattleModifiers = (source.BattleModifiers ?? new List<BattleModifier>())
            .Where(item => item != null)
            .Select(CloneModifier)
            .ToList();
        target.InitialCorpsCommandId = source.InitialCorpsCommandId ?? "";
        target.SiteStateSnapshot = CloneSiteState(source.SiteStateSnapshot);
        target.ReturnScenePath = source.ReturnScenePath ?? "";
        target.SiteScenePath = source.SiteScenePath ?? "";
    }

    private static BattleForceRequest CloneForce(BattleForceRequest source)
    {
        return new BattleForceRequest
        {
            ForceId = source.ForceId ?? "",
            CommandGroupId = source.CommandGroupId ?? "",
            SourceKind = source.SourceKind ?? "",
            SourceId = source.SourceId ?? "",
            UnitDefinitionId = source.UnitDefinitionId ?? "",
            StrategicParticipantId = source.StrategicParticipantId ?? "",
            StrategicHeroId = source.StrategicHeroId ?? "",
            StrategicHeroDefinitionId = source.StrategicHeroDefinitionId ?? "",
            StrategicHeroBattleUnitId = source.StrategicHeroBattleUnitId ?? "",
            StrategicCorpsInstanceId = source.StrategicCorpsInstanceId ?? "",
            StrategicCorpsDefinitionId = source.StrategicCorpsDefinitionId ?? "",
            StrategicCorpsBattleUnitId = source.StrategicCorpsBattleUnitId ?? "",
            StrategicSourceLocationId = source.StrategicSourceLocationId ?? "",
            StrategicPreBattleCorpsStrength = source.StrategicPreBattleCorpsStrength,
            Count = source.Count,
            FootprintWidth = source.FootprintWidth,
            FootprintHeight = source.FootprintHeight,
            MaxHitPoints = source.MaxHitPoints,
            AttackDamage = source.AttackDamage,
            AttackRange = source.AttackRange,
            AttackSpeed = source.AttackSpeed,
            MoveStepSeconds = source.MoveStepSeconds,
            AttackActionSeconds = source.AttackActionSeconds,
            AttackImpactDelaySeconds = source.AttackImpactDelaySeconds,
            FactionId = source.FactionId ?? "",
            PreferredEntranceId = source.PreferredEntranceId ?? "",
            DefaultFormationId = source.DefaultFormationId ?? "",
            TacticalIntentPlan = CloneTacticalIntent(source.TacticalIntentPlan),
            PreferredPlacements = (source.PreferredPlacements ?? new List<BattleForcePlacementRequest>())
                .Select(ClonePlacement)
                .ToList()
        };
    }

    private static BattleForcePlacementRequest ClonePlacement(BattleForcePlacementRequest source)
    {
        return source == null
            ? null
            : new BattleForcePlacementRequest
            {
                PlacementId = source.PlacementId ?? "",
                CellX = source.CellX,
                CellY = source.CellY,
                CellHeight = source.CellHeight
            };
    }

    private static BattleGroupPlanSnapshot ClonePlan(BattleGroupPlanSnapshot source)
    {
        return source == null
            ? new BattleGroupPlanSnapshot()
            : new BattleGroupPlanSnapshot
            {
                BattleGroupId = source.BattleGroupId ?? "",
                ObjectiveZoneId = source.ObjectiveZoneId ?? "",
                EngagementRule = source.EngagementRule,
                InitialFormationId = source.InitialFormationId ?? "",
                HasObjectiveAnchor = source.HasObjectiveAnchor,
                ObjectiveCellX = source.ObjectiveCellX,
                ObjectiveCellY = source.ObjectiveCellY,
                ObjectiveCellHeight = source.ObjectiveCellHeight,
                ObjectiveWidth = source.ObjectiveWidth,
                ObjectiveHeight = source.ObjectiveHeight,
                HasInitialDestinationBeacon = source.HasInitialDestinationBeacon,
                InitialDestinationCellX = source.InitialDestinationCellX,
                InitialDestinationCellY = source.InitialDestinationCellY,
                InitialDestinationCellHeight = source.InitialDestinationCellHeight
            };
    }

    private static Dictionary<string, BattleGroupPlanSnapshot> ClonePlans(
        Dictionary<string, BattleGroupPlanSnapshot> source)
    {
        return (source ?? new Dictionary<string, BattleGroupPlanSnapshot>())
            .Where(item => !string.IsNullOrWhiteSpace(item.Key))
            .ToDictionary(item => item.Key, item => ClonePlan(item.Value), StringComparer.Ordinal);
    }

    private static BattleTacticalIntentPlanSnapshot CloneTacticalIntent(BattleTacticalIntentPlanSnapshot source)
    {
        return source == null
            ? new BattleTacticalIntentPlanSnapshot()
            : new BattleTacticalIntentPlanSnapshot
            {
                IntentId = source.IntentId ?? "",
                PrimaryTargetSelector = source.PrimaryTargetSelector ?? "",
                SecondaryTargetSelectors = (source.SecondaryTargetSelectors ?? new List<string>()).ToList(),
                StyleProfileId = source.StyleProfileId ?? "",
                LeashSelector = source.LeashSelector ?? "",
                RetargetPolicyId = source.RetargetPolicyId ?? "",
                EngagementPolicyId = source.EngagementPolicyId ?? "",
                FallbackIntentId = source.FallbackIntentId ?? "",
                IntentSource = source.IntentSource ?? ""
            };
    }

    private static Dictionary<string, BattleTacticalIntentPlanSnapshot> CloneTacticalIntents(
        Dictionary<string, BattleTacticalIntentPlanSnapshot> source)
    {
        return (source ?? new Dictionary<string, BattleTacticalIntentPlanSnapshot>())
            .Where(item => !string.IsNullOrWhiteSpace(item.Key))
            .ToDictionary(item => item.Key, item => CloneTacticalIntent(item.Value), StringComparer.Ordinal);
    }

    private static BattleObjectiveZoneSnapshot CloneObjectiveZone(BattleObjectiveZoneSnapshot source)
    {
        return new BattleObjectiveZoneSnapshot
        {
            ObjectiveZoneId = source.ObjectiveZoneId ?? "",
            DisplayName = source.DisplayName ?? "",
            ObjectiveRole = source.ObjectiveRole ?? "",
            DeploymentSide = source.DeploymentSide ?? "",
            FactionId = source.FactionId ?? "",
            Priority = source.Priority,
            CellX = source.CellX,
            CellY = source.CellY,
            CellHeight = source.CellHeight,
            Width = source.Width,
            Height = source.Height
        };
    }

    private static BattleEntranceRequest CloneEntrance(BattleEntranceRequest source)
    {
        return new BattleEntranceRequest
        {
            EntranceId = source.EntranceId ?? "",
            DisplayName = source.DisplayName ?? "",
            FactionId = source.FactionId ?? "",
            Capacity = source.Capacity,
            Direction = source.Direction,
            BattleAnchorId = source.BattleAnchorId ?? "",
            Source = source.Source ?? ""
        };
    }

    private static BattleModifier CloneModifier(BattleModifier source)
    {
        return new BattleModifier
        {
            Id = source.Id ?? "",
            Type = source.Type ?? "",
            SourceKind = source.SourceKind ?? "",
            SourceId = source.SourceId ?? "",
            BattleAnchorId = source.BattleAnchorId ?? "",
            Uses = source.Uses,
            Values = new Dictionary<string, int>(source.Values ?? new Dictionary<string, int>(), StringComparer.Ordinal),
            Tags = (source.Tags ?? new List<string>()).ToList()
        };
    }

    private static SiteStateSnapshot CloneSiteState(SiteStateSnapshot source)
    {
        return source == null
            ? new SiteStateSnapshot()
            : new SiteStateSnapshot
            {
                SiteId = source.SiteId ?? "",
                ControlState = source.ControlState,
                DamageLevel = source.DamageLevel,
                GarrisonSummary = new Dictionary<string, int>(source.GarrisonSummary ?? new Dictionary<string, int>(), StringComparer.Ordinal),
                ActiveTags = (source.ActiveTags ?? new List<string>()).ToList()
            };
    }
}
