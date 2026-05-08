using System.Collections.Generic;
using System.Linq;
using Godot;
using Rpg.Application.Battle;
using Rpg.Definitions.World;
using Rpg.Domain.World;
using Rpg.Infrastructure.Logging;

namespace Rpg.Application.World;

public sealed class WorldSiteDeploymentService
{
    public const string DefaultGarrisonZoneId = "default_garrison";

    public bool CanAcceptArmyGarrison(
        StrategicWorldState state,
        StrategicWorldDefinition definition,
        WorldArmyState army,
        out string failureReason)
    {
        failureReason = "";
        if (state == null || definition == null || army == null)
        {
            failureReason = "missing_world_state";
            return false;
        }

        if (!state.SiteStates.TryGetValue(army.TargetSiteId, out WorldSiteState site))
        {
            failureReason = "missing_site";
            return false;
        }

        WorldSiteDefinition siteDefinition = new StrategicWorldDefinitionQueries(definition).GetSite(army.TargetSiteId);
        int incomingSlots = GetArmyGarrisonSlotUsage(army);
        return CanAcceptGarrison(site, siteDefinition, incomingSlots, out failureReason);
    }

    public bool CanAcceptGarrison(
        WorldSiteState site,
        WorldSiteDefinition definition,
        int incomingSlots,
        out string failureReason)
    {
        failureReason = "";
        if (site == null || definition == null)
        {
            failureReason = "missing_site";
            return false;
        }

        SiteDeploymentZoneDefinition zone = GetDefaultGarrisonZone(definition);
        int capacity = GetZoneCapacity(zone);
        if (capacity <= 0)
        {
            failureReason = "garrison_zone_missing";
            return false;
        }

        int used = GetGarrisonSlotUsage(site);
        if (used + System.Math.Max(incomingSlots, 0) > capacity)
        {
            failureReason = "garrison_zone_full";
            return false;
        }

        return true;
    }

    public int GetGarrisonSlotUsage(WorldSiteState site)
    {
        return site?.Garrison.Sum(unit => System.Math.Max(unit.Count, 0)) ?? 0;
    }

    public int GetArmyGarrisonSlotUsage(WorldArmyState army)
    {
        if (army == null)
        {
            return 0;
        }

        int unitCount = army.GarrisonUnits.Sum(unit => System.Math.Max(unit.Count, 0));
        return System.Math.Max(unitCount, 1);
    }

    public int GetDefaultGarrisonCapacity(WorldSiteDefinition definition)
    {
        return GetZoneCapacity(GetDefaultGarrisonZone(definition));
    }

    public SiteDeploymentZoneDefinition GetDefaultGarrisonZone(WorldSiteDefinition definition)
    {
        if (definition == null)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(definition.DefaultGarrisonZoneId))
        {
            SiteDeploymentZoneDefinition configured = definition.DeploymentZones
                .FirstOrDefault(zone => zone.ZoneId == definition.DefaultGarrisonZoneId);
            if (configured != null)
            {
                return configured;
            }
        }

