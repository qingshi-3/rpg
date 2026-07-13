import { centroid, featureCollection } from "@turf/turf";
import type { Feature, LineString, Point } from "geojson";
import { distancePointToLine, validateLocationGeometries } from "../shared/geo.js";
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
  diagnostics.push(...validateLocationGeometries(geography.locationGeometries));

  const locationCounts = new Map<string, number>();
  const provinceCounts = new Map<string, number>();
  const anchorCounts = new Map<string, number>();
  const rivers = geography.linearFeatures.features.filter((feature) => feature.properties.featureType === "river");
  const mountains = geography.linearFeatures.features.filter((feature) => feature.properties.featureType === "mountain");
  for (const province of geography.provinces) {
    provinceCounts.set(province.provinceId, (provinceCounts.get(province.provinceId) ?? 0) + 1);
    if (province.name === province.provinceId) {
      diagnostics.push({ code: "PROVINCE_PLACEHOLDER_NAME", severity: "warning", message: `省份 ${province.provinceId} 尚未设置显示名称`, objectId: province.provinceId, layerId: "territories" });
    }
  }
  for (const [provinceId, count] of provinceCounts) {
    if (count > 1) diagnostics.push({ code: "PROVINCE_DUPLICATE_ID", severity: "error", message: `ProvinceId 重复：${provinceId}`, objectId: provinceId, layerId: "territories" });
  }
  const provinceIds = new Set(geography.provinces.map((province) => province.provinceId));
  const cityLocations = geography.strategicLocations.features.filter((feature) =>
    feature.properties.locationType === "main-city" || feature.properties.locationType === "auxiliary-city");
  const locationById = new Map(geography.strategicLocations.features.map((feature) => [feature.properties.locationId, feature]));
  for (const location of geography.strategicLocations.features) {
    const id = location.properties.locationId;
    locationCounts.set(id, (locationCounts.get(id) ?? 0) + 1);
    const coordinate = pointCoordinate(location);
    const { locationType, provinceId } = location.properties;
    if ((locationType === "main-city" || locationType === "auxiliary-city") && location.properties.name === id) {
      diagnostics.push({ code: "CITY_PLACEHOLDER_NAME", severity: "warning", message: `城市 ${id} 尚未设置显示名称`, objectId: id, coordinate, layerId: "strategic-locations" });
    }
    if ((locationType === "main-city" || locationType === "auxiliary-city") && (!provinceId || !provinceIds.has(provinceId))) {
      diagnostics.push({ code: "LOCATION_UNKNOWN_PROVINCE", severity: "error", message: `城市 ${id} 的 ProvinceId 不存在：${provinceId ?? ""}`, objectId: id, coordinate, layerId: "strategic-locations" });
    }
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

  for (const province of geography.provinces) {
    const members = cityLocations.filter((location) => location.properties.provinceId === province.provinceId);
    const mainCount = members.filter((location) => location.properties.locationType === "main-city").length;
    if (mainCount !== 1) {
      diagnostics.push({ code: "PROVINCE_MAIN_CITY_COUNT", severity: "error", message: `省份 ${province.provinceId} 必须且只能有一个主城，当前为 ${mainCount}`, objectId: province.provinceId, layerId: "territories" });
    }
  }

  const geometryCounts = new Map<string, number>();
  for (const geometry of geography.locationGeometries.features) {
    const { locationId, provinceId } = geometry.properties;
    geometryCounts.set(locationId, (geometryCounts.get(locationId) ?? 0) + 1);
    const location = locationById.get(locationId);
    if (!location || (location.properties.locationType !== "main-city" && location.properties.locationType !== "auxiliary-city")) {
      const coordinate = centroid(geometry).geometry.coordinates;
      diagnostics.push({
        code: "LOCATION_GEOMETRY_UNKNOWN_CITY",
        severity: "error",
        message: `城市区域引用的主城/辅城不存在：${locationId}`,
        objectId: locationId,
        coordinate: [coordinate[0] ?? 0, coordinate[1] ?? 0],
        layerId: "territories",
      });
    } else if (location.properties.provinceId !== provinceId) {
      const coordinate = centroid(geometry).geometry.coordinates;
      diagnostics.push({
        code: "LOCATION_GEOMETRY_PROVINCE_MISMATCH",
        severity: "error",
        message: `城市区域 ${locationId} 的 ProvinceId 与城市定义不一致`,
        objectId: locationId,
        coordinate: [coordinate[0] ?? 0, coordinate[1] ?? 0],
        layerId: "territories",
      });
    }
  }
  for (const city of cityLocations) {
    const count = geometryCounts.get(city.properties.locationId) ?? 0;
    if (count !== 1) diagnostics.push({ code: "CITY_LOCATION_GEOMETRY_COUNT", severity: "error", message: `主城/辅城 ${city.properties.locationId} 必须且只能有一份视觉几何，当前为 ${count}`, objectId: city.properties.locationId, layerId: "territories" });
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
