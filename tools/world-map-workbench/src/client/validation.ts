import { centroid, featureCollection } from "@turf/turf";
import type { Feature, LineString, Point } from "geojson";
import { distancePointToLine, validateRegions } from "../shared/geo.js";
import type {
  GeographyDocument,
  LinearFeatureProperties,
  StrategicLocationProperties,
  ValidationItem,
  WorldProject,
} from "../shared/types.js";
import type { TerrainStore } from "./model/TerrainStore.js";

function pointCoordinate(feature: Feature<Point, StrategicLocationProperties>): [number, number] {
  return [feature.geometry.coordinates[0] ?? 0, feature.geometry.coordinates[1] ?? 0];
}

function nearInternalBoundary(value: number, size: number, worldSize: number, tolerance: number): boolean {
  if (value <= tolerance || value >= worldSize - tolerance) return false;
  const remainder = value % size;
  return remainder <= tolerance || size - remainder <= tolerance;
}

export function validateWorkbench(project: WorldProject, terrain: TerrainStore, geography: GeographyDocument): ValidationItem[] {
  const diagnostics = terrain.validateTerrain();
  diagnostics.push(...validateRegions(geography.regions));

  const locationCounts = new Map<string, number>();
  const anchorCounts = new Map<string, number>();
  const rivers = geography.linearFeatures.features.filter((feature) => feature.properties.featureType === "river");
  const mountains = geography.linearFeatures.features.filter((feature) => feature.properties.featureType === "mountain");
  for (const location of geography.strategicLocations.features) {
    const id = location.properties.locationId;
    locationCounts.set(id, (locationCounts.get(id) ?? 0) + 1);
    const coordinate = pointCoordinate(location);
    if (rivers.some((river) => distancePointToLine(coordinate, river) < 24)) {
      diagnostics.push({ code: "LOCATION_ON_WATER", severity: "error", message: `战略地点 ${id} 落在河流缓冲区`, objectId: id, coordinate, layerId: "strategic-locations" });
    }
    if (mountains.some((mountain) => distancePointToLine(coordinate, mountain) < 32)) {
      diagnostics.push({ code: "LOCATION_ON_MOUNTAIN", severity: "error", message: `战略地点 ${id} 落在山脉缓冲区`, objectId: id, coordinate, layerId: "strategic-locations" });
    }
    const reference = location.properties.referencePosition;
    if (reference) {
      const deviation = Math.hypot(coordinate[0] - reference[0], coordinate[1] - reference[1]);
      if (deviation > 64) diagnostics.push({ code: "LOCATION_REFERENCE_DEVIATION", severity: "warning", message: `${id} 与参考位置偏差 ${Math.round(deviation)}`, objectId: id, coordinate, layerId: "strategic-locations" });
    }
  }
  for (const [id, count] of locationCounts) {
    if (count > 1) diagnostics.push({ code: "LOCATION_DUPLICATE_ID", severity: "error", message: `LocationId 重复：${id}`, objectId: id, layerId: "strategic-locations" });
  }

  for (const anchor of geography.waterAnchors.features) {
    const id = anchor.properties.anchorId;
    anchorCounts.set(id, (anchorCounts.get(id) ?? 0) + 1);
  }
  for (const [id, count] of anchorCounts) {
    if (count > 1) diagnostics.push({ code: "WATER_ANCHOR_DUPLICATE_ID", severity: "error", message: `水系 AnchorId 重复：${id}`, objectId: id, layerId: "water" });
  }
  const anchorIds = new Set(geography.waterAnchors.features.map((anchor) => anchor.properties.anchorId));
  const riverIds = new Set(rivers.map((river) => river.properties.featureId));
  for (const river of rivers) {
    const { featureId, startAnchorId, endAnchorId, receiverId } = river.properties;
    if (startAnchorId && !anchorIds.has(startAnchorId)) diagnostics.push({ code: "RIVER_MISSING_START_ANCHOR", severity: "error", message: `${featureId} 的源头锚点不存在`, objectId: featureId, layerId: "water" });
    if (endAnchorId && !anchorIds.has(endAnchorId)) diagnostics.push({ code: "RIVER_MISSING_END_ANCHOR", severity: "error", message: `${featureId} 的终点锚点不存在`, objectId: featureId, layerId: "water" });
    if (receiverId && !riverIds.has(receiverId)) diagnostics.push({ code: "RIVER_MISSING_RECEIVER", severity: "error", message: `${featureId} 的汇入河流不存在`, objectId: featureId, layerId: "water" });
  }

  for (const line of geography.linearFeatures.features) {
    const endpoints = [line.geometry.coordinates[0], line.geometry.coordinates.at(-1)];
    for (const endpoint of endpoints) {
      if (!endpoint) continue;
      const coordinate: [number, number] = [endpoint[0] ?? 0, endpoint[1] ?? 0];
      if (
        nearInternalBoundary(coordinate[0], project.chunk.width, project.world.width, 2) ||
        nearInternalBoundary(coordinate[1], project.chunk.height, project.world.height, 2)
      ) {
        diagnostics.push({
          code: "LINE_BOUNDARY_ENDPOINT",
          severity: "warning",
          message: `${line.properties.featureId} 在 Chunk 边界无故终止`,
          objectId: line.properties.featureId,
          coordinate,
          layerId: line.properties.featureType === "river" ? "water" : line.properties.featureType === "road" ? "roads" : "mountains",
        });
      }
    }
  }

  return diagnostics;
}

export function diagnosticPoint(item: ValidationItem): Feature<Point, { code: string; severity: string; message: string }> | undefined {
  if (!item.coordinate) return undefined;
  return {
    type: "Feature",
    geometry: { type: "Point", coordinates: item.coordinate },
    properties: { code: item.code, severity: item.severity, message: item.message },
  };
}
