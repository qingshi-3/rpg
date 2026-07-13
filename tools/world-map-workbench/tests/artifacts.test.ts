import fs from "node:fs/promises";
import os from "node:os";
import path from "node:path";
import { featureCollection, polygon } from "@turf/turf";
import sharp from "sharp";
import { afterEach, describe, expect, it } from "vitest";
import { compileLocationGeometryArtifacts, sampleNaturalTerritoryEdge, writeTerrainMask } from "../src/server/artifacts.js";
import type { GeographyDocument, LocationGeometryFeature } from "../src/shared/types.js";

const temporaryDirectories: string[] = [];

afterEach(async () => {
  await Promise.all(temporaryDirectories.splice(0).map((directory) => fs.rm(directory, { recursive: true, force: true })));
});

async function temporaryDirectory(): Promise<string> {
  const directory = await fs.mkdtemp(path.join(os.tmpdir(), "world-map-workbench-"));
  temporaryDirectories.push(directory);
  return directory;
}

describe("derived artifacts", () => {
  it("preserves authored territory edges exactly", () => {
    expect(sampleNaturalTerritoryEdge([100, 200], [700, 260])).toEqual([[100, 200], [700, 260]]);
    expect(sampleNaturalTerritoryEdge([700, 260], [100, 200])).toEqual([[700, 260], [100, 200]]);
  });

  it("writes an indexed terrain-id PNG without changing ids", async () => {
    const output = path.join(await temporaryDirectory(), "terrain.png");
    const cells = Uint8Array.from([0, 1, 2, 3]);

    await writeTerrainMask(output, 2, 2, cells);
    const decoded = await sharp(output).extractChannel(0).raw().toBuffer({ resolveWithObject: true });

    expect(decoded.info.width).toBe(2);
    expect(decoded.info.height).toBe(2);
    expect([...decoded.data]).toEqual([0, 1, 2, 3]);
  });

  it("writes deterministic exact RGB24 masks and stable lookup identities", async () => {
    const directory = await temporaryDirectory();
    const geometries = featureCollection([
      polygon([[[0, 0], [8, 0], [8, 8], [0, 8], [0, 0]]], { locationId: "city_a", provinceId: "province_a", direction: "center" }),
    ]) as GeographyDocument["locationGeometries"];

    const first = await compileLocationGeometryArtifacts(path.join(directory, "first"), geometries, { width: 8, height: 8, maskScale: 1 });
    const second = await compileLocationGeometryArtifacts(path.join(directory, "second"), geometries, { width: 8, height: 8, maskScale: 1 });
    expect(await fs.readFile(first.maskPath)).toEqual(await fs.readFile(second.maskPath));
    expect(await fs.readFile(first.lookupPath, "utf8")).toBe(await fs.readFile(second.lookupPath, "utf8"));
    const lookup = JSON.parse(await fs.readFile(first.lookupPath, "utf8")) as { version: number; encoding: string; locations: Record<string, unknown> };
    expect(lookup).toMatchObject({ version: 3, encoding: "rgb24-location-code-v1" });
    expect(lookup.locations["1"]).toMatchObject({ locationId: "city_a", provinceId: "province_a" });
  });

  it("emits chunk-aligned masks", async () => {
    const geometries = featureCollection([
      polygon([[[0, 0], [10, 0], [10, 10], [0, 10], [0, 0]]], { locationId: "city_west", provinceId: "province_west", direction: "west" }),
      polygon([[[10, 0], [20, 0], [20, 10], [10, 10], [10, 0]]], { locationId: "city_east", provinceId: "province_east", direction: "east" }),
    ]) as GeographyDocument["locationGeometries"];
    const outputs = await compileLocationGeometryArtifacts(await temporaryDirectory(), geometries, {
      width: 20,
      height: 10,
      maskScale: 1,
      chunks: [
        { id: "west", worldOrigin: [0, 0], width: 10, height: 10 },
        { id: "east", worldOrigin: [10, 0], width: 10, height: 10 },
      ],
    });

    expect(outputs.maskPaths).toHaveLength(2);
    await expect(sharp(outputs.maskPaths[0]!).metadata()).resolves.toMatchObject({ width: 10, height: 10 });
    await expect(sharp(outputs.maskPaths[1]!).metadata()).resolves.toMatchObject({ width: 10, height: 10 });
  });

  it("encodes region codes above 255 without truncating identity", async () => {
    const features: LocationGeometryFeature[] = [];
    for (let index = 0; index < 300; index += 1) {
      const x = index * 2;
      features.push(polygon([[[x, 0], [x + 1, 0], [x + 1, 1], [x, 1], [x, 0]]], {
        locationId: `city_${index + 1}`,
        provinceId: `province_${index + 1}`,
        direction: "center",
      }) as LocationGeometryFeature);
    }
    const outputs = await compileLocationGeometryArtifacts(await temporaryDirectory(), featureCollection(features), { width: 600, height: 1, maskScale: 1 });
    const decoded = await sharp(outputs.maskPath).raw().toBuffer({ resolveWithObject: true });
    const offset = 510 * decoded.info.channels;
    const code = (decoded.data[offset] ?? 0) | ((decoded.data[offset + 1] ?? 0) << 8) | ((decoded.data[offset + 2] ?? 0) << 16);
    expect(code).toBe(256);
    const lookup = JSON.parse(await fs.readFile(outputs.lookupPath, "utf8")) as { locations: Record<string, { locationId: string }> };
    expect(lookup.locations["256"]?.locationId).toBe("city_256");
  });

  it("blocks overlapping categorical regions", async () => {
    const geometries = featureCollection([
      polygon([[[0, 0], [8, 0], [8, 8], [0, 8], [0, 0]]], { locationId: "city_a", provinceId: "province_a", direction: "center" }),
      polygon([[[4, 0], [12, 0], [12, 8], [4, 8], [4, 0]]], { locationId: "city_b", provinceId: "province_b", direction: "center" }),
    ]) as GeographyDocument["locationGeometries"];
    await expect(compileLocationGeometryArtifacts(await temporaryDirectory(), geometries, { width: 12, height: 8, maskScale: 1 })).rejects.toThrow("overlap blocks publish");
  });
});
