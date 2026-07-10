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
    version: z.literal(1),
    projectId: z.string().min(1),
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
    locationType: z.enum(["city", "gate", "bridge", "ferry", "port", "ruin", "resource-site"]),
    detailMapId: z.string().optional(),
    referencePosition: coordinateSchema.optional(),
  }),
});

const ringSchema = z.array(coordinateSchema).min(4);
const polygonCoordinatesSchema = z.array(ringSchema).min(1);
const regionFeatureSchema = z.object({
  type: z.literal("Feature"),
  geometry: z.discriminatedUnion("type", [
    z.object({ type: z.literal("Polygon"), coordinates: polygonCoordinatesSchema }),
    z.object({ type: z.literal("MultiPolygon"), coordinates: z.array(polygonCoordinatesSchema).min(1) }),
  ]),
  properties: z.object({
    regionId: z.string().min(1),
    cityId: z.string().min(1),
    role: z.string().min(1),
    direction: z.string().min(1),
  }),
});

function featureCollectionSchema<T extends z.ZodType>(featureSchema: T) {
  return z.object({ type: z.literal("FeatureCollection"), features: z.array(featureSchema) });
}

export const geographyDocumentSchema = z.object({
  version: z.literal(1),
  linearFeatures: featureCollectionSchema(lineFeatureSchema),
  waterAnchors: featureCollectionSchema(waterAnchorFeatureSchema),
  strategicLocations: featureCollectionSchema(locationFeatureSchema),
  regions: featureCollectionSchema(regionFeatureSchema),
});
