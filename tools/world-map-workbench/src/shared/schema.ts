import { z } from "zod";
import { acceptedLayerIds } from "./types.js";

const coordinateSchema = z.tuple([z.number().finite(), z.number().finite()]);
const acceptedLayerIdSchema = z.enum(acceptedLayerIds);

const layerSchema = z.object({
  id: acceptedLayerIdSchema,
  label: z.string().min(1),
  kind: z.enum(["reference", "canonical", "derived"]),
  visible: z.boolean(),
  locked: z.boolean(),
  opacity: z.number().min(0).max(1),
});

const chunkSchema = z.object({
  id: z.string().min(1),
  coordinate: z.tuple([z.number().int().nonnegative(), z.number().int().nonnegative()]),
  worldOrigin: coordinateSchema,
  visualTexturePath: z.string().min(1).optional(),
  referenceTexturePath: z.string().min(1).optional(),
  terrainMaskPath: z.string().min(1),
  territoryMaskPath: z.string().min(1),
  navigationScenePath: z.string().min(1).optional(),
});

export const projectSchema = z
  .object({
    version: z.literal(2),
    mapId: z.string().regex(/^[a-z0-9][a-z0-9_-]{1,63}$/),
    displayName: z.string().min(1),
    world: z.object({
      width: z.number().int().positive(),
      height: z.number().int().positive(),
    }),
    chunk: z.object({
      width: z.number().int().positive(),
      height: z.number().int().positive(),
      terrainCellSize: z.number().int().positive(),
      territoryMaskScale: z.number().positive().max(1),
    }),
    layers: z.array(layerSchema).length(acceptedLayerIds.length),
    terrainTypes: z
      .array(
        z.object({
          id: z.number().int().min(1).max(255),
          key: z.string().min(1),
          label: z.string().min(1),
          color: z.string().regex(/^#[0-9a-fA-F]{6}$/),
        }),
      )
      .min(1),
    chunks: z.array(chunkSchema).min(1),
  })
  .superRefine((project, context) => {
    if (project.chunk.width % project.chunk.terrainCellSize !== 0 || project.chunk.height % project.chunk.terrainCellSize !== 0) {
      context.addIssue({ code: "custom", message: "Chunk dimensions must be divisible by terrain cell size", path: ["chunk"] });
    }

    const columns = project.world.width / project.chunk.width;
    const rows = project.world.height / project.chunk.height;
    if (!Number.isInteger(columns) || !Number.isInteger(rows) || project.chunks.length !== columns * rows) {
      context.addIssue({ code: "custom", message: "Chunk grid must exactly cover the map-level world dimensions", path: ["chunks"] });
    }

    const layerIds = new Set(project.layers.map((layer) => layer.id));
    for (const acceptedId of acceptedLayerIds) {
      if (!layerIds.has(acceptedId)) {
        context.addIssue({ code: "custom", message: `Missing accepted layer ${acceptedId}`, path: ["layers"] });
      }
    }

    const chunkIds = new Set<string>();
    const coordinates = new Set<string>();
    for (const [index, chunk] of project.chunks.entries()) {
      if (chunkIds.has(chunk.id)) {
        context.addIssue({ code: "custom", message: `Duplicate chunk id ${chunk.id}`, path: ["chunks", index, "id"] });
      }
      chunkIds.add(chunk.id);

      const coordinateKey = chunk.coordinate.join(",");
      if (coordinates.has(coordinateKey)) {
        context.addIssue({ code: "custom", message: `Duplicate chunk coordinate ${coordinateKey}`, path: ["chunks", index, "coordinate"] });
      }
      coordinates.add(coordinateKey);

      const expectedOrigin: [number, number] = [chunk.coordinate[0] * project.chunk.width, chunk.coordinate[1] * project.chunk.height];
      if (chunk.worldOrigin[0] !== expectedOrigin[0] || chunk.worldOrigin[1] !== expectedOrigin[1]) {
        context.addIssue({ code: "custom", message: `Chunk ${chunk.id} origin does not match its coordinate`, path: ["chunks", index, "worldOrigin"] });
      }
      if (chunk.coordinate[0] >= columns || chunk.coordinate[1] >= rows) {
        context.addIssue({ code: "custom", message: `Chunk ${chunk.id} coordinate falls outside the map-level grid`, path: ["chunks", index, "coordinate"] });
      }
    }

    const terrainIds = new Set<number>();
    for (const [index, terrain] of project.terrainTypes.entries()) {
      if (terrainIds.has(terrain.id)) {
        context.addIssue({ code: "custom", message: `Duplicate terrain id ${terrain.id}`, path: ["terrainTypes", index, "id"] });
      }
      terrainIds.add(terrain.id);
    }
  });

export type ProjectSchemaInput = z.input<typeof projectSchema>;

const lineFeatureSchema = z.object({
  type: z.literal("Feature"),
  geometry: z.object({ type: z.literal("LineString"), coordinates: z.array(coordinateSchema).min(2) }),
  properties: z.object({
    featureId: z.string().min(1),
    featureType: z.enum(["river", "road", "mountain"]),
    name: z.string().optional(),
    widthClass: z.number().int().min(1).max(8).optional(),
    roadClass: z.number().int().min(1).max(8).optional(),
    density: z.number().min(0).max(1).optional(),
    receiverId: z.string().min(1).optional(),
    startAnchorId: z.string().min(1).optional(),
    endAnchorId: z.string().min(1).optional(),
  }),
});

const waterAnchorFeatureSchema = z.object({
  type: z.literal("Feature"),
  geometry: z.object({ type: z.literal("Point"), coordinates: coordinateSchema }),
  properties: z.object({
    anchorId: z.string().min(1),
    name: z.string(),
    anchorType: z.enum(["source", "lake", "coast"]),
  }),
});

const locationFeatureSchema = z.object({
  type: z.literal("Feature"),
  geometry: z.object({ type: z.literal("Point"), coordinates: coordinateSchema }),
  properties: z.object({
    locationId: z.string().min(1),
    name: z.string(),
    locationType: z.enum(["main-city", "auxiliary-city", "gate", "bridge", "ferry", "port", "ruin", "resource-site"]),
    provinceId: z.string().min(1).optional(),
    referencePosition: coordinateSchema.optional(),
  }),
});

const ringSchema = z.array(coordinateSchema).min(4);
const polygonCoordinatesSchema = z.array(ringSchema).min(1);
const locationGeometryFeatureSchema = z.object({
  type: z.literal("Feature"),
  geometry: z.discriminatedUnion("type", [
    z.object({ type: z.literal("Polygon"), coordinates: polygonCoordinatesSchema }),
    z.object({ type: z.literal("MultiPolygon"), coordinates: z.array(polygonCoordinatesSchema).min(1) }),
  ]),
  properties: z.object({
    locationId: z.string().min(1),
    provinceId: z.string().min(1),
    direction: z.string().min(1),
  }),
});

function featureCollectionSchema<T extends z.ZodType>(featureSchema: T) {
  return z.object({ type: z.literal("FeatureCollection"), features: z.array(featureSchema) });
}

export const geographyDraftSchema = z.object({
  version: z.literal(3),
  provinces: z.array(z.object({
    provinceId: z.string().min(1),
    name: z.string().min(1),
    layoutId: z.string().min(1),
  })),
  linearFeatures: featureCollectionSchema(lineFeatureSchema),
  waterAnchors: featureCollectionSchema(waterAnchorFeatureSchema),
  strategicLocations: featureCollectionSchema(locationFeatureSchema),
  locationGeometries: featureCollectionSchema(locationGeometryFeatureSchema),
});

export const geographyDocumentSchema = geographyDraftSchema.superRefine((geography, context) => {
  const provinceIds = new Set<string>();
  for (const [index, province] of geography.provinces.entries()) {
    if (provinceIds.has(province.provinceId)) {
      context.addIssue({ code: "custom", message: `Duplicate province id ${province.provinceId}`, path: ["provinces", index, "provinceId"] });
    }
    provinceIds.add(province.provinceId);
  }

  const locationsById = new Map<string, (typeof geography.strategicLocations.features)[number]>();
  const mainCityCounts = new Map<string, number>();
  for (const [index, location] of geography.strategicLocations.features.entries()) {
    const { locationId, locationType, provinceId } = location.properties;
    if (locationsById.has(locationId)) {
      context.addIssue({ code: "custom", message: `Duplicate location id ${locationId}`, path: ["strategicLocations", "features", index, "properties", "locationId"] });
    }
    locationsById.set(locationId, location);
    const isCity = locationType === "main-city" || locationType === "auxiliary-city";
    if (isCity && (!provinceId || !provinceIds.has(provinceId))) {
      context.addIssue({ code: "custom", message: `City ${locationId} references an unknown province`, path: ["strategicLocations", "features", index, "properties", "provinceId"] });
    }
    if (locationType === "main-city" && provinceId) {
      mainCityCounts.set(provinceId, (mainCityCounts.get(provinceId) ?? 0) + 1);
    }
  }
  for (const [index, province] of geography.provinces.entries()) {
    if ((mainCityCounts.get(province.provinceId) ?? 0) !== 1) {
      context.addIssue({ code: "custom", message: `Province ${province.provinceId} must have exactly one main city`, path: ["provinces", index, "provinceId"] });
    }
  }

  const geometryCounts = new Map<string, number>();
  for (const [index, geometry] of geography.locationGeometries.features.entries()) {
    const { locationId, provinceId } = geometry.properties;
    geometryCounts.set(locationId, (geometryCounts.get(locationId) ?? 0) + 1);
    const location = locationsById.get(locationId);
    if (!location || (location.properties.locationType !== "main-city" && location.properties.locationType !== "auxiliary-city")) {
      context.addIssue({ code: "custom", message: `Location geometry ${locationId} must reference a city`, path: ["locationGeometries", "features", index, "properties", "locationId"] });
    } else if (location.properties.provinceId !== provinceId) {
      context.addIssue({ code: "custom", message: `Location geometry ${locationId} province does not match its city`, path: ["locationGeometries", "features", index, "properties", "provinceId"] });
    }
  }
  for (const [locationId, location] of locationsById) {
    if (location.properties.locationType !== "main-city" && location.properties.locationType !== "auxiliary-city") continue;
    if ((geometryCounts.get(locationId) ?? 0) !== 1) {
      context.addIssue({ code: "custom", message: `City ${locationId} must have exactly one location geometry`, path: ["locationGeometries"] });
    }
  }
});
