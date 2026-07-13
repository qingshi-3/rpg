import { describe, expect, it } from "vitest";
import { selectInitialMapId } from "../src/client/mapSelection.js";
import type { MapCatalog } from "../src/shared/types.js";

const catalog: MapCatalog = {
  version: 2,
  defaultMapId: "mock_qinghe_chiyan",
  maps: [
    { mapId: "fixture_north_pass", displayName: "Fixture", sourceRevision: 1, kind: "fixture" },
    { mapId: "mock_qinghe_chiyan", displayName: "Authoring", sourceRevision: 1, kind: "authoring" },
  ],
};

describe("initial map selection", () => {
  it("prefers an explicit valid URL selection, then a valid last-opened map", () => {
    expect(selectInitialMapId(catalog, "fixture_north_pass", "mock_qinghe_chiyan")).toBe("fixture_north_pass");
    expect(selectInitialMapId(catalog, undefined, "fixture_north_pass")).toBe("fixture_north_pass");
  });

  it("uses the declared authoring default instead of lexicographic catalog order", () => {
    expect(selectInitialMapId(catalog)).toBe("mock_qinghe_chiyan");
    expect(selectInitialMapId(catalog, "missing", "also_missing")).toBe("mock_qinghe_chiyan");
  });
});
