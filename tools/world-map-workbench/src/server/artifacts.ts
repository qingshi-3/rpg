import fs from "node:fs/promises";
import path from "node:path";
import { featureCollection, union } from "@turf/turf";
import type { FeatureCollection, MultiPolygon, Polygon, Position } from "geojson";
import sharp from "sharp";
import type { RegionProperties } from "../shared/types.js";

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

function compareCoordinates(left: Position, right: Position): number {
  const x = (left[0] ?? 0) - (right[0] ?? 0);
  return x !== 0 ? x : (left[1] ?? 0) - (right[1] ?? 0);
}

function hashEdge(start: Position, end: Position): number {
  const text = `${start[0] ?? 0},${start[1] ?? 0}:${end[0] ?? 0},${end[1] ?? 0}`;
  let hash = 2166136261;
  for (let index = 0; index < text.length; index += 1) {
    hash ^= text.charCodeAt(index);
    hash = Math.imul(hash, 16777619);
  }
  return hash >>> 0;
}

export function sampleNaturalTerritoryEdge(start: Position, end: Position): Position[] {
  const reversed = compareCoordinates(start, end) > 0;
  const canonicalStart = reversed ? end : start;
  const canonicalEnd = reversed ? start : end;
  const startX = canonicalStart[0] ?? 0;
  const startY = canonicalStart[1] ?? 0;
  const deltaX = (canonicalEnd[0] ?? 0) - startX;
  const deltaY = (canonicalEnd[1] ?? 0) - startY;
  const length = Math.hypot(deltaX, deltaY);
  if (length === 0) return [[startX, startY]];

  const intervals = Math.max(5, Math.ceil(length / 72));
  const normalX = -deltaY / length;
  const normalY = deltaX / length;
  const seed = hashEdge(canonicalStart, canonicalEnd);
  const phase = (seed & 0xffff) / 0xffff * Math.PI * 2;
  const secondaryPhase = ((seed >>> 16) & 0xffff) / 0xffff * Math.PI * 2;
  const amplitude = Math.min(38, length * 0.06);
  const points: Position[] = [];
  for (let index = 0; index <= intervals; index += 1) {
    const t = index / intervals;
    const envelope = Math.sin(Math.PI * t);
    const wave = Math.sin(Math.PI * 2 * t + phase) + Math.sin(Math.PI * 4.4 * t + secondaryPhase) * 0.28;
    const offset = amplitude * envelope * wave;
    points.push([startX + deltaX * t + normalX * offset, startY + deltaY * t + normalY * offset]);
  }
  points[0] = [startX, startY];
  points[points.length - 1] = [canonicalEnd[0] ?? 0, canonicalEnd[1] ?? 0];
  return reversed ? points.reverse() : points;
}

function ringPath(ring: Position[], scale: number): string {
  const openRing = ring.length > 1 && ring[0]?.[0] === ring[ring.length - 1]?.[0] && ring[0]?.[1] === ring[ring.length - 1]?.[1]
    ? ring.slice(0, -1)
    : ring;
  const sampled: Position[] = [];
  for (let index = 0; index < openRing.length; index += 1) {
    const edge = sampleNaturalTerritoryEdge(openRing[index]!, openRing[(index + 1) % openRing.length]!);
    sampled.push(...edge.slice(0, -1));
  }
  return sampled
    .map((coordinate, index) => `${index === 0 ? "M" : "L"}${(coordinate[0] ?? 0) * scale} ${(coordinate[1] ?? 0) * scale}`)
    .join(" ") + " Z";
}

function geometryPath(geometry: Polygon | MultiPolygon, scale: number): string {
  const polygons = geometry.type === "Polygon" ? [geometry.coordinates] : geometry.coordinates;
  return polygons.flatMap((polygonCoordinates) => polygonCoordinates.map((ring) => ringPath(ring, scale))).join(" ");
}

export async function compileRegionArtifacts(
  outputDirectory: string,
  regions: FeatureCollection<Polygon | MultiPolygon, RegionProperties>,
  options: { width: number; height: number; maskScale: number },
): Promise<{ maskPath: string; lookupPath: string; outlinesPath: string }> {
  if (regions.features.length > 255) {
    throw new Error("Territory mask supports at most 255 regions in the first implementation");
  }
  const width = Math.max(1, Math.ceil(options.width * options.maskScale));
  const height = Math.max(1, Math.ceil(options.height * options.maskScale));
  const lookup: { version: 1; regions: Record<string, RegionProperties> } = { version: 1, regions: {} };
  const regionPaths = regions.features.map((region, index) => {
    const maskId = index + 1;
    lookup.regions[String(maskId)] = { ...region.properties };
    return { maskId, path: geometryPath(region.geometry, options.maskScale) };
  });

  const cityGroups = new Map<string, Array<(typeof regions.features)[number]>>();
  for (const region of regions.features) {
    const group = cityGroups.get(region.properties.cityId) ?? [];
    group.push(region);
    cityGroups.set(region.properties.cityId, group);
  }
  const cities = [...cityGroups.entries()].map(([cityId, cityRegions]) => {
    try {
      const outline = cityRegions.length === 1 ? cityRegions[0] : union(featureCollection(cityRegions));
      return { cityId, geometry: outline?.geometry ?? cityRegions[0]?.geometry };
    } catch {
      return { cityId, geometry: cityRegions[0]?.geometry, unionFailed: true };
    }
  });
  const outlines = { version: 1, cities, regions: regions.features };

  const maskPath = path.join(outputDirectory, "territory_mask.png");
  const lookupPath = path.join(outputDirectory, "region_lookup.json");
  const outlinesPath = path.join(outputDirectory, "region_outlines.json");
  // Province/region ids are categorical data. Rasterizing integer colors in one antialiased SVG
  // creates intermediate values (for example hostile id 8 at 25% coverage becomes player id 2).
  // Rasterize one binary coverage mask at a time, threshold it, then write the exact owning id.
  const categoricalMask = new Uint8Array(width * height);
  for (const region of regionPaths) {
    const svg = `<svg xmlns="http://www.w3.org/2000/svg" width="${width}" height="${height}" viewBox="0 0 ${width} ${height}" shape-rendering="crispEdges"><rect width="100%" height="100%" fill="black"/><path d="${region.path}" fill="white" fill-rule="evenodd" shape-rendering="crispEdges"/></svg>`;
    const coverage = await sharp(Buffer.from(svg)).greyscale().raw().toBuffer();
    for (let pixel = 0; pixel < coverage.length; pixel += 1) {
      if ((coverage[pixel] ?? 0) >= 128) categoricalMask[pixel] = region.maskId;
    }
  }
  const png = await sharp(categoricalMask, { raw: { width, height, channels: 1 } })
    .toColourspace("b-w")
    .png({ compressionLevel: 9 })
    .toBuffer();
  await Promise.all([
    atomicWriteFile(maskPath, png),
    atomicWriteFile(lookupPath, `${JSON.stringify(lookup, null, 2)}\n`),
    atomicWriteFile(outlinesPath, `${JSON.stringify(outlines, null, 2)}\n`),
  ]);
  return { maskPath, lookupPath, outlinesPath };
}
