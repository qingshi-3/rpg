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
  terrainMaskPath?: string;
  territoryMaskPath?: string;
  navigationScenePath?: string;
}

export interface ProvinceDefinition {
  provinceId: string;
  name: string;
  layoutId: string;
}

export interface WorldProject {
  version: 2;
  mapId: string;
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

export type StrategicLocationType = "main-city" | "auxiliary-city" | "gate" | "bridge" | "ferry" | "port" | "ruin" | "resource-site";

export interface StrategicLocationProperties {
  locationId: string;
  name: string;
  locationType: StrategicLocationType;
  provinceId?: string;
  referencePosition?: Coordinate;
}

export type StrategicLocationFeature = Feature<Point, StrategicLocationProperties>;

export interface LocationGeometryProperties {
  locationId: string;
  provinceId: string;
  direction: string;
}

export type LocationGeometryFeature = Feature<Polygon | MultiPolygon, LocationGeometryProperties>;

export interface GeographyDocument {
  version: 3;
  provinces: ProvinceDefinition[];
  linearFeatures: FeatureCollection<LineString, LinearFeatureProperties>;
  waterAnchors: FeatureCollection<Point, WaterAnchorProperties>;
  strategicLocations: FeatureCollection<Point, StrategicLocationProperties>;
  locationGeometries: FeatureCollection<Polygon | MultiPolygon, LocationGeometryProperties>;
}

export type PublishProfile = "visual-preview" | "region-interactive" | "strategic-runtime";

export type MapCatalogKind = "authoring" | "fixture";

export interface MapCatalogEntry {
  mapId: string;
  displayName: string;
  sourceRevision: number;
  kind: MapCatalogKind;
}

export interface MapCatalog {
  version: 2;
  defaultMapId?: string;
  maps: MapCatalogEntry[];
}

export interface PublishedMapPackage {
  schemaVersion: 2;
  mapId: string;
  revision: string;
  compatibilityRevision: number;
  contentHash: string;
  publishProfile: PublishProfile;
  capabilities: string[];
  chunkManifestPath: string;
  geographyPath: string;
  artifactHashes: Array<{
    kind: "chunk-manifest" | "geography" | "region-lookup" | "region-outlines" | "visual" | "region-mask" | "terrain" | "navigation";
    artifactId: string;
    sha256: string;
  }>;
  regionArtifacts?: {
    encoding: "rgb24-location-code-v1";
    lookupPath: string;
    outlinesPath: string;
    chunks: Array<{
      chunkId: string;
      worldOrigin: Coordinate;
      worldWidth: number;
      worldHeight: number;
      maskTexturePath: string;
    }>;
  };
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
