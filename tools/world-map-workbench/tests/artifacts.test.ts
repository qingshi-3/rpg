import fs from "node:fs/promises";
import os from "node:os";
import path from "node:path";
import { featureCollection, polygon } from "@turf/turf";
import sharp from "sharp";
import { afterEach, describe, expect, it } from "vitest";
import { compileRegionArtifacts, writeTerrainMask } from "../src/server/artifacts.js";

const temporaryDirectories: string[] = [];

afterEach(async () => {
  await Promise.all(temporaryDirectories.splice(0).map((directory) => fs.rm(directory, { recursive: true, force: true })));
});

describe("derived artifacts", () => {
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
});
