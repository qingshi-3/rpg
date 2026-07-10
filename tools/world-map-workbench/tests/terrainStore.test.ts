import { describe, expect, it } from "vitest";
import { TerrainStore } from "../src/client/model/TerrainStore.js";
import { createDefaultProject } from "../src/shared/defaultProject.js";

describe("TerrainStore", () => {
  it("paints one continuous stroke across a chunk boundary", () => {
    const project = createDefaultProject();
    const store = new TerrainStore(project);

    store.paintStroke([project.chunk.width - 24, 256], [project.chunk.width + 24, 256], 32, 2);

    expect(store.getTerrainIdAtWorld(project.chunk.width - 8, 256)).toBe(2);
    expect(store.getTerrainIdAtWorld(project.chunk.width + 8, 256)).toBe(2);
    expect(store.getDirtyChunkIds()).toEqual(expect.arrayContaining(["chunk_0_0", "chunk_1_0"]));
  });

  it("fills and polygon-paints using stable terrain ids", () => {
    const store = new TerrainStore(createDefaultProject());
    store.floodFill([32, 32], 3);
    expect(store.getTerrainIdAtWorld(32, 32)).toBe(3);

    store.fillPolygon(
      [
        [128, 128],
        [256, 128],
        [256, 256],
        [128, 256],
        [128, 128],
      ],
      5,
    );
    expect(store.getTerrainIdAtWorld(192, 192)).toBe(5);
  });

  it("detects unclassified and isolated terrain cells", () => {
    const store = new TerrainStore(createDefaultProject());
    store.setTerrainIdAtWorld(32, 32, 2);
    const diagnostics = store.validateTerrain(2);
    expect(diagnostics.some((item) => item.code === "TERRAIN_UNCLASSIFIED")).toBe(true);
    expect(diagnostics.some((item) => item.code === "TERRAIN_ISOLATED")).toBe(true);
  });
});
