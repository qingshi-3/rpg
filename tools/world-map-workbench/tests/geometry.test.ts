import { featureCollection, lineString, point, polygon } from "@turf/turf";
import { describe, expect, it } from "vitest";
import { clipLinearFeaturesToChunks, snapRiverConfluence, snapRiverEndpoints, validateRegions } from "../src/shared/geo.js";
import { createDefaultProject } from "../src/shared/defaultProject.js";
import type { LinearFeature, WaterAnchorFeature } from "../src/shared/types.js";

describe("geographic contracts", () => {
  it("snaps a tributary and records the receiver relationship", () => {
    const receiver = lineString(
      [
        [100, 0],
        [100, 200],
      ],
      { featureId: "river_main", featureType: "river" as const },
    );
    const tributary = lineString(
      [
        [0, 50],
        [96, 50],
      ],
      { featureId: "river_branch", featureType: "river" as const },
    );

    const result = snapRiverConfluence(tributary, [receiver], 8);
    expect(result.properties?.receiverId).toBe("river_main");
    expect(result.geometry.coordinates.at(-1)).toEqual([100, 50]);
  });

  it("snaps river endpoints to configured source and coast anchors", () => {
    const river = lineString([[4, 4], [196, 4]], { featureId: "river_a", featureType: "river" as const, receiverId: "stale_receiver" }) as LinearFeature;
    const anchors = [
      point([0, 0], { anchorId: "source_a", name: "源头", anchorType: "source" as const }) as WaterAnchorFeature,
      point([200, 0], { anchorId: "coast_a", name: "海岸", anchorType: "coast" as const }) as WaterAnchorFeature,
    ];

    const result = snapRiverEndpoints(river, [], anchors, 8);
    expect(result.geometry.coordinates[0]).toEqual([0, 0]);
    expect(result.geometry.coordinates.at(-1)).toEqual([200, 0]);
    expect(result.properties).toMatchObject({ startAnchorId: "source_a", endAnchorId: "coast_a" });
    expect(result.properties.receiverId).toBeUndefined();
  });

  it("produces explicit per-chunk clip previews from one global line", () => {
    const project = createDefaultProject();
    const line = lineString([[900, 100], [1100, 100]], { featureId: "road_a", featureType: "road" as const }) as LinearFeature;
    const clips = clipLinearFeaturesToChunks(project, featureCollection([line]));
    expect(clips.features.map((feature) => feature.properties.chunkId)).toEqual(expect.arrayContaining(["chunk_0_0", "chunk_1_0"]));
  });

  it("reports duplicate region ids, overlap, holes, and cross-city conflict", () => {
    const regions = featureCollection([
      polygon(
        [
          [
            [0, 0],
            [100, 0],
            [100, 100],
            [0, 100],
            [0, 0],
          ],
          [
            [25, 25],
            [50, 25],
            [50, 50],
            [25, 50],
            [25, 25],
          ],
        ],
        { regionId: "region_a", cityId: "city_a", role: "core", direction: "center" },
      ),
      polygon(
        [
          [
            [50, 50],
            [150, 50],
            [150, 150],
            [50, 150],
            [50, 50],
          ],
        ],
        { regionId: "region_a", cityId: "city_b", role: "outer", direction: "east" },
      ),
    ]);

    const codes = validateRegions(regions).map((item) => item.code);
    expect(codes).toEqual(expect.arrayContaining(["REGION_DUPLICATE_ID", "REGION_HOLE", "REGION_OVERLAP", "REGION_CROSS_CITY"]));
  });
});
