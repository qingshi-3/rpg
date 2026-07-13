import fs from "node:fs/promises";
import os from "node:os";
import path from "node:path";
import type { AddressInfo } from "node:net";
import { afterEach, describe, expect, it } from "vitest";
import { point, polygon } from "@turf/turf";
import { createApp } from "../src/server/app.js";
import type { GeographyDocument, WorldProject } from "../src/shared/types.js";

const temporaryDirectories: string[] = [];
const closeServers: Array<() => Promise<void>> = [];

afterEach(async () => {
  await Promise.all(closeServers.splice(0).map((close) => close()));
  await Promise.all(temporaryDirectories.splice(0).map((directory) => fs.rm(directory, { recursive: true, force: true })));
});

async function startApi(): Promise<{ projectRoot: string; baseUrl: string }> {
  const projectRoot = await fs.mkdtemp(path.join(os.tmpdir(), "world-map-api-"));
  temporaryDirectories.push(projectRoot);
  const server = createApp({ projectRoot, quiet: true }).listen(0, "127.0.0.1");
  await new Promise<void>((resolve) => server.once("listening", resolve));
  closeServers.push(() => new Promise<void>((resolve, reject) => server.close((error) => error ? reject(error) : resolve())));
  return { projectRoot, baseUrl: `http://127.0.0.1:${(server.address() as AddressInfo).port}` };
}

async function createMap(baseUrl: string, mapId: string, displayName = mapId, columns = 4, rows = 2) {
  const response = await fetch(`${baseUrl}/api/maps`, {
    method: "POST",
    headers: { "content-type": "application/json" },
    body: JSON.stringify({ mapId, displayName, columns, rows }),
  });
  expect(response.status).toBe(201);
  return response.json() as Promise<{ project: WorldProject; geography: GeographyDocument }>;
}

