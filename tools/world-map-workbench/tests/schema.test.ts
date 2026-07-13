import { describe, expect, it } from "vitest";
import { point, polygon } from "@turf/turf";
import { createDefaultProject, createEmptyGeography } from "../src/shared/defaultProject.js";
import { geographyDocumentSchema, projectSchema } from "../src/shared/schema.js";

describe("project schema", () => {
  it("accepts the bootstrap project", () => {
    expect(projectSchema.parse(createDefaultProject()).version).toBe(2);
  });

  it("rejects duplicate chunk ids and coordinates", () => {
    const project = createDefaultProject();
    project.chunks[1] = {
      ...project.chunks[0]!,
      worldOrigin: [project.chunk.width, 0],
    };

    const result = projectSchema.safeParse(project);
    expect(result.success).toBe(false);
    expect(result.error?.issues.map((issue) => issue.message).join(" ")).toContain("Duplicate chunk id");
  });

  it("rejects unknown or missing accepted layers", () => {
    const project = createDefaultProject();
    project.layers = project.layers.filter((layer) => layer.id !== "validation");
    project.layers.push({
      id: "not-authoritative" as never,
      label: "bad",
      kind: "derived",
      visible: true,
      locked: true,
      opacity: 1,
    });

    expect(projectSchema.safeParse(project).success).toBe(false);
  });
});

describe("geography schema", () => {
  it("accepts one main city and one geometry per province member city", () => {
    const geography = createEmptyGeography();
    geography.provinces = [{ provinceId: "province_a", name: "甲省", layoutId: "layout_a" }];
    geography.strategicLocations.features.push(point([10, 10], { locationId: "city_a", provinceId: "province_a", name: "甲城", locationType: "main-city" }));
    geography.locationGeometries.features.push(polygon([[[0, 0], [20, 0], [20, 20], [0, 20], [0, 0]]], { locationId: "city_a", provinceId: "province_a", direction: "center" }));

    expect(geographyDocumentSchema.parse(geography).version).toBe(3);
  });

  it("rejects missing main cities, mismatched province membership, and missing geometry", () => {
    const geography = createEmptyGeography();
    geography.provinces = [{ provinceId: "province_a", name: "甲省", layoutId: "layout_a" }];
    geography.strategicLocations.features.push(point([10, 10], { locationId: "city_a", provinceId: "province_missing", name: "甲城", locationType: "auxiliary-city" }));

    const messages = geographyDocumentSchema.safeParse(geography).error?.issues.map((issue) => issue.message).join(" ") ?? "";
    expect(messages).toContain("unknown province");
    expect(messages).toContain("exactly one main city");
    expect(messages).toContain("exactly one location geometry");
  });
});
