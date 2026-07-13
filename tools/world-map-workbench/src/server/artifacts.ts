import fs from "node:fs/promises";
import path from "node:path";
import { featureCollection, union } from "@turf/turf";
import type { FeatureCollection, MultiPolygon, Polygon, Position } from "geojson";
import sharp from "sharp";
import type { LocationGeometryProperties } from "../shared/types.js";

async function atomicWriteFile(outputPath: string, contents: string | Uint8Array): Promise<void> {
  await fs.mkdir(path.dirname(outputPath), { recursive: true });
  const temporaryPath = `${outputPath}.${process.pid}.${Date.now()}.tmp`;
  await fs.writeFile(temporaryPath, contents);
  await fs.rename(temporaryPath, outputPath);
}

export async function writeTerrainMask(outputPath: string, width: number, height: number, cells: Uint8Array): Promise<void> {
  if (width <= 0 || height <= 0 || cells.length !== width * height) {
    throw new Error(`Terrain mask dimensions do not match cell count width=${width} height=${height} cells=${cells.length}`);
  }
  // Encode grayscale values so the first decoded channel always remains the exact terrain id.
  const png = await sharp(cells, { raw: { width, height, channels: 1 } })
    .toColourspace("b-w")
    .png({ compressionLevel: 9 })
    .toBuffer();
  await atomicWriteFile(outputPath, png);
}

export function sampleNaturalTerritoryEdge(start: Position, end: Position): Position[] {
  return [[start[0] ?? 0, start[1] ?? 0], [end[0] ?? 0, end[1] ?? 0]];
}

function ringPath(ring: Position[], scale: number): string {
  const openRing = ring.length > 1 && ring[0]?.[0] === ring[ring.length - 1]?.[0] && ring[0]?.[1] === ring[ring.length - 1]?.[1]
    ? ring.slice(0, -1)
    : ring;
  return openRing
    .map((coordinate, index) => `${index === 0 ? "M" : "L"}${(coordinate[0] ?? 0) * scale} ${(coordinate[1] ?? 0) * scale}`)
    .join(" ") + " Z";
}

function geometryPath(geometry: Polygon | MultiPolygon, scale: number): string {
  const polygons = geometry.type === "Polygon" ? [geometry.coordinates] : geometry.coordinates;
  return polygons.flatMap((polygonCoordinates) => polygonCoordinates.map((ring) => ringPath(ring, scale))).join(" ");
}

