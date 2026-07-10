import fs from "node:fs/promises";
import os from "node:os";
import path from "node:path";
import type { AddressInfo } from "node:net";
import { afterEach, describe, expect, it } from "vitest";
import { polygon } from "@turf/turf";
import { createApp } from "../src/server/app.js";

const temporaryDirectories: string[] = [];

afterEach(async () => {
  await Promise.all(temporaryDirectories.splice(0).map((directory) => fs.rm(directory, { recursive: true, force: true })));
});

describe("local workbench API", () => {
  it("bootstraps and round-trips the canonical project documents", async () => {
    const projectRoot = await fs.mkdtemp(path.join(os.tmpdir(), "world-map-api-"));
    temporaryDirectories.push(projectRoot);
    const app = createApp({ projectRoot, quiet: true });
    const server = app.listen(0, "127.0.0.1");
    await new Promise<void>((resolve) => server.once("listening", () => resolve()));
    const address = server.address() as AddressInfo;
    const baseUrl = `http://127.0.0.1:${address.port}`;

    try {
      const initial = await fetch(`${baseUrl}/api/project`).then((response) => response.json()) as { initialized: boolean };
      expect(initial.initialized).toBe(false);

      const bootstrapResponse = await fetch(`${baseUrl}/api/project/bootstrap`, { method: "POST" });
      expect(bootstrapResponse.status).toBe(201);
      await expect(fs.stat(path.join(projectRoot, "config", "world", "workbench.project.json"))).resolves.toBeDefined();

      const bundle = await fetch(`${baseUrl}/api/project`).then((response) => response.json()) as {
        initialized: boolean;
        geography: { strategicLocations: { features: unknown[] } };
      };
      expect(bundle.initialized).toBe(true);
      expect(bundle.geography.strategicLocations.features).toEqual([]);

      const saveResponse = await fetch(`${baseUrl}/api/geography`, {
        method: "PUT",
        headers: { "content-type": "application/json" },
        body: JSON.stringify(bundle.geography),
      });
      expect(saveResponse.status).toBe(200);
    } finally {
      await new Promise<void>((resolve, reject) => server.close((error) => error ? reject(error) : resolve()));
    }
  });

  it("rejects malformed project data without writing it", async () => {
    const projectRoot = await fs.mkdtemp(path.join(os.tmpdir(), "world-map-api-"));
    temporaryDirectories.push(projectRoot);
    const app = createApp({ projectRoot, quiet: true });
    const server = app.listen(0, "127.0.0.1");
    await new Promise<void>((resolve) => server.once("listening", () => resolve()));
    const address = server.address() as AddressInfo;
    try {
      const response = await fetch(`http://127.0.0.1:${address.port}/api/project`, {
        method: "PUT",
        headers: { "content-type": "application/json" },
        body: JSON.stringify({ version: 1 }),
      });
      expect(response.status).toBe(400);
      await expect(fs.stat(path.join(projectRoot, "config", "world", "workbench.project.json"))).rejects.toThrow();
    } finally {
      await new Promise<void>((resolve, reject) => server.close((error) => error ? reject(error) : resolve()));
    }
  });

  it("saves terrain chunks and compiles region artifacts through the service", async () => {
    const projectRoot = await fs.mkdtemp(path.join(os.tmpdir(), "world-map-api-"));
    temporaryDirectories.push(projectRoot);
    const app = createApp({ projectRoot, quiet: true });
    const server = app.listen(0, "127.0.0.1");
    await new Promise<void>((resolve) => server.once("listening", () => resolve()));
    const address = server.address() as AddressInfo;
    const baseUrl = `http://127.0.0.1:${address.port}`;
    try {
      const bundle = await fetch(`${baseUrl}/api/project/bootstrap`, { method: "POST" }).then((response) => response.json()) as {
        project: { chunk: { width: number; height: number; terrainCellSize: number }; chunks: Array<{ id: string }> };
        geography: { regions: { features: unknown[] } };
      };
      const maskWidth = bundle.project.chunk.width / bundle.project.chunk.terrainCellSize;
      const maskHeight = bundle.project.chunk.height / bundle.project.chunk.terrainCellSize;
      const cells = Buffer.alloc(maskWidth * maskHeight, 2).toString("base64");
      const maskResponse = await fetch(`${baseUrl}/api/terrain/masks`, {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify({ chunks: [{ chunkId: bundle.project.chunks[0]!.id, width: maskWidth, height: maskHeight, cellsBase64: cells }] }),
      });
      expect(maskResponse.status).toBe(200);
      await expect(fs.stat(path.join(projectRoot, "assets", "textures", "world", "masks", "terrain", "chunk_0_0.png"))).resolves.toBeDefined();

      bundle.geography.regions.features.push(polygon([[[0, 0], [128, 0], [128, 128], [0, 128], [0, 0]]], {
        regionId: "region_api",
        cityId: "city_api",
        role: "core",
        direction: "center",
      }));
      const compileResponse = await fetch(`${baseUrl}/api/regions/compile`, {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify(bundle.geography),
      });
      expect(compileResponse.status).toBe(200);
      await expect(fs.stat(path.join(projectRoot, "assets", "textures", "world", "masks", "territory", "territory_mask.png"))).resolves.toBeDefined();
      await expect(fs.stat(path.join(projectRoot, "assets", "textures", "world", "masks", "territory", "region_lookup.json"))).resolves.toBeDefined();
    } finally {
      await new Promise<void>((resolve, reject) => server.close((error) => error ? reject(error) : resolve()));
    }
  });
});
