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

function ringPath(ring: Position[], scale: number): string {
  return ring
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
  const paths = regions.features.map((region, index) => {
    const maskId = index + 1;
    lookup.regions[String(maskId)] = { ...region.properties };
    return `<path d="${geometryPath(region.geometry, options.maskScale)}" fill="rgb(${maskId},${maskId},${maskId})" fill-rule="evenodd"/>`;
  });
  const svg = `<svg xmlns="http://www.w3.org/2000/svg" width="${width}" height="${height}" viewBox="0 0 ${width} ${height}"><rect width="100%" height="100%" fill="black"/>${paths.join("")}</svg>`;

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
  const png = await sharp(Buffer.from(svg)).greyscale().png({ compressionLevel: 9 }).toBuffer();
  await Promise.all([
    atomicWriteFile(maskPath, png),
    atomicWriteFile(lookupPath, `${JSON.stringify(lookup, null, 2)}\n`),
    atomicWriteFile(outlinesPath, `${JSON.stringify(outlines, null, 2)}\n`),
  ]);
  return { maskPath, lookupPath, outlinesPath };
}
