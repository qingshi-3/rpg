import type { Feature, FeatureCollection, Geometry, LineString, MultiPolygon, Point, Polygon } from "geojson";

export const acceptedLayerIds = [
  "reference-map",
  "terrain",
  "water",
  "mountains",
  "roads",
  "strategic-locations",
  "territories",
  "final-chunk-art",
  "region-masks",
  "validation",
] as const;

export type LayerId = (typeof acceptedLayerIds)[number];
export type LayerKind = "reference" | "canonical" | "derived";
export type Coordinate = [number, number];

export interface LayerDefinition {
  id: LayerId;
  label: string;
  kind: LayerKind;
  visible: boolean;
  locked: boolean;
  opacity: number;
}

export interface TerrainType {
  id: number;
  key: string;
  label: string;
  color: string;
}

export interface ChunkDefinition {
  id: string;
  coordinate: [number, number];
  worldOrigin: Coordinate;
  visualTexturePath?: string;
  referenceTexturePath?: string;
  terrainMaskPath: string;
  territoryMaskPath: string;
  navigationScenePath?: string;
}

export interface WorldProject {
  version: 1;
  projectId: string;
  displayName: string;
  world: {
    width: number;
    height: number;
  };
  chunk: {
    width: number;
    height: number;
    terrainCellSize: number;
    territoryMaskScale: number;
  };
  layers: LayerDefinition[];
  terrainTypes: TerrainType[];
  chunks: ChunkDefinition[];
}

export type MapFeatureType = "river" | "road" | "mountain";

export interface LinearFeatureProperties {
  featureId: string;
  featureType: MapFeatureType;
  name?: string;
  widthClass?: number;
  roadClass?: number;
  density?: number;
  receiverId?: string;
  startAnchorId?: string;
  endAnchorId?: string;
}

export type LinearFeature = Feature<LineString, LinearFeatureProperties>;

export type WaterAnchorType = "source" | "lake" | "coast";

export interface WaterAnchorProperties {
  anchorId: string;
  name: string;
  anchorType: WaterAnchorType;
}

export type WaterAnchorFeature = Feature<Point, WaterAnchorProperties>;

export type StrategicLocationType = "city" | "gate" | "bridge" | "ferry" | "port" | "ruin" | "resource-site";

export interface StrategicLocationProperties {
  locationId: string;
  name: string;
  locationType: StrategicLocationType;
  detailMapId?: string;
  referencePosition?: Coordinate;
}

export type StrategicLocationFeature = Feature<Point, StrategicLocationProperties>;

export interface RegionProperties {
  regionId: string;
  cityId: string;
  role: string;
  direction: string;
}

export type RegionFeature = Feature<Polygon | MultiPolygon, RegionProperties>;

export interface GeographyDocument {
  version: 1;
  linearFeatures: FeatureCollection<LineString, LinearFeatureProperties>;
  waterAnchors: FeatureCollection<Point, WaterAnchorProperties>;
  strategicLocations: FeatureCollection<Point, StrategicLocationProperties>;
  regions: FeatureCollection<Polygon | MultiPolygon, RegionProperties>;
}

export type ValidationSeverity = "error" | "warning" | "info";

export interface ValidationItem {
  code: string;
  severity: ValidationSeverity;
  message: string;
  objectId?: string;
  coordinate?: Coordinate;
  layerId?: LayerId;
}

export interface TerrainChunkPayload {
  chunkId: string;
  width: number;
  height: number;
  cellsBase64: string;
}

export type AnyMapFeature = Feature<Geometry, Record<string, unknown>>;
