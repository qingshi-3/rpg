import { describe, expect, it } from "vitest";
import { resolveCityWorkspaceSelection } from "../src/client/cityWorkspaceSelection.js";

const provinces = [
  { provinceId: "province_a", name: "甲州", layoutId: "layout_a" },
  { provinceId: "province_b", name: "乙州", layoutId: "layout_b" },
];

const locations = [
  { provinceId: "province_a", locationId: "city_main", name: "甲城", locationType: "main-city" as const },
  { provinceId: "province_a", locationId: "city_aux", name: "乙城", locationType: "auxiliary-city" as const },
  { provinceId: "province_b", locationId: "gate_b", name: "乙关", locationType: "gate" as const },
];

describe("city workspace selection identity", () => {
  it("resolves main and auxiliary region clicks from ProvinceId plus LocationId", () => {
    expect(resolveCityWorkspaceSelection({ provinceId: "province_a", locationId: "city_main", direction: "center" }, provinces, locations)).toEqual({
      provinceId: "province_a", locationId: "city_main", locationType: "main-city",
    });
    expect(resolveCityWorkspaceSelection({ provinceId: "province_a", locationId: "city_aux", direction: "east" }, provinces, locations)).toEqual({
      provinceId: "province_a", locationId: "city_aux", locationType: "auxiliary-city",
    });
  });

  it("resolves city marker clicks but rejects non-city and mismatched identities", () => {
    expect(resolveCityWorkspaceSelection({ provinceId: "province_a", locationId: "city_aux", locationType: "auxiliary-city" }, provinces, locations)?.locationId).toBe("city_aux");
    expect(resolveCityWorkspaceSelection({ provinceId: "province_b", locationId: "gate_b", locationType: "gate" }, provinces, locations)).toBeUndefined();
    expect(resolveCityWorkspaceSelection({ provinceId: "province_b", locationId: "city_main", direction: "center" }, provinces, locations)).toBeUndefined();
  });
});
