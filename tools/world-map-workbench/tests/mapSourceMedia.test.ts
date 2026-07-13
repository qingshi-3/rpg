import fs from "node:fs/promises";
import path from "node:path";
import { describe, expect, it } from "vitest";
import type { MapCatalog, WorldProject } from "../src/shared/types.js";

const projectRoot = path.resolve(process.cwd(), "../..");

async function readProject(mapId: string): Promise<WorldProject> {
  return JSON.parse(await fs.readFile(path.join(projectRoot, "config", "world", "maps", mapId, "source", "workbench.project.json"), "utf8")) as WorldProject;
}

function resolveResource(resourcePath: string): string {
  if (!resourcePath.startsWith("res://")) throw new Error(`Expected explicit source ownership path: ${resourcePath}`);
  return path.join(projectRoot, resourcePath.slice("res://".length));
}

describe("checked-in map catalog and authoring media", () => {
  it("declares the authoring default separately from the verification fixture", async () => {
    const catalog = JSON.parse(await fs.readFile(path.join(projectRoot, "config", "world", "maps", "catalog.json"), "utf8")) as MapCatalog;
    expect(catalog).toEqual(expect.objectContaining({ version: 2, defaultMapId: "mock_qinghe_chiyan" }));
    expect(catalog.maps.find((entry) => entry.mapId === "mock_qinghe_chiyan")?.kind).toBe("authoring");
    expect(catalog.maps.find((entry) => entry.mapId === "fixture_north_pass")?.kind).toBe("fixture");
  });

  it("resolves mock reference/visual media and leaves the fixture without borrowed reference media", async () => {
    const mock = await readProject("mock_qinghe_chiyan");
    const fixture = await readProject("fixture_north_pass");
    for (const chunk of mock.chunks) {
      expect(chunk.referenceTexturePath).toMatch(/^res:\/\/assets\/textures\/world\/reference\//);
      expect(chunk.visualTexturePath).toMatch(/^res:\/\/assets\/textures\/world\/visual-chunks\//);
      await expect(fs.stat(resolveResource(chunk.referenceTexturePath!))).resolves.toBeDefined();
      await expect(fs.stat(resolveResource(chunk.visualTexturePath!))).resolves.toBeDefined();
    }
    for (const chunk of fixture.chunks) {
      expect(chunk.referenceTexturePath).toBeUndefined();
      expect(chunk.visualTexturePath).toMatch(/^res:\/\/assets\/textures\/world\/maps\/fixture_north_pass\//);
      await expect(fs.stat(resolveResource(chunk.visualTexturePath!))).resolves.toBeDefined();
    }
  });
});