        return definition.DeploymentZones.FirstOrDefault(zone => zone.ZoneKind == SiteDeploymentZoneKind.DefaultGarrison);
    }

    public void EnsureGarrisonPlacements(WorldSiteState site, WorldSiteDefinition definition)
    {
        if (site == null || definition == null)
        {
            return;
        }

        SiteDeploymentZoneDefinition zone = GetDefaultGarrisonZone(definition);
        if (zone == null)
        {
            site.UnitPlacements.RemoveAll(placement => IsGarrisonPlacement(placement));
            return;
        }

        HashSet<string> expectedIds = BuildExpectedPlacementIds(site);
        site.UnitPlacements.RemoveAll(placement =>
            IsGarrisonPlacement(placement) &&
            !expectedIds.Contains(placement.PlacementId));

        foreach (GarrisonState garrison in site.Garrison)
        {
            for (int index = 1; index <= garrison.Count; index++)
            {
                string placementId = BuildGarrisonPlacementId(garrison.UnitTypeId, index);
                WorldSiteUnitPlacement existing = site.UnitPlacements.FirstOrDefault(placement => placement.PlacementId == placementId);
                if (existing != null)
                {
                    ApplyGarrisonMetadata(existing, site, zone, garrison.UnitTypeId, index);
                    continue;
                }

                Vector2I cell = ResolveNextDefaultCell(site, zone);
                site.UnitPlacements.Add(new WorldSiteUnitPlacement
                {
                    PlacementId = placementId,
                    UnitTypeId = garrison.UnitTypeId,
                    UnitIndex = index,
                    FactionId = site.OwnerFactionId,
                    PlacementKind = WorldSiteUnitPlacementKind.Garrison,
                    SourceKind = "Garrison",
                    SourceId = site.SiteId,
                    ZoneId = zone.ZoneId,
                    CellX = cell.X,
                    CellY = cell.Y
                });
            }
        }
    }

    public void ClearBattlePlacementsForForces(WorldSiteState site, IEnumerable<BattleForceRequest> forces)
    {
        if (site == null || forces == null)
        {
            return;
        }

        HashSet<string> sourceKeys = forces
            .Where(force => force != null && force.Count > 0)
            .Select(force => BuildSourceKey(ResolveForceSourceKind(force), ResolveForceSourceId(force)))
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .ToHashSet();
        if (sourceKeys.Count == 0)
        {
            return;
        }

        int removed = site.UnitPlacements.RemoveAll(placement =>
            !IsGarrisonPlacement(placement) &&
            sourceKeys.Contains(BuildSourceKey(placement.SourceKind, placement.SourceId)));
        if (removed > 0)
        {
            GameLog.Info(nameof(WorldSiteDeploymentService), $"BattlePlacementsCleared site={site.SiteId} sources={sourceKeys.Count} removed={removed}");
        }
    }

    public bool EnsureBattlePlacementsForForce(
        WorldSiteState site,
        BattleForceRequest force,
        WorldSiteUnitPlacementKind placementKind,
        WorldSiteAttackDirection direction,
        IReadOnlyList<WorldSiteDeploymentCell> candidates,
        string threatId,
        string preferredEntranceId,
        out string failureReason)
    {
        failureReason = "";
        if (site == null || force == null || string.IsNullOrWhiteSpace(force.UnitDefinitionId))
        {
            failureReason = "missing_force";
            return false;
        }

        if (force.Count <= 0)
        {
            return true;
        }

        if (candidates == null || candidates.Count == 0)
        {
            failureReason = "deployment_candidates_missing";
            GameLog.Error(
                nameof(WorldSiteDeploymentService),
                $"BattlePlacementFailed site={site.SiteId} force={force.ForceId} unit={force.UnitDefinitionId} reason={failureReason} direction={direction}");
            return false;
        }

        string sourceKind = ResolveForceSourceKind(force);
        string sourceId = ResolveForceSourceId(force);
        string armyId = ResolveArmyId(sourceKind, sourceId);
        int created = 0;

        for (int index = 1; index <= force.Count; index++)
        {
            string placementId = BuildBattlePlacementId(sourceKind, sourceId, force.UnitDefinitionId, index);
            WorldSiteUnitPlacement existing = site.UnitPlacements.FirstOrDefault(placement => placement.PlacementId == placementId);
            if (existing != null)
            {
                ApplyBattleMetadata(existing, force, placementKind, sourceKind, sourceId, armyId, threatId, preferredEntranceId, direction, index);
                continue;
            }

            if (!TryResolveNextBattleCell(site, candidates, out WorldSiteDeploymentCell cell))
            {
                failureReason = "deployment_cell_unavailable";
                GameLog.Error(
                    nameof(WorldSiteDeploymentService),
                    $"BattlePlacementFailed site={site.SiteId} force={force.ForceId} unit={force.UnitDefinitionId} index={index} reason={failureReason} direction={direction}");
                return false;
            }

            site.UnitPlacements.Add(new WorldSiteUnitPlacement
            {
                PlacementId = placementId,
                UnitTypeId = force.UnitDefinitionId,
                UnitIndex = index,
                FactionId = force.FactionId ?? "",
                PlacementKind = placementKind,
                SourceKind = sourceKind,
                SourceId = sourceId,
                ArmyId = armyId,
                ThreatId = threatId ?? "",
                ZoneId = "",
                EntranceId = preferredEntranceId ?? "",
                AttackDirection = direction,
                CellX = cell.Cell.X,
                CellY = cell.Cell.Y,
                CellHeight = cell.Height
            });
            created++;
        }

        GameLog.Info(
            nameof(WorldSiteDeploymentService),
            $"BattlePlacementsEnsured site={site.SiteId} force={force.ForceId} unit={force.UnitDefinitionId} count={force.Count} created={created} kind={placementKind} source={sourceKind}:{sourceId} direction={direction} entrance={preferredEntranceId ?? ""}");
        return true;
    }

    public bool TryMovePlacement(
        WorldSiteState site,
        WorldSiteDefinition definition,
        string placementId,
        Vector2I cell,
        out string failureReason)
    {
        if (!TryValidatePlacementMove(site, definition, placementId, cell, out WorldSiteUnitPlacement placement, out failureReason))
        {
            return false;
        }

        placement.CellX = cell.X;
        placement.CellY = cell.Y;
        placement.ZoneId = ResolveZoneIdForCell(definition, cell, placement.ZoneId);
        GameLog.Info(nameof(WorldSiteDeploymentService), $"SiteUnitPlacementMoved site={site.SiteId} placement={placementId} cell={cell}");
        return true;
    }

    public bool CanMovePlacement(
        WorldSiteState site,
        WorldSiteDefinition definition,
        string placementId,
        Vector2I cell,
        out string failureReason)
    {
        return TryValidatePlacementMove(site, definition, placementId, cell, out _, out failureReason);
    }

    public string BuildGarrisonSummary(WorldSiteState site, WorldSiteDefinition definition)
    {
        int capacity = GetDefaultGarrisonCapacity(definition);
        int used = GetGarrisonSlotUsage(site);
        return capacity <= 0 ? $"{used}/未配置" : $"{used}/{capacity}";
    }

    private static int GetZoneCapacity(SiteDeploymentZoneDefinition zone)
    {
        if (zone == null)
        {
            return 0;
        }

        return zone.Capacity > 0 ? zone.Capacity : zone.Cells.Count;
    }

    private static HashSet<string> BuildExpectedPlacementIds(WorldSiteState site)
    {
        HashSet<string> ids = new();
        if (site == null)
        {
            return ids;
        }

        foreach (GarrisonState garrison in site.Garrison)
        {
            for (int index = 1; index <= garrison.Count; index++)
            {
                ids.Add(BuildGarrisonPlacementId(garrison.UnitTypeId, index));
            }
        }

        return ids;
    }

    private static Vector2I ResolveNextDefaultCell(WorldSiteState site, SiteDeploymentZoneDefinition zone)
    {
        if (zone?.Cells.Count > 0)
        {
            foreach (Vector2I cell in zone.Cells)
            {
                bool occupied = site.UnitPlacements.Any(placement => placement.CellX == cell.X && placement.CellY == cell.Y);
                if (!occupied)
                {
                    return cell;
                }
            }

            return zone.Cells[^1];
        }

        return Vector2I.Zero;
    }

    private static bool TryValidatePlacementMove(
        WorldSiteState site,
        WorldSiteDefinition definition,
        string placementId,
        Vector2I cell,
        out WorldSiteUnitPlacement placement,
        out string failureReason)
    {
        placement = null;
        failureReason = "";
        if (site == null || definition == null || string.IsNullOrWhiteSpace(placementId))
        {
            failureReason = "missing_site";
            return false;
        }

        placement = site.UnitPlacements.FirstOrDefault(item => item.PlacementId == placementId);
        if (placement == null)
        {
            failureReason = "missing_placement";
            return false;
        }

        string activePlacementId = placement.PlacementId;
        WorldSiteUnitPlacement occupied = site.UnitPlacements.FirstOrDefault(item =>
            item.PlacementId != activePlacementId &&
            item.CellX == cell.X &&
            item.CellY == cell.Y);
        if (occupied != null)
        {
            failureReason = "placement_cell_occupied";
            return false;
        }

        return true;
    }

    private static string ResolveZoneIdForCell(WorldSiteDefinition definition, Vector2I cell, string fallback)
    {
        SiteDeploymentZoneDefinition zone = definition.DeploymentZones.FirstOrDefault(item => item.Cells.Contains(cell));
        return string.IsNullOrWhiteSpace(zone?.ZoneId) ? fallback ?? "" : zone.ZoneId;
    }

    public static bool IsGarrisonPlacement(WorldSiteUnitPlacement placement)
    {
        return placement != null &&
               (placement.PlacementKind == WorldSiteUnitPlacementKind.Garrison ||
                placement.PlacementId.StartsWith("garrison:", System.StringComparison.OrdinalIgnoreCase));
    }

    private static string BuildGarrisonPlacementId(string unitTypeId, int index)
    {
        return $"garrison:{unitTypeId}:{index}";
    }

    private static void ApplyGarrisonMetadata(
        WorldSiteUnitPlacement placement,
        WorldSiteState site,
        SiteDeploymentZoneDefinition zone,
        string unitTypeId,
        int index)
    {
        placement.UnitTypeId = unitTypeId;
        placement.UnitIndex = index;
        placement.FactionId = site.OwnerFactionId;
        placement.PlacementKind = WorldSiteUnitPlacementKind.Garrison;
        placement.SourceKind = "Garrison";
        placement.SourceId = site.SiteId;
        placement.ArmyId = "";
        placement.ThreatId = "";
        placement.ZoneId = zone?.ZoneId ?? placement.ZoneId;
        placement.EntranceId = "";
        placement.AttackDirection = WorldSiteAttackDirection.Any;
    }

    private static void ApplyBattleMetadata(
        WorldSiteUnitPlacement placement,
        BattleForceRequest force,
        WorldSiteUnitPlacementKind placementKind,
        string sourceKind,
        string sourceId,
        string armyId,
        string threatId,
        string entranceId,
        WorldSiteAttackDirection direction,
        int index)
    {
        placement.UnitTypeId = force.UnitDefinitionId;
        placement.UnitIndex = index;
        placement.FactionId = force.FactionId ?? "";
        placement.PlacementKind = placementKind;
        placement.SourceKind = sourceKind;
        placement.SourceId = sourceId;
        placement.ArmyId = armyId;
        placement.ThreatId = threatId ?? "";
        placement.EntranceId = entranceId ?? "";
        placement.AttackDirection = direction;
    }

    private static bool TryResolveNextBattleCell(
        WorldSiteState site,
        IReadOnlyList<WorldSiteDeploymentCell> candidates,
        out WorldSiteDeploymentCell cell)
    {
        foreach (WorldSiteDeploymentCell candidate in candidates)
        {
            bool occupied = site.UnitPlacements.Any(placement =>
                placement.CellX == candidate.Cell.X &&
                placement.CellY == candidate.Cell.Y);
            if (!occupied)
            {
                cell = candidate;
                return true;
            }
        }

        cell = default;
        return false;
    }

    private static string ResolveForceSourceKind(BattleForceRequest force)
    {
        return string.IsNullOrWhiteSpace(force?.SourceKind) ? "BattleForce" : force.SourceKind;
    }

    private static string ResolveForceSourceId(BattleForceRequest force)
    {
        if (force == null)
        {
            return "";
        }

        if (!string.IsNullOrWhiteSpace(force.SourceId))
        {
            return force.SourceId;
        }

        if (!string.IsNullOrWhiteSpace(force.ForceId))
        {
            return force.ForceId;
        }

        return force.UnitDefinitionId ?? "";
    }

    private static string ResolveArmyId(string sourceKind, string sourceId)
    {
        return sourceKind switch
        {
            "PlayerArmy" or "EnemyArmy" or "ThreatArmy" => sourceId ?? "",
            _ => ""
        };
    }

    private static string BuildBattlePlacementId(string sourceKind, string sourceId, string unitTypeId, int index)
    {
        return $"battle:{sourceKind}:{sourceId}:{unitTypeId}:{index}";
    }

    private static string BuildSourceKey(string sourceKind, string sourceId)
    {
        if (string.IsNullOrWhiteSpace(sourceKind) || string.IsNullOrWhiteSpace(sourceId))
        {
            return "";
        }

        return $"{sourceKind}:{sourceId}";
    }
}
