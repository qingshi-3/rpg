import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";
import { validateLocationGeometries } from "../src/shared/geo.js";
import { geographyDocumentSchema } from "../src/shared/schema.js";

const geographyPath = path.resolve(process.cwd(), "../../config/world/maps/mock_qinghe_chiyan/source/geography.json");

function loadLocationGeometries() {
  return geographyDocumentSchema.parse(JSON.parse(fs.readFileSync(geographyPath, "utf8"))).locationGeometries;
}

function edgeKey(left: number[], right: number[]): string {
  const a = `${left[0]},${left[1]}`;
  const b = `${right[0]},${right[1]}`;
  return a < b ? `${a}|${b}` : `${b}|${a}`;
}

function adjacencyDegrees(provinceId: string): number[] {
  const regions = loadLocationGeometries().features.filter((region) => region.properties.provinceId === provinceId);
  const ownersByEdge = new Map<string, number[]>();
  regions.forEach((region, regionIndex) => {
    const ring = region.geometry.type === "Polygon" ? region.geometry.coordinates[0]! : [];
    for (let index = 0; index < ring.length - 1; index += 1) {
      const key = edgeKey(ring[index]!, ring[index + 1]!);
      ownersByEdge.set(key, [...(ownersByEdge.get(key) ?? []), regionIndex]);
    }
  });
  return regions.map((_region, regionIndex) =>
    [...ownersByEdge.values()].filter((owners) => owners.length === 2 && owners.includes(regionIndex)).length,
  ).sort((left, right) => left - right);
}

describe("standalone strategic-region prototype topology", () => {
  it("keeps valid isolated five and six region city sets", () => {
    const regions = loadLocationGeometries();
    const errors = validateLocationGeometries(regions).filter((item) => item.severity === "error");

    expect(errors).toEqual([]);
    expect(regions.features.filter((region) => region.properties.provinceId === "qinghe")).toHaveLength(5);
    expect(regions.features.filter((region) => region.properties.provinceId === "chiyan")).toHaveLength(6);
    expect(new Set(regions.features.map((region) => region.properties.locationId)).size).toBe(11);
  });

  it("uses exact shared edges and distinct non-quadrant connection patterns", () => {
    expect(adjacencyDegrees("qinghe")).toEqual([1, 2, 3, 3, 3]);
    expect(adjacencyDegrees("chiyan")).toEqual([1, 1, 2, 2, 2, 2]);
  });
});
