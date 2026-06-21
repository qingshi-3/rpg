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
        request.ObjectiveIds.Add("occupy_bonefield");
        AddEntrances(request, siteDefinition, StrategicWorldIds.FactionPlayer, "", includeGarrisonEntrances: false);
        AddEntrances(request, siteDefinition, StrategicWorldIds.FactionUndead, "", includeGarrisonEntrances: false);
        if (sourceArmy != null)
        {
            if (IsStrategicManagementArmy(sourceArmy))
            {
                AddArmyForces(
                    request.PlayerForces,
                    sourceArmy,
                    "PlayerArmy",
                    state.PlayerFactionId,
                    assignFirstSliceHeroCompanyCommandGroups: true);
                GameLog.Info(
                    nameof(WorldBattleRequestBuilder),
                    $"StrategicArmySkippedLegacyGarrisonImport army={sourceArmy.ArmyId} expedition={sourceArmy.StrategicExpeditionId} target={site.SiteId} forces={request.PlayerForces.Count}");
            }
            else
            {
                _battleUnitPool.ImportArmyForSiteBattle(site, sourceArmy, state.PlayerFactionId);
                AddSiteGarrisonForces(
                    request.PlayerForces,
                    site,
                    "PlayerArmy",
                    state.PlayerFactionId,
                    sourceFilterKind: "PlayerArmy",
                    sourceFilterId: sourceArmy.ArmyId,
                    assignFirstSliceHeroCompanyCommandGroups: true);
            }

            ApplyDefaultFormation(request.PlayerForces, sourceArmy.DefaultFormationId);
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
            AttackerFactionId = state.PlayerFactionId,
            DefenderFactionId = enemyArmy.OwnerFactionId,
            AttackDirection = ResolveAttackDirection(definition, playerArmy.SourceSiteId, site.SiteId),
            MapDefinitionId = "field_intercept_v1",
            ReturnScenePath = returnScenePath ?? "",
            SiteScenePath = string.IsNullOrWhiteSpace(siteScenePath) ? "res://scenes/world/sites/WorldSiteRoot.tscn" : siteScenePath,
            SiteStateSnapshot = BuildSnapshot(site)
        };
        request.ObjectiveIds.Add("win_field_intercept");
        AddEntrances(request, siteDefinition, state.PlayerFactionId, "", includeGarrisonEntrances: false);
        AddEntrances(request, siteDefinition, enemyArmy.OwnerFactionId, "", includeGarrisonEntrances: false);
        AddArmyForces(request.PlayerForces, playerArmy, "PlayerArmy", state.PlayerFactionId);
        AddArmyForces(request.EnemyForces, enemyArmy, "EnemyArmy", enemyArmy.OwnerFactionId);

        GameLog.Info(nameof(WorldBattleRequestBuilder), $"BattleRequested kind={request.BattleKind} playerArmy={playerArmy.ArmyId} enemyArmy={enemyArmy.ArmyId} direction={request.AttackDirection}");
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

        foreach (GarrisonState garrison in site.Garrison)
        {
            snapshot.GarrisonSummary[garrison.UnitTypeId] = garrison.Count;
        }

        return snapshot;
    }

    private static void AddEntrances(
        BattleStartRequest request,
        WorldSiteDefinition siteDefinition,
        string factionId,
        string preferredEntranceId,
        bool includeGarrisonEntrances = true)
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
        string sourceFilterId = "",
        bool assignFirstSliceHeroCompanyCommandGroups = false)
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
                CommandGroupId = assignFirstSliceHeroCompanyCommandGroups
                    ? ResolveFirstSliceHeroCompanyCommandGroupId(resolvedSourceKind, resolvedSourceId, garrison.UnitTypeId, garrison.StrategicParticipantId)
                    : "",
                SourceKind = resolvedSourceKind,
                SourceId = resolvedSourceId,
                UnitDefinitionId = garrison.UnitTypeId,
                StrategicParticipantId = garrison.StrategicParticipantId ?? "",
                Count = garrison.Count,
                FactionId = string.IsNullOrWhiteSpace(garrison.FactionId) ? factionId : garrison.FactionId
            };
            target.Add(force);
        }
    }

    private static string ResolveFirstSliceHeroCompanyCommandGroupId(
        string sourceKind,
        string sourceId,
        string unitTypeId,
        string strategicParticipantId = "")
    {
        if (!string.IsNullOrWhiteSpace(strategicParticipantId))
        {
            return strategicParticipantId;
        }

        if (!string.Equals(sourceKind ?? "", "PlayerArmy", System.StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(sourceId) ||
            !FirstSliceHeroCompanyIds.TryGetCompanyByAnyUnit(unitTypeId, out FirstSliceHeroCompanyDefinition company))
        {
            return "";
        }

        return $"PlayerArmy:{sourceId}:company:{company.CompanyId}";
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
        string factionId,
        bool assignFirstSliceHeroCompanyCommandGroups = false)
    {
        foreach (GarrisonState unit in army.GarrisonUnits.Where(item => item.Count > 0))
        {
            string forceId = string.IsNullOrWhiteSpace(unit.SourceId)
                ? $"{army.ArmyId}:{unit.UnitTypeId}"
                : $"{army.ArmyId}:{unit.SourceId}:{unit.UnitTypeId}";
            target.Add(new BattleForceRequest
            {
                ForceId = forceId,
                CommandGroupId = assignFirstSliceHeroCompanyCommandGroups
                    ? ResolveFirstSliceHeroCompanyCommandGroupId(sourceKind, army.ArmyId, unit.UnitTypeId, unit.StrategicParticipantId)
                    : "",
                SourceKind = sourceKind,
                SourceId = army.ArmyId,
                UnitDefinitionId = unit.UnitTypeId,
                StrategicParticipantId = unit.StrategicParticipantId ?? "",
                Count = unit.Count,
                FactionId = factionId,
                DefaultFormationId = army.DefaultFormationId ?? ""
            });
        }
    }

    private static bool IsStrategicManagementArmy(WorldArmyState army)
    {
        return !string.IsNullOrWhiteSpace(army?.StrategicExpeditionId);
    }

    private static void ApplyDefaultFormation(
        System.Collections.Generic.IEnumerable<BattleForceRequest> forces,
        string defaultFormationId)
    {
        string formationId = defaultFormationId?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(formationId))
        {
            return;
        }

        foreach (BattleForceRequest force in forces ?? System.Array.Empty<BattleForceRequest>())
        {
            if (force != null && string.IsNullOrWhiteSpace(force.DefaultFormationId))
            {
                force.DefaultFormationId = formationId;
            }
        }
    }
}
