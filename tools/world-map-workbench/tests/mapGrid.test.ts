import { describe, expect, it } from "vitest";
import { createDefaultProject } from "../src/shared/defaultProject.js";
import { expandProjectGrid, getProjectGridDimensions, validateMapGridDimensions } from "../src/shared/mapGrid.js";

describe("map-level Chunk grid", () => {
  it("generates deterministic non-default grids", () => {
    const project = createDefaultProject("grid_map", "Grid Map", 2, 1);

    expect(project.world).toEqual({ width: 2048, height: 1024 });
    expect(project.chunks).toEqual([
      expect.objectContaining({ id: "chunk_0_0", coordinate: [0, 0], worldOrigin: [0, 0], terrainMaskPath: "masks/terrain/chunk_0_0.png" }),
      expect.objectContaining({ id: "chunk_1_0", coordinate: [1, 0], worldOrigin: [1024, 0], terrainMaskPath: "masks/terrain/chunk_1_0.png" }),
    ]);
    expect(getProjectGridDimensions(project)).toEqual({ columns: 2, rows: 1 });
  });

  it("expands right and down without changing existing Chunk identities or origins", () => {
    const project = createDefaultProject("expand_map", "Expand Map", 2, 1);
    project.chunks[0]!.visualTexturePath = "res://existing.png";
    const before = structuredClone(project.chunks);
    const expanded = expandProjectGrid(project, 3, 2);

    expect(expanded.chunks.slice(0, before.length)).toEqual(before);
    expect(expanded.world).toEqual({ width: 3072, height: 2048 });
    expect(expanded.chunks.map((chunk) => chunk.id)).toEqual([
      "chunk_0_0", "chunk_1_0", "chunk_2_0", "chunk_0_1", "chunk_1_1", "chunk_2_1",
    ]);
    expect(expanded.chunks.find((chunk) => chunk.id === "chunk_2_1")).toEqual(expect.objectContaining({
      coordinate: [2, 1], worldOrigin: [2048, 1024],
    }));
  });

  it("rejects shrinking, zero, non-integer, and excessive grids", () => {
    const project = createDefaultProject("bounded_map", "Bounded Map", 2, 2);
    expect(() => expandProjectGrid(project, 1, 2)).toThrow(/shrinking or relocation/);
    expect(() => expandProjectGrid(project, 2, 1)).toThrow(/shrinking or relocation/);
    expect(() => validateMapGridDimensions(0, 1)).toThrow(/positive integers/);
    expect(() => validateMapGridDimensions(1.5, 2)).toThrow(/positive integers/);
    expect(() => validateMapGridDimensions(32, 32)).toThrow(/supported limit/);
  });
});
