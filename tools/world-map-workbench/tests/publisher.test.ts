import crypto from "node:crypto";
import fs from "node:fs/promises";
import os from "node:os";
import path from "node:path";
import { point, polygon } from "@turf/turf";
import sharp from "sharp";
import { afterEach, describe, expect, it } from "vitest";
import { MapPublisher } from "../src/server/mapPublisher.js";
import { ProjectRepository } from "../src/server/projectRepository.js";

const temporaryDirectories: string[] = [];

function compareOrdinal(left: string, right: string): number {
  return left < right ? -1 : left > right ? 1 : 0;
}

function resolveResourcePath(projectRoot: string, resourcePath: string): string {
  expect(resourcePath.startsWith("res://")).toBe(true);
  return path.join(projectRoot, resourcePath.slice(6));
}

async function sha256(filePath: string): Promise<string> {
  return crypto.createHash("sha256").update(await fs.readFile(filePath)).digest("hex");
}

afterEach(async () => {
  await Promise.all(temporaryDirectories.splice(0).map((directory) => fs.rm(directory, { recursive: true, force: true })));
});

async function createPublishableMap() {
  const projectRoot = await fs.mkdtemp(path.join(os.tmpdir(), "world-map-publisher-"));
  temporaryDirectories.push(projectRoot);
  const repository = new ProjectRepository(projectRoot);
  const bundle = await repository.createMap("publish_map", "Publish Map", 2, 1);
  bundle.project.world = { width: 32, height: 16 };
  bundle.project.chunk = { width: 16, height: 16, terrainCellSize: 4, territoryMaskScale: 1 };
  bundle.project.chunks = [
    { id: "west", coordinate: [0, 0], worldOrigin: [0, 0], visualTexturePath: "source-visual/west.png", terrainMaskPath: "masks/terrain/west.png", territoryMaskPath: "masks/territory/west.png" },
    { id: "east", coordinate: [1, 0], worldOrigin: [16, 0], visualTexturePath: "source-visual/east.png", terrainMaskPath: "masks/terrain/east.png", territoryMaskPath: "masks/territory/east.png" },
  ];
  await fs.mkdir(path.join(projectRoot, "assets", "textures", "world", "source-visual"), { recursive: true });
  await Promise.all([
    sharp({ create: { width: 16, height: 16, channels: 4, background: "#224466" } }).png().toFile(path.join(projectRoot, "assets", "textures", "world", "source-visual", "west.png")),
    sharp({ create: { width: 16, height: 16, channels: 4, background: "#664422" } }).png().toFile(path.join(projectRoot, "assets", "textures", "world", "source-visual", "east.png")),
  ]);
  bundle.geography.provinces = [
    { provinceId: "province_west", name: "West", layoutId: "layout_west" },
    { provinceId: "province_east", name: "East", layoutId: "layout_east" },
  ];
  bundle.geography.strategicLocations.features = [
    point([8, 8], { locationId: "city_west", provinceId: "province_west", name: "West City", locationType: "main-city" }),
    point([24, 8], { locationId: "city_east", provinceId: "province_east", name: "East City", locationType: "main-city" }),
  ];
  bundle.geography.locationGeometries.features = [
    polygon([[[0, 0], [16, 0], [16, 16], [0, 16], [0, 0]]], { locationId: "city_west", provinceId: "province_west", direction: "west" }),
    polygon([[[16, 0], [32, 0], [32, 16], [16, 16], [16, 0]]], { locationId: "city_east", provinceId: "province_east", direction: "east" }),
  ];
  await repository.saveProject("publish_map", bundle.project);
  await repository.saveGeography("publish_map", bundle.geography);
  return { projectRoot, repository, publisher: new MapPublisher(repository) };
}

