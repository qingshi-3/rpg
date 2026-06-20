using System;
using System.Collections.Generic;
using System.Linq;
using Rpg.Application.Maps;
using Rpg.Application.StrategicManagement;
using Rpg.Definitions.Maps;
using Rpg.Definitions.StrategicManagement;
using Rpg.Domain.StrategicManagement;

namespace Rpg.Presentation.World.Sites;

internal sealed class StrategicBuildingPlacementResolver
{
    public bool TryResolveStrategicBuildingPlacement(
        StrategicManagementDefinitionSet definitions,
        StrategicManagementState state,
        StrategicManagementRules rules,
        string cityId,
        string buildingDefinitionId,
        IReadOnlyList<SemanticMapMarkerData> constructionRegionMarkers,
        int gridX,
        int gridY,
        out string constructionRegionId,
        out string failureReason)
    {
        constructionRegionId = "";
        failureReason = "";

        if (definitions == null ||
            state == null ||
            rules == null ||
            !definitions.Buildings.TryGetValue(buildingDefinitionId ?? "", out StrategicBuildingDefinition building))
        {
            failureReason = StrategicFailureReasons.MissingBuilding;
            return false;
        }

        if (!state.Cities.TryGetValue(cityId ?? "", out StrategicCityState city) ||
            !definitions.Locations.TryGetValue(city.LocationId, out StrategicLocationDefinition location))
        {
            failureReason = StrategicFailureReasons.MissingCity;
            return false;
        }

        List<SemanticMapMarkerData> candidateMarkers = (constructionRegionMarkers ?? Array.Empty<SemanticMapMarkerData>())
            .Where(marker => marker != null &&
                             marker.MarkerType == SemanticMapMarkerType.ConstructionRegion &&
                             city.ConstructionRegionIds.Contains(marker.MarkerId))
            .OrderBy(marker => marker.Priority)
            .ThenBy(marker => marker.MarkerId, StringComparer.Ordinal)
            .ToList();
        if (candidateMarkers.Count == 0)
        {
            failureReason = StrategicFailureReasons.MissingConstructionRegion;
            return false;
        }

        IReadOnlyList<(int X, int Y)> footprintCells = BuildFootprintCells(
            gridX,
            gridY,
            building.FootprintWidth,
            building.FootprintHeight);
        string firstFailure = "";
        foreach (SemanticMapMarkerData marker in candidateMarkers)
        {
            if (!IsFootprintInsideMarker(footprintCells, marker))
            {
                firstFailure = string.IsNullOrWhiteSpace(firstFailure)
                    ? StrategicFailureReasons.BuildingPlacementOutOfBounds
                    : firstFailure;
                continue;
            }

            // Marker geometry selects the presentation region. Strategic rules remain
            // the authority for category, overlap, bounds, and resource legality.
            string candidateFailure = rules.GetBuildingPlacementFailureReason(
                state,
                city.LocationId,
                building.BuildingDefinitionId,
                marker.MarkerId,
                gridX,
                gridY);
            if (string.IsNullOrWhiteSpace(candidateFailure))
            {
                constructionRegionId = marker.MarkerId;
                return true;
            }

            firstFailure = string.IsNullOrWhiteSpace(firstFailure)
                ? candidateFailure
                : firstFailure;
        }

        failureReason = string.IsNullOrWhiteSpace(firstFailure)
            ? StrategicFailureReasons.MissingConstructionRegion
            : firstFailure;
        return false;
    }

    private static IReadOnlyList<(int X, int Y)> BuildFootprintCells(int gridX, int gridY, int width, int height)
    {
        List<(int X, int Y)> cells = new();
        for (int y = 0; y < Math.Max(0, height); y++)
        {
            for (int x = 0; x < Math.Max(0, width); x++)
            {
                cells.Add((gridX + x, gridY + y));
            }
        }

        return cells;
    }

    private static bool IsFootprintInsideMarker(
        IReadOnlyList<(int X, int Y)> footprintCells,
        SemanticMapMarkerData marker)
    {
        if (footprintCells == null || footprintCells.Count == 0 || marker == null)
        {
            return false;
        }

        HashSet<(int X, int Y)> markerCells = marker.CoveredCells
            .Select(cell => (cell.X, cell.Y))
            .ToHashSet();
        return footprintCells.All(markerCells.Contains);
    }
}
