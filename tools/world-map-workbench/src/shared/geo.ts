import { bboxClip, centroid, featureCollection, intersect, point } from "@turf/turf";
import type { Feature, FeatureCollection, LineString, MultiLineString, MultiPolygon, Point, Polygon, Position } from "geojson";
import type { Coordinate, LinearFeatureProperties, LocationGeometryProperties, ValidationItem, WaterAnchorProperties, WorldProject } from "./types.js";

function closestPointOnSegment(pointCoordinate: Coordinate, start: Position, end: Position): { coordinate: Coordinate; distance: number } {
  const [px, py] = pointCoordinate;
  const [x1, y1] = [start[0] ?? 0, start[1] ?? 0];
  const [x2, y2] = [end[0] ?? 0, end[1] ?? 0];
  const dx = x2 - x1;
  const dy = y2 - y1;
  const lengthSquared = dx * dx + dy * dy;
  const t = lengthSquared === 0 ? 0 : Math.max(0, Math.min(1, ((px - x1) * dx + (py - y1) * dy) / lengthSquared));
  const coordinate: Coordinate = [x1 + t * dx, y1 + t * dy];
  return { coordinate, distance: Math.hypot(px - coordinate[0], py - coordinate[1]) };
}

export function distancePointToLine(pointCoordinate: Coordinate, line: Feature<LineString>): number {
  let best = Number.POSITIVE_INFINITY;
  for (let index = 1; index < line.geometry.coordinates.length; index += 1) {
    const start = line.geometry.coordinates[index - 1];
    const end = line.geometry.coordinates[index];
    if (!start || !end) continue;
    best = Math.min(best, closestPointOnSegment(pointCoordinate, start, end).distance);
  }
  return best;
}

export function snapRiverConfluence(
  candidate: Feature<LineString, LinearFeatureProperties>,
  rivers: Array<Feature<LineString, LinearFeatureProperties>>,
  tolerance: number,
): Feature<LineString, LinearFeatureProperties> {
  const coordinates = candidate.geometry.coordinates.map((coordinate) => [...coordinate] as Coordinate);
  const endpoint = coordinates.at(-1);
  if (!endpoint) {
    return candidate;
  }

  let best: { coordinate: Coordinate; distance: number; receiverId: string } | undefined;
  for (const river of rivers) {
    if (river.properties.featureId === candidate.properties.featureId) {
      continue;
    }
    for (let index = 1; index < river.geometry.coordinates.length; index += 1) {
      const segmentStart = river.geometry.coordinates[index - 1];
      const segmentEnd = river.geometry.coordinates[index];
      if (!segmentStart || !segmentEnd) {
        continue;
      }
      const result = closestPointOnSegment(endpoint, segmentStart, segmentEnd);
      if (result.distance <= tolerance && (!best || result.distance < best.distance)) {
        best = { ...result, receiverId: river.properties.featureId };
      }
    }
  }

  if (!best) {
    return candidate;
  }

  coordinates[coordinates.length - 1] = best.coordinate;
  return {
    ...candidate,
    geometry: { ...candidate.geometry, coordinates },
    properties: { ...candidate.properties, receiverId: best.receiverId },
  };
}

export function snapRiverEndpoints(
  candidate: Feature<LineString, LinearFeatureProperties>,
  rivers: Array<Feature<LineString, LinearFeatureProperties>>,
  anchors: Array<Feature<Point, WaterAnchorProperties>>,
  tolerance: number,
): Feature<LineString, LinearFeatureProperties> {
  let result: Feature<LineString, LinearFeatureProperties> = {
    ...candidate,
    geometry: { ...candidate.geometry, coordinates: candidate.geometry.coordinates.map((coordinate) => [...coordinate]) },
    properties: { ...candidate.properties },
  };
  // Relationships are derived from the current endpoints; stale links must not survive a control-point move.
  delete result.properties.receiverId;
  delete result.properties.startAnchorId;
  delete result.properties.endAnchorId;
  const coordinates = result.geometry.coordinates;
  const start = coordinates[0];
  const end = coordinates.at(-1);

  const nearestAnchor = (coordinate: Position | undefined) => {
    if (!coordinate) return undefined;
    return anchors
      .map((anchor) => ({
        anchor,
        distance: Math.hypot((coordinate[0] ?? 0) - (anchor.geometry.coordinates[0] ?? 0), (coordinate[1] ?? 0) - (anchor.geometry.coordinates[1] ?? 0)),
      }))
      .filter((entry) => entry.distance <= tolerance)
      .sort((left, right) => left.distance - right.distance)[0];
  };

  const startAnchor = nearestAnchor(start);
  if (startAnchor && start) {
    coordinates[0] = [...startAnchor.anchor.geometry.coordinates];
    result.properties.startAnchorId = startAnchor.anchor.properties.anchorId;
  }

  const endpointBeforeRiverSnap = end ? [...end] : undefined;
  result = snapRiverConfluence(result, rivers, tolerance);
  if (!result.properties.receiverId) {
    const endAnchor = nearestAnchor(endpointBeforeRiverSnap);
    if (endAnchor) {
      result.geometry.coordinates[result.geometry.coordinates.length - 1] = [...endAnchor.anchor.geometry.coordinates];
      result.properties.endAnchorId = endAnchor.anchor.properties.anchorId;
    }
  }
  return result;
}

