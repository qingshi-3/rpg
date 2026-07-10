import path from "node:path";
import { describe, expect, it } from "vitest";
import { createPathPolicy } from "../src/server/pathPolicy.js";

describe("project path policy", () => {
  it("allows only configured world config and texture roots", () => {
    const root = path.resolve("D:/workspace/project");
    const policy = createPathPolicy(root);

    expect(policy.resolveConfig("geography.json")).toBe(path.join(root, "config", "world", "geography.json"));
    expect(policy.resolveTexture("masks/terrain/chunk_0_0.png")).toBe(
      path.join(root, "assets", "textures", "world", "masks", "terrain", "chunk_0_0.png"),
    );
  });

  it("rejects path traversal and absolute paths", () => {
    const policy = createPathPolicy(path.resolve("D:/workspace/project"));
    expect(() => policy.resolveConfig("../../secret.txt")).toThrow(/outside/i);
    expect(() => policy.resolveTexture("C:/secret.png")).toThrow(/relative/i);
  });
});