describe("map publisher", () => {
  it("publishes one immutable revision and distinct visual/category import contracts", async () => {
    const { projectRoot, publisher } = await createPublishableMap();
    const published = await publisher.publish("publish_map", "region-interactive");
    const configRevision = path.join(projectRoot, "config", "world", "published", "publish_map", published.revision);
    const assetRevision = path.join(projectRoot, "assets", "textures", "world", "maps", "publish_map", published.revision);
    const pointer = JSON.parse(await fs.readFile(path.join(projectRoot, "config", "world", "published", "publish_map", "current.json"), "utf8")) as { revision: string };

    expect(pointer.revision).toBe(published.revision);
    await expect(fs.stat(path.join(configRevision, "package.json"))).resolves.toBeDefined();
    await expect(fs.stat(path.join(assetRevision, "regions", "chunks", "west.png"))).resolves.toBeDefined();
    const visualImport = await fs.readFile(path.join(assetRevision, "visual", "west.png.import"), "utf8");
    const regionImport = await fs.readFile(path.join(assetRevision, "regions", "chunks", "west.png.import"), "utf8");
    expect(visualImport).toContain("compress/mode=0");
    expect(visualImport).toContain("mipmaps/generate=true");
    expect(regionImport).toContain("compress/mode=0");
    expect(regionImport).toContain("mipmaps/generate=false");
  });

  it("publishes schema-v2 hashes with exact artifact coverage and byte integrity", async () => {
    const { projectRoot, publisher } = await createPublishableMap();
    const published = await publisher.publish("publish_map", "region-interactive");

    expect(published.schemaVersion).toBe(2);
    expect(published.artifactHashes.map((artifact) => `${artifact.kind}:${artifact.artifactId}`)).toEqual([
      "chunk-manifest:manifest",
      "geography:geography",
      "region-lookup:lookup",
      "region-mask:east",
      "region-mask:west",
      "region-outlines:outlines",
      "visual:east",
      "visual:west",
    ]);

    const packagePaths = new Map<string, string>([
      ["chunk-manifest:manifest", published.chunkManifestPath],
      ["geography:geography", published.geographyPath],
      ["region-lookup:lookup", published.regionArtifacts!.lookupPath],
      ["region-outlines:outlines", published.regionArtifacts!.outlinesPath],
      ...published.regionArtifacts!.chunks.map((chunk) => [`region-mask:${chunk.chunkId}`, chunk.maskTexturePath] as [string, string]),
      ...(["west", "east"] as const).map((chunkId) => [
        `visual:${chunkId}`,
        `res://assets/textures/world/maps/publish_map/${published.revision}/visual/${chunkId}.png`,
      ] as [string, string]),
    ]);
    for (const artifact of published.artifactHashes) {
      const key = `${artifact.kind}:${artifact.artifactId}`;
      expect(artifact.sha256).toBe(await sha256(resolveResourcePath(projectRoot, packagePaths.get(key)!)));
    }

    const canonical = [...published.artifactHashes]
      .sort((left, right) => compareOrdinal(left.kind, right.kind) || compareOrdinal(left.artifactId, right.artifactId))
      .map((artifact) => `${artifact.kind}\0${artifact.artifactId}\0${artifact.sha256}\n`)
      .join("");
    expect(published.contentHash).toBe(crypto.createHash("sha256").update(canonical, "utf8").digest("hex"));
  });

  it("preserves the prior current pointer when a later publish fails", async () => {
    const { projectRoot, publisher } = await createPublishableMap();
    const first = await publisher.publish("publish_map", "region-interactive");
    await sharp({ create: { width: 8, height: 8, channels: 4, background: "#ffffff" } }).png().toFile(path.join(projectRoot, "assets", "textures", "world", "source-visual", "west.png"));
    await expect(publisher.publish("publish_map", "region-interactive")).rejects.toThrow("dimensions mismatch");
    const pointer = JSON.parse(await fs.readFile(path.join(projectRoot, "config", "world", "published", "publish_map", "current.json"), "utf8")) as { revision: string };
    expect(pointer.revision).toBe(first.revision);
  });

  it("reports strategic-runtime capability blockers separately", async () => {
    const { publisher } = await createPublishableMap();
    await expect(publisher.publish("publish_map", "strategic-runtime")).rejects.toThrow("requires terrain mask");
  });

  it("changes strategic-runtime revision when navigation bytes change", async () => {
    const { projectRoot, repository, publisher } = await createPublishableMap();
    const bundle = await repository.loadBundle("publish_map");
    for (const chunk of bundle.project.chunks) {
      const terrainPath = path.join(projectRoot, "assets", "textures", "world", "maps", "publish_map", "draft", chunk.terrainMaskPath!);
      await fs.mkdir(path.dirname(terrainPath), { recursive: true });
      await sharp(Buffer.alloc(16), { raw: { width: 4, height: 4, channels: 1 } }).png().toFile(terrainPath);
      chunk.navigationScenePath = `res://scenes/world/navigation/${chunk.id}.tscn`;
      const navigationPath = resolveResourcePath(projectRoot, chunk.navigationScenePath);
      await fs.mkdir(path.dirname(navigationPath), { recursive: true });
      await fs.writeFile(navigationPath, `[gd_scene format=3]\n\n[node name="${chunk.id}" type="Node2D"]\n`, "utf8");
    }
    await repository.saveProject("publish_map", bundle.project);
    const first = await publisher.publish("publish_map", "strategic-runtime");

    const changedNavigation = resolveResourcePath(projectRoot, bundle.project.chunks[0]!.navigationScenePath!);
    await fs.appendFile(changedNavigation, "\n; navigation revision changed\n", "utf8");
    const second = await publisher.publish("publish_map", "strategic-runtime");

    expect(second.revision).not.toBe(first.revision);
    expect(second.artifactHashes.find((artifact) => artifact.kind === "navigation" && artifact.artifactId === "west")?.sha256)
      .not.toBe(first.artifactHashes.find((artifact) => artifact.kind === "navigation" && artifact.artifactId === "west")?.sha256);
  });
});
