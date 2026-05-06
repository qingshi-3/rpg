using System.Linq;
using Rpg.Application.Battle;
using Rpg.Definitions.World;
using Rpg.Domain.World;
using Rpg.Infrastructure.Logging;

namespace Rpg.Application.World;

public sealed class WorldBattleRequestBuilder
{
    private readonly WorldSiteDeploymentService _deploymentService = new();

    public BattleStartRequest BuildAssaultBonefieldRequest(
        StrategicWorldState state,
        StrategicWorldDefinition definition,
        string returnScenePath,
        string siteScenePath,
        string sourceArmyId = "")
    {
        StrategicWorldDefinitionQueries queries = new(definition);
        WorldSiteState site = state.SiteStates[StrategicWorldIds.SiteBonefield];
        WorldSiteDefinition siteDefinition = queries.GetSite(StrategicWorldIds.SiteBonefield);

        BattleStartRequest request = new()
        {
            ContextId = StrategicWorldIds.SiteBonefield,
            BattleKind = BattleKind.AssaultSite,
            EncounterId = "assault_bonefield",
            SourceArmyId = sourceArmyId ?? "",
            SourceSiteId = StrategicWorldIds.SitePlayerCamp,
            TargetSiteId = StrategicWorldIds.SiteBonefield,
            AttackerFactionId = StrategicWorldIds.FactionPlayer,
            DefenderFactionId = StrategicWorldIds.FactionUndead,
            MapDefinitionId = "bonefield_assault_v1",
            ReturnScenePath = returnScenePath ?? "",
            SiteScenePath = string.IsNullOrWhiteSpace(siteScenePath) ? "res://scenes/world/sites/WorldSiteRoot.tscn" : siteScenePath,
            SiteStateSnapshot = BuildSnapshot(site)
        };
        request.ObjectiveIds.Add("occupy_bonefield");
        AddEntrances(request, siteDefinition, StrategicWorldIds.FactionPlayer, "main_entrance");
        if (!string.IsNullOrWhiteSpace(sourceArmyId) &&
            state.ArmyStates.TryGetValue(sourceArmyId, out WorldArmyState sourceArmy))
        {
            AddArmyForces(request.PlayerForces, sourceArmy, "PlayerArmy", state.PlayerFactionId);
        }
        else if (state.SiteStates.TryGetValue(StrategicWorldIds.SitePlayerCamp, out WorldSiteState sourceSite))
        {
            AddSiteGarrisonForces(request.PlayerForces, sourceSite, "SourceSite", state.PlayerFactionId);
        }

        _deploymentService.EnsureGarrisonPlacements(site, siteDefinition);
        AddSiteGarrisonForces(request.EnemyForces, site, "DefenderSite", request.DefenderFactionId);

        GameLog.Info(nameof(WorldBattleRequestBuilder), $"BattleRequested kind={request.BattleKind} target={request.TargetSiteId} request={request.RequestId}");
        return request;
    }

