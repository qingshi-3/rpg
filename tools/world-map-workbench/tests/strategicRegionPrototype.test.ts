import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";
import { validateRegions } from "../src/shared/geo.js";
import { geographyDocumentSchema } from "../src/shared/schema.js";

const geographyPath = path.resolve(process.cwd(), "../../config/world/geography.json");

function loadRegions() {
  return geographyDocumentSchema.parse(JSON.parse(fs.readFileSync(geographyPath, "utf8"))).regions;
}

function edgeKey(left: number[], right: number[]): string {
  const a = `${left[0]},${left[1]}`;
  const b = `${right[0]},${right[1]}`;
  return a < b ? `${a}|${b}` : `${b}|${a}`;
}

function adjacencyDegrees(cityId: string): number[] {
  const regions = loadRegions().features.filter((region) => region.properties.cityId === cityId);
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
    const regions = loadRegions();
    const errors = validateRegions(regions).filter((item) => item.severity === "error");

    expect(errors).toEqual([]);
    expect(regions.features.filter((region) => region.properties.cityId === "city_qinghe")).toHaveLength(5);
    expect(regions.features.filter((region) => region.properties.cityId === "city_chiyan")).toHaveLength(6);
    expect(new Set(regions.features.map((region) => region.properties.regionId)).size).toBe(11);
  });

  it("uses exact shared edges and distinct non-quadrant connection patterns", () => {
    expect(adjacencyDegrees("city_qinghe")).toEqual([1, 2, 3, 3, 3]);
    expect(adjacencyDegrees("city_chiyan")).toEqual([1, 1, 2, 2, 2, 2]);
  });
});
