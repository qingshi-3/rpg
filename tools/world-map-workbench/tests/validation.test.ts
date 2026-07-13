import { featureCollection, lineString, point, polygon } from "@turf/turf";
import { describe, expect, it } from "vitest";
import { createDefaultProject, createEmptyGeography } from "../src/shared/defaultProject.js";
import type { GeographyDocument, LinearFeature, StrategicLocationFeature } from "../src/shared/types.js";
import { TerrainStore } from "../src/client/model/TerrainStore.js";
import { validateWorkbench } from "../src/client/validation.js";

describe("workbench validation", () => {
  it("reports generated ID-based names as non-blocking completion reminders", () => {
    const project = createDefaultProject();
    const geography = createEmptyGeography();
    geography.provinces = [{ provinceId: "province_generated", name: "province_generated", layoutId: "layout_generated" }];
    geography.strategicLocations = featureCollection([
      point([100, 100], { locationId: "location_generated", provinceId: "province_generated", name: "location_generated", locationType: "main-city" as const }) as StrategicLocationFeature,
    ]);

    const reminders = validateWorkbench(project, new TerrainStore(project), geography).filter((item) => item.code.endsWith("PLACEHOLDER_NAME"));
    expect(reminders.map((item) => item.code)).toEqual(["PROVINCE_PLACEHOLDER_NAME", "CITY_PLACEHOLDER_NAME"]);
    expect(reminders.every((item) => item.severity === "warning")).toBe(true);
  });

  it("detects duplicate locations, river/mountain conflicts, deviation, and boundary endpoints", () => {
    const project = createDefaultProject();
    const geography = createEmptyGeography();
    geography.provinces = [{ provinceId: "province_a", name: "甲省", layoutId: "province_a_layout" }];
    geography.linearFeatures = featureCollection([
      lineString([[100, 100], [300, 100]], { featureId: "river_a", featureType: "river" as const, widthClass: 2 }) as LinearFeature,
      lineString([[100, 200], [300, 200]], { featureId: "mountain_a", featureType: "mountain" as const, density: 0.6 }) as LinearFeature,
      lineString([[1024, 400], [1300, 500]], { featureId: "road_boundary", featureType: "road" as const, roadClass: 1 }) as LinearFeature,
    ]);
    geography.strategicLocations = featureCollection([
      point([150, 100], { locationId: "city_duplicate", provinceId: "province_a", name: "河中城", locationType: "main-city" as const, referencePosition: [500, 500] as [number, number] }) as StrategicLocationFeature,
      point([150, 200], { locationId: "city_duplicate", provinceId: "province_a", name: "山中城", locationType: "auxiliary-city" as const, referencePosition: [150, 200] as [number, number] }) as StrategicLocationFeature,
    ]);
    geography.locationGeometries = featureCollection([
      polygon([[[400, 400], [500, 400], [500, 500], [400, 500], [400, 400]]], {
        locationId: "city_missing",
        provinceId: "province_a",
        direction: "center",
      }),
    ]);

    const codes = validateWorkbench(project, new TerrainStore(project), geography as GeographyDocument).map((item) => item.code);
    expect(codes).toEqual(expect.arrayContaining([
      "LOCATION_DUPLICATE_ID",
      "LOCATION_ON_WATER",
      "LOCATION_ON_MOUNTAIN",
      "LOCATION_REFERENCE_DEVIATION",
      "LINE_BOUNDARY_ENDPOINT",
      "LOCATION_GEOMETRY_UNKNOWN_CITY",
    ]));
  });
});
