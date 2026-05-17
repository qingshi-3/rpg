using System.Collections.Generic;
using System.Linq;
using Rpg.Application.Battle;
using Rpg.Definitions.World;
using Rpg.Domain.World;
using Rpg.Infrastructure.Logging;

namespace Rpg.Application.World;

public sealed class WorldBattleRequestBuilder
{
    private readonly WorldSiteDeploymentService _deploymentService = new();
    private readonly WorldSiteBattleUnitPoolService _battleUnitPool = new();

    public BattleStartRequest BuildAssaultBonefieldRequest(
        StrategicWorldState state,
        StrategicWorldDefinition definition,
        string returnScenePath,
        string siteScenePath,
        string sourceArmyId = "")
    {
        StrategicWorldDefinitionQueries queries = new(definition);
        WorldSiteState site = state.SiteStates[StrategicWorldIds.SiteBonefield];
        new StrategicWorldStateInvariantService().RepairGarrisonMetadata(state);
        WorldSiteDefinition siteDefinition = queries.GetSite(StrategicWorldIds.SiteBonefield);
        WorldArmyState sourceArmy = !string.IsNullOrWhiteSpace(sourceArmyId) &&
                                    state.ArmyStates.TryGetValue(sourceArmyId, out WorldArmyState army)
            ? army
            : null;

        BattleStartRequest request = new()
        {
            ContextId = StrategicWorldIds.SiteBonefield,
            BattleKind = BattleKind.AssaultSite,
            EncounterId = "assault_bonefield",
            SourceArmyId = sourceArmyId ?? "",
            SourceSiteId = string.IsNullOrWhiteSpace(sourceArmy?.SourceSiteId) ? StrategicWorldIds.SitePlayerCamp : sourceArmy.SourceSiteId,
            TargetSiteId = StrategicWorldIds.SiteBonefield,
            AttackerFactionId = StrategicWorldIds.FactionPlayer,
            DefenderFactionId = StrategicWorldIds.FactionUndead,
            AttackDirection = ResolveAttackDirection(definition, sourceArmy, StrategicWorldIds.SitePlayerCamp, StrategicWorldIds.SiteBonefield),
            MapDefinitionId = "bonefield_assault_v1",
            ReturnScenePath = returnScenePath ?? "",
            SiteScenePath = string.IsNullOrWhiteSpace(siteScenePath) ? "res://scenes/world/sites/WorldSiteRoot.tscn" : siteScenePath,
            SiteStateSnapshot = BuildSnapshot(site)
        };
        WorldSiteIntelService.ApplySiteIntelToRequest(state, definition, request, request.TargetSiteId);
        request.ObjectiveIds.Add("occupy_bonefield");
        AddEntrances(request, siteDefinition, StrategicWorldIds.FactionPlayer, "", includeGarrisonEntrances: false, request.RevealedEntranceIds);
        AddEntrances(request, siteDefinition, StrategicWorldIds.FactionUndead, "", includeGarrisonEntrances: false);
        if (sourceArmy != null)
        {
            _battleUnitPool.ImportArmyForSiteBattle(site, sourceArmy, state.PlayerFactionId);
            AddSiteGarrisonForces(
                request.PlayerForces,
                site,
                "PlayerArmy",
                state.PlayerFactionId,
                sourceFilterKind: "PlayerArmy",
                sourceFilterId: sourceArmy.ArmyId);
        }
        else if (state.SiteStates.TryGetValue(StrategicWorldIds.SitePlayerCamp, out WorldSiteState sourceSite))
        {
            AddSiteGarrisonForces(
                request.PlayerForces,
                sourceSite,
                "SourceSite",
                state.PlayerFactionId,
                factionFilterId: state.PlayerFactionId);
        }

        _deploymentService.EnsureGarrisonPlacements(site, siteDefinition);
        AddSiteGarrisonForces(
            request.EnemyForces,
            site,
            "DefenderSite",
            request.DefenderFactionId,
            factionFilterId: request.DefenderFactionId);

        GameLog.Info(nameof(WorldBattleRequestBuilder), $"BattleRequested kind={request.BattleKind} target={request.TargetSiteId} direction={request.AttackDirection} request={request.RequestId}");
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
        WorldArmyState threatArmy = !string.IsNullOrWhiteSpace(threat.WorldArmyId) &&
                                    state.ArmyStates.TryGetValue(threat.WorldArmyId, out WorldArmyState resolvedThreatArmy)
            ? resolvedThreatArmy
            : null;
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
            AttackDirection = ResolveAttackDirection(definition, threatArmy, threat.SourceSiteId, threat.TargetSiteId),
            MapDefinitionId = "bonefield_defense_v1",
            ReturnScenePath = returnScenePath ?? "",
            SiteScenePath = string.IsNullOrWhiteSpace(siteScenePath) ? "res://scenes/world/sites/WorldSiteRoot.tscn" : siteScenePath,
            SiteStateSnapshot = BuildSnapshot(site)
        };
        WorldSiteIntelService.ApplySiteIntelToRequest(state, definition, request, request.TargetSiteId);
        request.ObjectiveIds.Add("defend_bonefield");
        AddEntrances(request, siteDefinition, StrategicWorldIds.FactionPlayer, "", includeGarrisonEntrances: true, request.RevealedEntranceIds);
        AddEntrances(request, siteDefinition, request.AttackerFactionId, "", includeGarrisonEntrances: false);

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
            request.PlayerForces.Add(force);
        }

        if (threatArmy != null)
        {
            AddArmyForces(request.EnemyForces, threatArmy, "ThreatArmy", threatArmy.OwnerFactionId);
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

        GameLog.Info(nameof(WorldBattleRequestBuilder), $"BattleRequested kind={request.BattleKind} target={request.TargetSiteId} threat={request.ThreatId} direction={request.AttackDirection} modifiers={request.BattleModifiers.Count} forces={request.PlayerForces.Count}");
        return request;
    }

    public BattleStartRequest BuildWorldBattleInterventionRequest(
        StrategicWorldState state,
        StrategicWorldDefinition definition,
        string worldBattleId,
        string returnScenePath,
        string siteScenePath)
    {
        if (state == null ||
            string.IsNullOrWhiteSpace(worldBattleId) ||
            state.WorldBattleStates == null ||
            !state.WorldBattleStates.TryGetValue(worldBattleId, out WorldBattleState battle))
        {
            return null;
        }

        BattleStartRequest request = BuildDefenseRaidRequest(
            state,
            definition,
            battle.ThreatId,
            returnScenePath,
            siteScenePath);
        request.WorldBattleId = battle.BattleId;
        request.WorldBattlePhase = battle.CurrentPhase.ToString();
        request.ContextId = battle.BattleId;
        request.EncounterId = $"{request.EncounterId}:{battle.CurrentPhase}";
        request.AttackerFactionId = battle.AttackerFactionId;
        request.DefenderFactionId = battle.DefenderFactionId;
        request.BattleModifiers.Add(BuildWorldBattlePhaseModifier(battle));
        GameLog.Info(
            nameof(WorldBattleRequestBuilder),
            $"WorldBattleInterventionRequested battle={battle.BattleId} phase={battle.CurrentPhase} request={request.RequestId}");
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
            AttackDirection = ResolveAttackDirection(definition, playerArmy.SourceSiteId, site.SiteId),
            MapDefinitionId = "field_intercept_v1",
            ReturnScenePath = returnScenePath ?? "",
            SiteScenePath = string.IsNullOrWhiteSpace(siteScenePath) ? "res://scenes/world/sites/WorldSiteRoot.tscn" : siteScenePath,
            SiteStateSnapshot = BuildSnapshot(site)
        };
        WorldSiteIntelService.ApplySiteIntelToRequest(state, definition, request, request.TargetSiteId);
        request.ObjectiveIds.Add("win_field_intercept");
        AddEntrances(request, siteDefinition, state.PlayerFactionId, "", includeGarrisonEntrances: false, request.RevealedEntranceIds);
        AddEntrances(request, siteDefinition, enemyArmy.OwnerFactionId, "", includeGarrisonEntrances: false);
        AddArmyForces(request.PlayerForces, playerArmy, "PlayerArmy", state.PlayerFactionId);
        AddArmyForces(request.EnemyForces, enemyArmy, "EnemyArmy", enemyArmy.OwnerFactionId);

        GameLog.Info(nameof(WorldBattleRequestBuilder), $"BattleRequested kind={request.BattleKind} playerArmy={playerArmy.ArmyId} enemyArmy={enemyArmy.ArmyId} threat={request.ThreatId} direction={request.AttackDirection}");
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

    private static BattleModifier BuildWorldBattlePhaseModifier(WorldBattleState battle)
    {
        int pressure = battle.CurrentPhase switch
        {
            WorldBattlePhase.Skirmish => 1,
            WorldBattlePhase.Engagement => 2,
            WorldBattlePhase.Decisive => 3,
            WorldBattlePhase.Resolution => 4,
            _ => 0
        };
        bool attackerHasMomentum = battle.ProjectedOutcome is WorldBattleOutcome.AttackerDamagedSite or WorldBattleOutcome.AttackerCapturedSite ||
                                   battle.AttackerPower >= battle.DefenderPower;
        Dictionary<string, int> values = new()
        {
            ["phase_pressure"] = pressure,
            ["attacker_power"] = battle.AttackerPower,
            ["defender_power"] = battle.DefenderPower
        };
        if (pressure > 0)
        {
            values[attackerHasMomentum ? "player_damage" : "enemy_damage"] = pressure;
        }

        return new BattleModifier
        {
            Id = $"{battle.BattleId}:phase:{battle.CurrentPhase}",
            Type = "world_battle_phase",
            SourceKind = "WorldBattle",
            SourceId = battle.BattleId,
            Uses = 1,
            Values = values,
            Tags =
            {
                battle.CurrentPhase.ToString(),
                battle.ProjectedOutcome.ToString()
            }
        };
    }

    private static void AddEntrances(
        BattleStartRequest request,
        WorldSiteDefinition siteDefinition,
        string factionId,
        string preferredEntranceId,
        bool includeGarrisonEntrances = true,
        IReadOnlyCollection<string> visibleEntranceIds = null)
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

            if (!includeGarrisonEntrances &&
                string.Equals(entrance.Source, "Garrison", System.StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            bool isGarrisonEntrance = string.Equals(entrance.Source, "Garrison", System.StringComparison.OrdinalIgnoreCase);
            if (!isGarrisonEntrance &&
                visibleEntranceIds != null &&
                !visibleEntranceIds.Contains(entrance.EntranceId))
            {
                continue;
            }

            string resolvedFactionId = string.IsNullOrWhiteSpace(entrance.FactionId) ? factionId : entrance.FactionId;
            if (request.AvailableEntrances.Any(existing =>
                    existing.EntranceId == entrance.EntranceId &&
                    existing.FactionId == resolvedFactionId &&
                    existing.Direction == entrance.Direction))
            {
                continue;
            }

            request.AvailableEntrances.Add(new BattleEntranceRequest
            {
                EntranceId = entrance.EntranceId,
                DisplayName = entrance.DisplayName,
                FactionId = resolvedFactionId,
                Capacity = entrance.Capacity,
                Direction = entrance.Direction,
                BattleAnchorId = entrance.BattleAnchorId,
                Source = entrance.Source
            });
        }
    }

    private static WorldSiteAttackDirection ResolveAttackDirection(
        StrategicWorldDefinition definition,
        WorldArmyState sourceArmy,
        string fallbackSourceSiteId,
        string targetSiteId)
    {
        if (sourceArmy != null && !string.IsNullOrWhiteSpace(targetSiteId))
        {
            if (sourceArmy.TargetApproachDirection != WorldSiteAttackDirection.Any)
            {
                return sourceArmy.TargetApproachDirection;
            }

            WorldSiteAttackDirection direction = ResolveAttackDirection(definition, sourceArmy.WorldPosition, targetSiteId);
            if (direction != WorldSiteAttackDirection.Any)
            {
                return direction;
            }
        }

        return ResolveAttackDirection(definition, fallbackSourceSiteId, targetSiteId);
    }

    private static WorldSiteAttackDirection ResolveAttackDirection(
        StrategicWorldDefinition definition,
        Godot.Vector2 sourcePosition,
        string targetSiteId)
    {
        if (definition == null || string.IsNullOrWhiteSpace(targetSiteId))
        {
            return WorldSiteAttackDirection.Any;
        }

        WorldSiteDefinition targetSite = definition.SiteDefinitions.FirstOrDefault(site => site.Id == targetSiteId);
        if (targetSite == null)
        {
            return WorldSiteAttackDirection.Any;
        }

        return ResolveDirectionFromDelta(sourcePosition.X - targetSite.MapPosition.X, sourcePosition.Y - targetSite.MapPosition.Y);
    }

    private static WorldSiteAttackDirection ResolveAttackDirection(
        StrategicWorldDefinition definition,
        string sourceSiteId,
        string targetSiteId)
    {
        if (definition == null ||
            string.IsNullOrWhiteSpace(sourceSiteId) ||
            string.IsNullOrWhiteSpace(targetSiteId) ||
            sourceSiteId == targetSiteId)
        {
            return WorldSiteAttackDirection.Any;
        }

        WorldSiteDefinition sourceSite = definition.SiteDefinitions.FirstOrDefault(site => site.Id == sourceSiteId);
        WorldSiteDefinition targetSite = definition.SiteDefinitions.FirstOrDefault(site => site.Id == targetSiteId);
        if (sourceSite == null || targetSite == null)
        {
            return WorldSiteAttackDirection.Any;
        }

        return ResolveDirectionFromDelta(sourceSite.MapPosition.X - targetSite.MapPosition.X, sourceSite.MapPosition.Y - targetSite.MapPosition.Y);
    }

    private static WorldSiteAttackDirection ResolveDirectionFromDelta(float deltaX, float deltaY)
    {
        if (System.Math.Abs(deltaX) < 0.001f && System.Math.Abs(deltaY) < 0.001f)
        {
            return WorldSiteAttackDirection.Any;
        }

        if (System.Math.Abs(deltaX) >= System.Math.Abs(deltaY))
        {
            return deltaX < 0f ? WorldSiteAttackDirection.West : WorldSiteAttackDirection.East;
        }

        return deltaY < 0f ? WorldSiteAttackDirection.North : WorldSiteAttackDirection.South;
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
        string factionId,
        string factionFilterId = "",
        string sourceFilterKind = "",
        string sourceFilterId = "")
    {
        if (target == null || site == null)
        {
            return;
        }

        foreach (GarrisonState garrison in site.Garrison.Where(item =>
                     item.Count > 0 &&
                     MatchesOptionalFilter(item.FactionId, factionFilterId) &&
                     MatchesOptionalFilter(item.SourceKind, sourceFilterKind) &&
                     MatchesOptionalFilter(item.SourceId, sourceFilterId)))
        {
            string resolvedSourceKind = string.IsNullOrWhiteSpace(garrison.SourceKind) ||
                                        sourceKind is "SourceSite" or "DefenderSite"
                ? sourceKind
                : garrison.SourceKind;
            string resolvedSourceId = string.IsNullOrWhiteSpace(garrison.SourceId) ||
                                      sourceKind is "SourceSite" or "DefenderSite"
                ? site.SiteId
                : garrison.SourceId;
            BattleForceRequest force = new()
            {
                ForceId = $"{resolvedSourceId}:{garrison.UnitTypeId}",
                SourceKind = resolvedSourceKind,
                SourceId = resolvedSourceId,
                UnitDefinitionId = garrison.UnitTypeId,
                Count = garrison.Count,
                FactionId = string.IsNullOrWhiteSpace(garrison.FactionId) ? factionId : garrison.FactionId
            };
            target.Add(force);
        }
    }

    private static bool MatchesOptionalFilter(string current, string expected)
    {
        return string.IsNullOrWhiteSpace(expected) ||
               string.Equals(current ?? "", expected, System.StringComparison.Ordinal);
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