    public BattleStartRequest BuildDefenseRaidRequest(
        StrategicWorldState state,
        StrategicWorldDefinition definition,
        string threatId,
        string returnScenePath,
        string siteScenePath)
    {
        StrategicWorldDefinitionQueries queries = new(definition);
        EnemyThreatPlan threat = state.ThreatPlans[threatId];
        WorldSiteState site = state.SiteStates[threat.TargetSiteId];
        WorldSiteDefinition siteDefinition = queries.GetSite(threat.TargetSiteId);
        _deploymentService.EnsureGarrisonPlacements(site, siteDefinition);

        BattleStartRequest request = new()
        {
            ContextId = threat.TargetSiteId,
            BattleKind = BattleKind.DefenseRaid,
            EncounterId = threat.EnemyGroupId,
            SourceSiteId = threat.SourceSiteId,
            TargetSiteId = threat.TargetSiteId,
            ThreatId = threat.Id,
            AttackerFactionId = StrategicWorldIds.FactionUndead,
            DefenderFactionId = StrategicWorldIds.FactionPlayer,
            MapDefinitionId = "bonefield_defense_v1",
            ReturnScenePath = returnScenePath ?? "",
            SiteScenePath = string.IsNullOrWhiteSpace(siteScenePath) ? "res://scenes/world/sites/WorldSiteRoot.tscn" : siteScenePath,
            SiteStateSnapshot = BuildSnapshot(site)
        };
        request.ObjectiveIds.Add("defend_bonefield");
        AddEntrances(request, siteDefinition, StrategicWorldIds.FactionPlayer, "");

        foreach (GarrisonState garrison in site.Garrison.Where(item => item.Count > 0))
        {
            BattleForceRequest force = new()
            {
                ForceId = $"{site.SiteId}:{garrison.UnitTypeId}",
                SourceKind = "Garrison",
                SourceId = site.SiteId,
                UnitDefinitionId = garrison.UnitTypeId,
                Count = garrison.Count,
                FactionId = StrategicWorldIds.FactionPlayer,
                PreferredEntranceId = "defense_post"
            };
            AddGarrisonPlacements(force, site, garrison.UnitTypeId, garrison.Count);
            request.PlayerForces.Add(force);
        }

        if (!string.IsNullOrWhiteSpace(threat.WorldArmyId) &&
            state.ArmyStates.TryGetValue(threat.WorldArmyId, out WorldArmyState enemyArmy))
        {
            AddArmyForces(request.EnemyForces, enemyArmy, "ThreatArmy", enemyArmy.OwnerFactionId);
        }
        else
        {
            ThreatRuleDefinition threatRule = definition.ThreatRules.FirstOrDefault(rule => rule.Id == threat.RuleId);
            AddDefinitionForces(
                request.EnemyForces,
                threatRule?.EnemyForces,
                "ThreatRule",
                string.IsNullOrWhiteSpace(threat.EnemyGroupId) ? threat.RuleId : threat.EnemyGroupId,
                request.AttackerFactionId);
        }

        foreach (FacilityInstance facility in site.Facilities.Where(item => item.State == FacilityState.Active))
        {
            FacilityDefinition facilityDefinition = queries.GetFacility(facility.FacilityId);
            if (facilityDefinition == null)
            {
                continue;
            }

            foreach (Rpg.Definitions.World.BattleModifierDefinition modifier in facilityDefinition.BattleModifiers)
            {
                request.BattleModifiers.Add(new BattleModifier
                {
                    Id = $"{facility.InstanceId}:{modifier.Id}",
                    Type = modifier.Type,
                    SourceKind = "Facility",
                    SourceId = facility.InstanceId,
                    BattleAnchorId = modifier.BattleAnchorId,
                    Uses = modifier.Uses,
                    Values = modifier.Values.ToDictionary(item => item.Key, item => item.Value),
                    Tags = modifier.Tags.ToList()
                });
            }
        }

        GameLog.Info(nameof(WorldBattleRequestBuilder), $"BattleRequested kind={request.BattleKind} target={request.TargetSiteId} threat={request.ThreatId} modifiers={request.BattleModifiers.Count} forces={request.PlayerForces.Count}");
        return request;
    }

    public BattleStartRequest BuildFieldInterceptRequest(
        StrategicWorldState state,
        StrategicWorldDefinition definition,
        string playerArmyId,
        string enemyArmyId,
        string returnScenePath,
        string siteScenePath)
    {
        StrategicWorldDefinitionQueries queries = new(definition);
        WorldArmyState playerArmy = state.ArmyStates[playerArmyId];
        WorldArmyState enemyArmy = state.ArmyStates[enemyArmyId];
        string contextSiteId = !string.IsNullOrWhiteSpace(enemyArmy.TargetSiteId)
            ? enemyArmy.TargetSiteId
            : !string.IsNullOrWhiteSpace(playerArmy.TargetSiteId)
                ? playerArmy.TargetSiteId
                : StrategicWorldIds.SiteBonefield;
        WorldSiteState site = state.SiteStates.TryGetValue(contextSiteId, out WorldSiteState siteState)
            ? siteState
            : state.SiteStates[StrategicWorldIds.SiteBonefield];
        WorldSiteDefinition siteDefinition = queries.GetSite(site.SiteId);

        BattleStartRequest request = new()
        {
            ContextId = $"field_intercept:{playerArmy.ArmyId}:{enemyArmy.ArmyId}",
            BattleKind = BattleKind.FieldIntercept,
            EncounterId = $"field_intercept:{playerArmy.ArmyId}:{enemyArmy.ArmyId}",
            SourceArmyId = playerArmy.ArmyId,
            TargetArmyId = enemyArmy.ArmyId,
            SourceSiteId = playerArmy.SourceSiteId,
            TargetSiteId = site.SiteId,
            ThreatId = enemyArmy.RelatedThreatId,
            AttackerFactionId = state.PlayerFactionId,
            DefenderFactionId = enemyArmy.OwnerFactionId,
            MapDefinitionId = "field_intercept_v1",
            ReturnScenePath = returnScenePath ?? "",
            SiteScenePath = string.IsNullOrWhiteSpace(siteScenePath) ? "res://scenes/world/sites/WorldSiteRoot.tscn" : siteScenePath,
            SiteStateSnapshot = BuildSnapshot(site)
        };
        request.ObjectiveIds.Add("win_field_intercept");
        AddEntrances(request, siteDefinition, state.PlayerFactionId, "");
        AddArmyForces(request.PlayerForces, playerArmy, "PlayerArmy", state.PlayerFactionId);
        AddArmyForces(request.EnemyForces, enemyArmy, "EnemyArmy", enemyArmy.OwnerFactionId);

        GameLog.Info(nameof(WorldBattleRequestBuilder), $"BattleRequested kind={request.BattleKind} playerArmy={playerArmy.ArmyId} enemyArmy={enemyArmy.ArmyId} threat={request.ThreatId}");
        return request;
    }