describe("local workbench API", () => {
  it("creates, lists, opens, and duplicates isolated MapId sources", async () => {
    const { projectRoot, baseUrl } = await startApi();
    const alpha = await createMap(baseUrl, "map_alpha", "Alpha");
    await createMap(baseUrl, "map_beta", "Beta");
    alpha.geography.provinces.push({ provinceId: "province_alpha", name: "Alpha Province", layoutId: "alpha_layout" });
    expect((await fetch(`${baseUrl}/api/geography?mapId=map_alpha`, {
      method: "PUT", headers: { "content-type": "application/json" }, body: JSON.stringify(alpha.geography),
    })).status).toBe(200);

    const duplicateResponse = await fetch(`${baseUrl}/api/maps/map_alpha/duplicate`, {
      method: "POST",
      headers: { "content-type": "application/json" },
      body: JSON.stringify({ mapId: "map_alpha_copy", displayName: "Alpha Copy" }),
    });
    expect(duplicateResponse.status).toBe(201);
    const catalog = await fetch(`${baseUrl}/api/maps`).then((response) => response.json()) as { version: number; defaultMapId: string; maps: Array<{ mapId: string; kind: string }> };
    expect(catalog.version).toBe(2);
    expect(catalog.defaultMapId).toBe("map_alpha");
    expect(catalog.maps.every((entry) => entry.kind === "authoring")).toBe(true);
    expect(catalog.maps.map((entry) => entry.mapId)).toEqual(["map_alpha", "map_alpha_copy", "map_beta"]);
    const alphaLoaded = await fetch(`${baseUrl}/api/maps/map_alpha`).then((response) => response.json()) as { geography: GeographyDocument };
    const betaLoaded = await fetch(`${baseUrl}/api/maps/map_beta`).then((response) => response.json()) as { geography: GeographyDocument };
    const copyLoaded = await fetch(`${baseUrl}/api/maps/map_alpha_copy`).then((response) => response.json()) as { geography: GeographyDocument };
    expect(alphaLoaded.geography.provinces).toHaveLength(1);
    expect(betaLoaded.geography.provinces).toHaveLength(0);
    expect(copyLoaded.geography.provinces).toEqual(alphaLoaded.geography.provinces);
    await expect(fs.stat(path.join(projectRoot, "config", "world", "maps", "map_alpha", "source", "workbench.project.json"))).resolves.toBeDefined();
    await expect(fs.stat(path.join(projectRoot, "config", "world", "maps", "map_beta", "source", "workbench.project.json"))).resolves.toBeDefined();
  });

  it("creates bounded non-default grids deterministically and rejects mutation before conflicting writes", async () => {
    const { projectRoot, baseUrl } = await startApi();
    const twoByOne = await createMap(baseUrl, "grid_two_by_one", "Two By One", 2, 1);
    const threeByTwo = await createMap(baseUrl, "grid_three_by_two", "Three By Two", 3, 2);

    expect(twoByOne.project.world).toEqual({ width: 2048, height: 1024 });
    expect(twoByOne.project.chunks.map((chunk) => [chunk.id, chunk.coordinate, chunk.worldOrigin])).toEqual([
      ["chunk_0_0", [0, 0], [0, 0]],
      ["chunk_1_0", [1, 0], [1024, 0]],
    ]);
    expect(threeByTwo.project.chunks).toHaveLength(6);
    await expect(fs.stat(path.join(projectRoot, "config", "world", "maps", "grid_two_by_one", "source", "workbench.project.json"))).resolves.toBeDefined();
    await expect(fs.stat(path.join(projectRoot, "config", "world", "maps", "grid_three_by_two", "source", "workbench.project.json"))).resolves.toBeDefined();

    for (const input of [
      { mapId: "zero_grid", displayName: "Zero", columns: 0, rows: 1 },
      { mapId: "huge_grid", displayName: "Huge", columns: 32, rows: 32 },
    ]) {
      const response = await fetch(`${baseUrl}/api/maps`, { method: "POST", headers: { "content-type": "application/json" }, body: JSON.stringify(input) });
      expect(response.status).toBe(400);
      await expect(fs.stat(path.join(projectRoot, "config", "world", "maps", input.mapId))).rejects.toThrow();
      await expect(fs.stat(path.join(projectRoot, "assets", "textures", "world", "maps", input.mapId))).rejects.toThrow();
    }

    const original = await fs.readFile(path.join(projectRoot, "config", "world", "maps", "grid_two_by_one", "source", "workbench.project.json"));
    const conflict = await fetch(`${baseUrl}/api/maps`, {
      method: "POST", headers: { "content-type": "application/json" }, body: JSON.stringify({ mapId: "grid_two_by_one", displayName: "Overwrite", columns: 1, rows: 1 }),
    });
    expect(conflict.status).not.toBe(201);
    expect(await fs.readFile(path.join(projectRoot, "config", "world", "maps", "grid_two_by_one", "source", "workbench.project.json"))).toEqual(original);
  });

  it("expands only right and down while preserving prior Chunk data and rejecting shrink", async () => {
    const { projectRoot, baseUrl } = await startApi();
    const bundle = await createMap(baseUrl, "expand_map", "Expand", 2, 1);
    bundle.geography.provinces.push({ provinceId: "province_keep", name: "Keep", layoutId: "layout_keep" });
    await fetch(`${baseUrl}/api/geography?mapId=expand_map`, {
      method: "PUT", headers: { "content-type": "application/json" }, body: JSON.stringify(bundle.geography),
    });
    const firstMaskPath = path.join(projectRoot, "assets", "textures", "world", "maps", "expand_map", "draft", "masks", "terrain", "chunk_0_0.png");
    const firstMask = await fs.readFile(firstMaskPath);
    const originalChunks = structuredClone(bundle.project.chunks);

    const expandedResponse = await fetch(`${baseUrl}/api/maps/expand_map/grid`, {
      method: "POST", headers: { "content-type": "application/json" }, body: JSON.stringify({ columns: 3, rows: 2 }),
    });
    expect(expandedResponse.status).toBe(200);
    const expanded = await fetch(`${baseUrl}/api/maps/expand_map`).then((response) => response.json()) as { project: WorldProject; geography: GeographyDocument };
    expect(expanded.project.world).toEqual({ width: 3072, height: 2048 });
    expect(expanded.project.chunks.slice(0, originalChunks.length)).toEqual(originalChunks);
    expect(expanded.project.chunks.map((chunk) => chunk.id)).toEqual(["chunk_0_0", "chunk_1_0", "chunk_2_0", "chunk_0_1", "chunk_1_1", "chunk_2_1"]);
    expect(expanded.geography.provinces).toEqual(bundle.geography.provinces);
    expect(await fs.readFile(firstMaskPath)).toEqual(firstMask);
    await expect(fs.stat(path.join(projectRoot, "assets", "textures", "world", "maps", "expand_map", "draft", "masks", "terrain", "chunk_2_1.png"))).resolves.toBeDefined();

    const sourcePath = path.join(projectRoot, "config", "world", "maps", "expand_map", "source", "workbench.project.json");
    const beforeShrink = await fs.readFile(sourcePath);
    const shrink = await fetch(`${baseUrl}/api/maps/expand_map/grid`, {
      method: "POST", headers: { "content-type": "application/json" }, body: JSON.stringify({ columns: 2, rows: 2 }),
    });
    expect(shrink.status).toBe(400);
    expect(await fs.readFile(sourcePath)).toEqual(beforeShrink);
  });

  it("saves and reopens an incomplete draft while publish preserves the absent pointer", async () => {
    const { projectRoot, baseUrl } = await startApi();
    const bundle = await createMap(baseUrl, "incomplete_map");
    bundle.geography.provinces.push({ provinceId: "province_draft", name: "Draft", layoutId: "draft_layout" });
    const save = await fetch(`${baseUrl}/api/geography?mapId=incomplete_map`, {
      method: "PUT", headers: { "content-type": "application/json" }, body: JSON.stringify(bundle.geography),
    });
    expect(save.status).toBe(200);
    const reopened = await fetch(`${baseUrl}/api/maps/incomplete_map`).then((response) => response.json()) as { geography: GeographyDocument };
    expect(reopened.geography.provinces).toEqual(bundle.geography.provinces);

    const publish = await fetch(`${baseUrl}/api/maps/incomplete_map/publish`, {
      method: "POST", headers: { "content-type": "application/json" }, body: JSON.stringify({ profile: "region-interactive" }),
    });
    expect(publish.status).toBe(400);
    await expect(fs.stat(path.join(projectRoot, "config", "world", "published", "incomplete_map", "current.json"))).rejects.toThrow();
  });

  it("saves map-scoped terrain and compiles map-scoped location artifacts", async () => {
    const { projectRoot, baseUrl } = await startApi();
    const bundle = await createMap(baseUrl, "artifact_map");
    const maskWidth = bundle.project.chunk.width / bundle.project.chunk.terrainCellSize;
    const maskHeight = bundle.project.chunk.height / bundle.project.chunk.terrainCellSize;
    const cells = Buffer.alloc(maskWidth * maskHeight, 2).toString("base64");
    const maskResponse = await fetch(`${baseUrl}/api/terrain/masks?mapId=artifact_map`, {
      method: "POST", headers: { "content-type": "application/json" }, body: JSON.stringify({ chunks: [{ chunkId: bundle.project.chunks[0]!.id, width: maskWidth, height: maskHeight, cellsBase64: cells }] }),
    });
    expect(maskResponse.status).toBe(200);
    await expect(fs.stat(path.join(projectRoot, "assets", "textures", "world", "maps", "artifact_map", "draft", "masks", "terrain", "chunk_0_0.png"))).resolves.toBeDefined();

    bundle.geography.provinces.push({ provinceId: "province_api", name: "API Province", layoutId: "province_api_layout" });
    bundle.geography.strategicLocations.features.push(point([64, 64], { locationId: "city_api", provinceId: "province_api", name: "API City", locationType: "main-city" }));
    bundle.geography.locationGeometries.features.push(polygon([[[0, 0], [128, 0], [128, 128], [0, 128], [0, 0]]], { locationId: "city_api", provinceId: "province_api", direction: "center" }));
    const compileResponse = await fetch(`${baseUrl}/api/location-geometries/compile?mapId=artifact_map`, {
      method: "POST", headers: { "content-type": "application/json" }, body: JSON.stringify(bundle.geography),
    });
    expect(compileResponse.status).toBe(200);
    await expect(fs.stat(path.join(projectRoot, "assets", "textures", "world", "maps", "artifact_map", "draft", "regions", "chunks", "chunk_0_0.png"))).resolves.toBeDefined();
    await expect(fs.stat(path.join(projectRoot, "assets", "textures", "world", "maps", "artifact_map", "draft", "regions", "territory_mask.png"))).resolves.toBeDefined();
    await expect(fs.stat(path.join(projectRoot, "assets", "textures", "world", "maps", "artifact_map", "draft", "regions", "region_lookup.json"))).resolves.toBeDefined();
  });
});