export async function compileLocationGeometryArtifacts(
  outputDirectory: string,
  locationGeometries: FeatureCollection<Polygon | MultiPolygon, LocationGeometryProperties>,
  options: {
    width: number;
    height: number;
    maskScale: number;
    chunks?: Array<{ id: string; worldOrigin: [number, number]; width: number; height: number }>;
  },
): Promise<{ maskPath: string; maskPaths: string[]; lookupPath: string; outlinesPath: string }> {
  if (locationGeometries.features.length > 16_777_215) {
    throw new Error("Region mask exceeds rgb24-location-code-v1 capacity");
  }
  const width = Math.max(1, Math.ceil(options.width * options.maskScale));
  const height = Math.max(1, Math.ceil(options.height * options.maskScale));
  const lookup: { version: 3; encoding: "rgb24-location-code-v1"; locations: Record<string, { locationId: string; provinceId: string }> } = {
    version: 3,
    encoding: "rgb24-location-code-v1",
    locations: {},
  };
  const geometryPaths = locationGeometries.features.map((geometry, index) => {
    const maskId = index + 1;
    lookup.locations[String(maskId)] = {
      locationId: geometry.properties.locationId,
      provinceId: geometry.properties.provinceId,
    };
    return { maskId, path: geometryPath(geometry.geometry, options.maskScale) };
  });

  const provinceGroups = new Map<string, Array<(typeof locationGeometries.features)[number]>>();
  for (const geometry of locationGeometries.features) {
    const group = provinceGroups.get(geometry.properties.provinceId) ?? [];
    group.push(geometry);
    provinceGroups.set(geometry.properties.provinceId, group);
  }
  const provinces = [...provinceGroups.entries()].map(([provinceId, provinceGeometries]) => {
    const outline = provinceGeometries.length === 1 ? provinceGeometries[0] : union(featureCollection(provinceGeometries));
    if (!outline?.geometry) throw new Error(`Province geometry union failed provinceId=${provinceId}`);
    return { provinceId, geometry: outline.geometry };
  });
  const outlines = { version: 2, provinces, locationGeometries: locationGeometries.features };

  const maskPath = path.join(outputDirectory, "territory_mask.png");
  const lookupPath = path.join(outputDirectory, "region_lookup.json");
  const outlinesPath = path.join(outputDirectory, "region_outlines.json");
  // Mask ids are derived categorical values. Rasterize one binary coverage mask at a time so
  // antialiasing cannot create a value that resolves to another LocationId.
  const categoricalMask = new Uint32Array(width * height);
  for (const geometry of geometryPaths) {
    const svg = `<svg xmlns="http://www.w3.org/2000/svg" width="${width}" height="${height}" viewBox="0 0 ${width} ${height}" shape-rendering="crispEdges"><rect width="100%" height="100%" fill="black"/><path d="${geometry.path}" fill="white" fill-rule="evenodd" shape-rendering="crispEdges"/></svg>`;
    const coverage = await sharp(Buffer.from(svg)).greyscale().raw().toBuffer();
    for (let pixel = 0; pixel < coverage.length; pixel += 1) {
      if ((coverage[pixel] ?? 0) < 128) continue;
      if (categoricalMask[pixel] !== 0 && categoricalMask[pixel] !== geometry.maskId) {
        throw new Error(`Region geometry overlap blocks publish maskId=${geometry.maskId} existing=${categoricalMask[pixel]}`);
      }
      categoricalMask[pixel] = geometry.maskId;
    }
  }
  const rgb = Buffer.alloc(width * height * 3);
  for (let pixel = 0; pixel < categoricalMask.length; pixel += 1) {
    const code = categoricalMask[pixel] ?? 0;
    rgb[pixel * 3] = code & 0xff;
    rgb[pixel * 3 + 1] = (code >>> 8) & 0xff;
    rgb[pixel * 3 + 2] = (code >>> 16) & 0xff;
  }
  const fullMask = sharp(rgb, { raw: { width, height, channels: 3 } });
  const chunkContracts = options.chunks ?? [{ id: "territory_mask", worldOrigin: [0, 0] as [number, number], width: options.width, height: options.height }];
  const maskPaths: string[] = [];
  if (options.chunks) {
    // The full draft image is preview-only; published packages reference only chunk-aligned categorical masks.
    await atomicWriteFile(maskPath, await fullMask.clone().png({ compressionLevel: 9, palette: false }).toBuffer());
  }
  for (const chunk of chunkContracts) {
    const left = Math.round(chunk.worldOrigin[0] * options.maskScale);
    const top = Math.round(chunk.worldOrigin[1] * options.maskScale);
    const chunkWidth = Math.round(chunk.width * options.maskScale);
    const chunkHeight = Math.round(chunk.height * options.maskScale);
    const outputPath = chunkContracts.length === 1 && chunk.id === "territory_mask"
      ? maskPath
      : path.join(outputDirectory, "chunks", `${chunk.id}.png`);
    const png = await fullMask.clone().extract({ left, top, width: chunkWidth, height: chunkHeight })
      .png({ compressionLevel: 9, palette: false }).toBuffer();
    await atomicWriteFile(outputPath, png);
    maskPaths.push(outputPath);
  }
  await Promise.all([
    atomicWriteFile(lookupPath, `${JSON.stringify(lookup, null, 2)}\n`),
    atomicWriteFile(outlinesPath, `${JSON.stringify(outlines, null, 2)}\n`),
  ]);
  return { maskPath: maskPaths[0]!, maskPaths, lookupPath, outlinesPath };
}