    private static SiteStateSnapshot BuildSnapshot(WorldSiteState site)
    {
        SiteStateSnapshot snapshot = new()
        {
            SiteId = site.SiteId,
            ControlState = site.ControlState,
            DamageLevel = site.DamageLevel,
            ActiveTags = site.ActiveTags.ToList()
        };

        foreach (FacilityInstance facility in site.Facilities)
        {
            if (facility.State == FacilityState.Active)
            {
                snapshot.ActiveFacilityIds.Add(facility.FacilityId);
            }
            else if (facility.State == FacilityState.Damaged)
            {
                snapshot.DamagedFacilityIds.Add(facility.FacilityId);
            }
        }

        foreach (GarrisonState garrison in site.Garrison)
        {
            snapshot.GarrisonSummary[garrison.UnitTypeId] = garrison.Count;
        }

        return snapshot;
    }

    private static void AddEntrances(BattleStartRequest request, WorldSiteDefinition siteDefinition, string factionId, string preferredEntranceId)
    {
        if (siteDefinition == null)
        {
            return;
        }

        foreach (Rpg.Definitions.World.BattleEntranceDefinition entrance in siteDefinition.EntranceDefinitions)
        {
            if (!string.IsNullOrWhiteSpace(preferredEntranceId) && entrance.EntranceId != preferredEntranceId)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(entrance.FactionId) && entrance.FactionId != factionId)
            {
                continue;
            }

            request.AvailableEntrances.Add(new BattleEntranceRequest
            {
                EntranceId = entrance.EntranceId,
                DisplayName = entrance.DisplayName,
                FactionId = string.IsNullOrWhiteSpace(entrance.FactionId) ? factionId : entrance.FactionId,
                Capacity = entrance.Capacity,
                BattleAnchorId = entrance.BattleAnchorId,
                Source = entrance.Source
            });
        }
    }

    private static void AddGarrisonPlacements(
        BattleForceRequest force,
        WorldSiteState site,
        string unitTypeId,
        int count)
    {
        if (force == null || site == null || string.IsNullOrWhiteSpace(unitTypeId) || count <= 0)
        {
            return;
        }

        foreach (WorldSiteUnitPlacement placement in site.UnitPlacements
                     .Where(item => item.UnitTypeId == unitTypeId)
                     .OrderBy(item => item.UnitIndex)
                     .Take(count))
        {
            force.PreferredPlacements.Add(new BattleForcePlacementRequest
            {
                PlacementId = placement.PlacementId,
                CellX = placement.CellX,
                CellY = placement.CellY,
                CellHeight = placement.CellHeight
            });
        }
    }

    private static void AddSiteGarrisonForces(
        System.Collections.Generic.ICollection<BattleForceRequest> target,
        WorldSiteState site,
        string sourceKind,
        string factionId)
    {
        if (target == null || site == null)
        {
            return;
        }

        foreach (GarrisonState garrison in site.Garrison.Where(item => item.Count > 0))
        {
            BattleForceRequest force = new()
            {
                ForceId = $"{site.SiteId}:{garrison.UnitTypeId}",
                SourceKind = sourceKind,
                SourceId = site.SiteId,
                UnitDefinitionId = garrison.UnitTypeId,
                Count = garrison.Count,
                FactionId = factionId
            };
            AddGarrisonPlacements(force, site, garrison.UnitTypeId, garrison.Count);
            target.Add(force);
        }
    }

    private static void AddDefinitionForces(
        System.Collections.Generic.ICollection<BattleForceRequest> target,
        System.Collections.Generic.IEnumerable<GarrisonDefinition> forces,
        string sourceKind,
        string sourceId,
        string factionId)
    {
        if (target == null || forces == null)
        {
            return;
        }

        foreach (GarrisonDefinition unit in forces.Where(item => item.Count > 0))
        {
            target.Add(new BattleForceRequest
            {
                ForceId = $"{sourceId}:{unit.UnitTypeId}",
                SourceKind = sourceKind,
                SourceId = sourceId ?? "",
                UnitDefinitionId = unit.UnitTypeId,
                Count = unit.Count,
                FactionId = factionId
            });
        }
    }

    private static void AddArmyForces(
        System.Collections.Generic.ICollection<BattleForceRequest> target,
        WorldArmyState army,
        string sourceKind,
        string factionId)
    {
        foreach (GarrisonState unit in army.GarrisonUnits.Where(item => item.Count > 0))
        {
            target.Add(new BattleForceRequest
            {
                ForceId = $"{army.ArmyId}:{unit.UnitTypeId}",
                SourceKind = sourceKind,
                SourceId = army.ArmyId,
                UnitDefinitionId = unit.UnitTypeId,
                Count = unit.Count,
                FactionId = factionId
            });
        }
    }
}
