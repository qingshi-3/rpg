using System.Collections.Generic;
using System.Linq;
using Godot;
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
                if (site.UnitPlacements.Any(placement => placement.PlacementId == placementId))
                {
                    continue;
                }

                Vector2I cell = ResolveNextDefaultCell(site, zone);
                site.UnitPlacements.Add(new WorldSiteUnitPlacement
                {
                    PlacementId = placementId,
                    UnitTypeId = garrison.UnitTypeId,
                    UnitIndex = index,
                    ZoneId = zone.ZoneId,
                    CellX = cell.X,
                    CellY = cell.Y
                });
            }
        }
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

    private static bool IsGarrisonPlacement(WorldSiteUnitPlacement placement)
    {
        return placement != null && !string.IsNullOrWhiteSpace(placement.UnitTypeId);
    }

    private static string BuildGarrisonPlacementId(string unitTypeId, int index)
    {
        return $"garrison:{unitTypeId}:{index}";
    }
}