export function clipLinearFeaturesToChunks(
  project: WorldProject,
  features: FeatureCollection<LineString, LinearFeatureProperties>,
): FeatureCollection<LineString | MultiLineString, LinearFeatureProperties & { sourceFeatureId: string; chunkId: string }> {
  // These pieces are display/export derivatives. The global line remains the only editable authority.
  const clips: Array<Feature<LineString | MultiLineString, LinearFeatureProperties & { sourceFeatureId: string; chunkId: string }>> = [];
  for (const chunk of project.chunks) {
    const bbox: [number, number, number, number] = [
      chunk.worldOrigin[0],
      chunk.worldOrigin[1],
      chunk.worldOrigin[0] + project.chunk.width,
      chunk.worldOrigin[1] + project.chunk.height,
    ];
    for (const feature of features.features) {
      try {
        const clipped = bboxClip(feature, bbox);
        if (clipped.geometry.type !== "LineString" && clipped.geometry.type !== "MultiLineString") continue;
        if (clipped.geometry.coordinates.length === 0) continue;
        clips.push({
          ...clipped,
          properties: { ...feature.properties, sourceFeatureId: feature.properties.featureId, chunkId: chunk.id },
        } as Feature<LineString | MultiLineString, LinearFeatureProperties & { sourceFeatureId: string; chunkId: string }>);
      } catch {
        // Invalid lines are reported separately and do not prevent other clip previews.
      }
    }
  }
  return featureCollection(clips);
}

function featureCoordinate(feature: Feature<Polygon | MultiPolygon, LocationGeometryProperties>): Coordinate | undefined {
  try {
    const center = centroid(feature);
    return [center.geometry.coordinates[0] ?? 0, center.geometry.coordinates[1] ?? 0];
  } catch {
    return undefined;
  }
}

export function validateLocationGeometries(geometries: FeatureCollection<Polygon | MultiPolygon, LocationGeometryProperties>): ValidationItem[] {
  const diagnostics: ValidationItem[] = [];
  const ids = new Map<string, number>();

  for (const geometry of geometries.features) {
    const locationId = geometry.properties.locationId;
    ids.set(locationId, (ids.get(locationId) ?? 0) + 1);
    const coordinate = featureCoordinate(geometry);
    if (!geometry.properties.provinceId) {
      diagnostics.push({ code: "LOCATION_GEOMETRY_ORPHAN", severity: "error", message: `城市区域 ${locationId} 没有 ProvinceId`, objectId: locationId, coordinate, layerId: "territories" });
    }
    if (geometry.geometry.type === "Polygon" && geometry.geometry.coordinates.length > 1) {
      diagnostics.push({ code: "LOCATION_GEOMETRY_HOLE", severity: "warning", message: `城市区域 ${locationId} 存在空洞`, objectId: locationId, coordinate, layerId: "territories" });
    }
  }

  for (const [locationId, count] of ids) {
    if (count > 1) {
      diagnostics.push({ code: "LOCATION_GEOMETRY_DUPLICATE_LOCATION", severity: "error", message: `LocationId 对应多份城市区域：${locationId}`, objectId: locationId, layerId: "territories" });
    }
  }

  for (let leftIndex = 0; leftIndex < geometries.features.length; leftIndex += 1) {
    const left = geometries.features[leftIndex];
    if (!left) continue;
    for (let rightIndex = leftIndex + 1; rightIndex < geometries.features.length; rightIndex += 1) {
      const right = geometries.features[rightIndex];
      if (!right) continue;
      try {
        const overlap = intersect(featureCollection([left, right]));
        if (!overlap) continue;
        const coordinate = featureCoordinate(left);
        diagnostics.push({
          code: "LOCATION_GEOMETRY_OVERLAP",
          severity: "error",
          message: `城市区域 ${left.properties.locationId} 与 ${right.properties.locationId} 重叠`,
          objectId: left.properties.locationId,
          coordinate,
          layerId: "territories",
        });
        if (left.properties.provinceId !== right.properties.provinceId) {
          diagnostics.push({
            code: "LOCATION_GEOMETRY_CROSS_PROVINCE",
            severity: "error",
            message: `城市区域 ${left.properties.locationId} 与其他省份 ${right.properties.provinceId} 冲突`,
            objectId: left.properties.locationId,
            coordinate,
            layerId: "territories",
          });
        }
      } catch {
        diagnostics.push({
          code: "LOCATION_GEOMETRY_INVALID",
          severity: "error",
          message: `城市区域 ${left.properties.locationId} 或 ${right.properties.locationId} 几何无效`,
          objectId: left.properties.locationId,
          layerId: "territories",
        });
      }
    }
  }

  return diagnostics;
}

export function gamePoint(coordinate: Coordinate) {
  return point(coordinate);
}
