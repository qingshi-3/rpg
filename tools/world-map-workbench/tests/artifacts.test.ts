import fs from "node:fs/promises";
import os from "node:os";
import path from "node:path";
import { featureCollection, polygon } from "@turf/turf";
import sharp from "sharp";
import { afterEach, describe, expect, it } from "vitest";
import { compileRegionArtifacts, sampleNaturalTerritoryEdge, writeTerrainMask } from "../src/server/artifacts.js";

const temporaryDirectories: string[] = [];

afterEach(async () => {
  await Promise.all(temporaryDirectories.splice(0).map((directory) => fs.rm(directory, { recursive: true, force: true })));
});

describe("derived artifacts", () => {
  it("samples shared territory edges identically in reverse", () => {
    const forward = sampleNaturalTerritoryEdge([100, 200], [700, 260]);
    const reverse = sampleNaturalTerritoryEdge([700, 260], [100, 200]);

    expect(forward.length).toBeGreaterThan(5);
    expect(reverse).toEqual([...forward].reverse());
    expect(forward.some((point) => Math.abs((point[1] ?? 0) - 200 - ((point[0] ?? 0) - 100) * 0.1) > 1)).toBe(true);
  });

  it("writes an indexed terrain-id PNG without changing ids", async () => {
    const directory = await fs.mkdtemp(path.join(os.tmpdir(), "world-map-workbench-"));
    temporaryDirectories.push(directory);
    const output = path.join(directory, "terrain.png");
    const cells = Uint8Array.from([0, 1, 2, 3]);

    await writeTerrainMask(output, 2, 2, cells);
    const decoded = await sharp(output).extractChannel(0).raw().toBuffer({ resolveWithObject: true });

    expect(decoded.info.width).toBe(2);
    expect(decoded.info.height).toBe(2);
    expect([...decoded.data]).toEqual([0, 1, 2, 3]);
  });

  it("compiles territory mask, lookup, and outlines", async () => {
    const directory = await fs.mkdtemp(path.join(os.tmpdir(), "world-map-workbench-"));
    temporaryDirectories.push(directory);
    const regions = featureCollection([
      polygon(
        [
          [
            [0, 0],
            [64, 0],
            [64, 64],
            [0, 64],
            [0, 0],
          ],
        ],
        { regionId: "region_a", cityId: "city_a", role: "core", direction: "center" },
      ),
    ]);

    const outputs = await compileRegionArtifacts(directory, regions, { width: 64, height: 64, maskScale: 1 });

    await expect(fs.stat(outputs.maskPath)).resolves.toBeDefined();
    const lookup = JSON.parse(await fs.readFile(outputs.lookupPath, "utf8")) as { regions: Record<string, unknown> };
    expect(lookup.regions["1"]).toMatchObject({ regionId: "region_a", cityId: "city_a" });
    const outlines = JSON.parse(await fs.readFile(outputs.outlinesPath, "utf8")) as { regions: unknown[] };
    expect(outlines.regions).toHaveLength(1);
  });

  it("never leaks an earlier region id into a later region antialiased edge", async () => {
    const directory = await fs.mkdtemp(path.join(os.tmpdir(), "world-map-workbench-"));
    temporaryDirectories.push(directory);
    const regions = featureCollection([
      polygon([[[2, 2], [20, 2], [20, 20], [2, 20], [2, 2]]], { regionId: "player", cityId: "city_player", role: "core", direction: "west" }),
      polygon([[[32, 8], [57, 13], [54, 55], [29, 50], [32, 8]]], { regionId: "hostile", cityId: "city_hostile", role: "core", direction: "east" }),
    ]);

    const outputs = await compileRegionArtifacts(directory, regions, { width: 64, height: 64, maskScale: 1 });
    const decoded = await sharp(outputs.maskPath).extractChannel(0).raw().toBuffer({ resolveWithObject: true });
    const hostileAreaValues = new Set<number>();
    for (let y = 6; y < 58; y += 1) {
      for (let x = 27; x < 60; x += 1) hostileAreaValues.add(decoded.data[y * decoded.info.width + x] ?? 0);
    }

    expect([...hostileAreaValues].sort()).toEqual([0, 2]);
  });
});
