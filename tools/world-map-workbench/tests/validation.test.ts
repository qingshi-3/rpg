import { featureCollection, lineString, point, polygon } from "@turf/turf";
import { describe, expect, it } from "vitest";
import { createDefaultProject, createEmptyGeography } from "../src/shared/defaultProject.js";
import type { GeographyDocument, LinearFeature } from "../src/shared/types.js";
import { TerrainStore } from "../src/client/model/TerrainStore.js";
import { validateWorkbench } from "../src/client/validation.js";

describe("workbench validation", () => {
  it("detects duplicate locations, river/mountain conflicts, deviation, and boundary endpoints", () => {
    const project = createDefaultProject();
    const geography = createEmptyGeography();
    geography.linearFeatures = featureCollection([
      lineString([[100, 100], [300, 100]], { featureId: "river_a", featureType: "river" as const, widthClass: 2 }) as LinearFeature,
      lineString([[100, 200], [300, 200]], { featureId: "mountain_a", featureType: "mountain" as const, density: 0.6 }) as LinearFeature,
      lineString([[1024, 400], [1300, 500]], { featureId: "road_boundary", featureType: "road" as const, roadClass: 1 }) as LinearFeature,
    ]);
    geography.strategicLocations = featureCollection([
      point([150, 100], { locationId: "city_duplicate", name: "河中城", locationType: "city" as const, referencePosition: [500, 500] as [number, number] }),
      point([150, 200], { locationId: "city_duplicate", name: "山中城", locationType: "city" as const }),
    ]);
    geography.regions = featureCollection([
      polygon([[[400, 400], [500, 400], [500, 500], [400, 500], [400, 400]]], {
        regionId: "region_unknown_city",
        cityId: "city_missing",
        role: "territory",
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
      "REGION_UNKNOWN_CITY",
    ]));
  });
});
