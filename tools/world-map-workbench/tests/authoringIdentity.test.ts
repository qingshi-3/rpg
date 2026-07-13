import { featureCollection, point } from "@turf/turf";
import { describe, expect, it } from "vitest";
import { AuthoringIdentityFactory } from "../src/client/model/AuthoringIdentityFactory.js";
import type { GeographyDocument, StrategicLocationFeature } from "../src/shared/types.js";

function geographyWithIds(): Pick<GeographyDocument, "provinces" | "strategicLocations"> {
  return {
    provinces: [{ provinceId: "province_repeat", name: "既有省份", layoutId: "layout_repeat" }],
    strategicLocations: featureCollection([
      point([10, 10], { locationId: "location_repeat", name: "既有城市", locationType: "main-city", provinceId: "province_repeat" }) as StrategicLocationFeature,
    ]),
  };
}

describe("authoring identity factory", () => {
  it("retries collisions and reserves every generated identity inside one map", () => {
    const tokens = ["repeat", "province-new", "repeat", "city-new", "repeat", "layout-new", "city-new", "aux-new"];
    const factory = new AuthoringIdentityFactory(geographyWithIds(), () => tokens.shift() ?? "fallback");

    expect(factory.createProvinceCityIdentity()).toEqual({
      provinceId: "province_provincenew",
      locationId: "location_citynew",
      layoutId: "layout_layoutnew",
    });
    expect(factory.createLocationId()).toBe("location_auxnew");
  });

  it("does not derive stable identities from display names or geometry", () => {
    const factory = new AuthoringIdentityFactory(geographyWithIds(), () => "ABC-123");
    expect(factory.createProvinceCityIdentity()).toEqual({
      provinceId: "province_abc123",
      locationId: "location_abc123",
      layoutId: "layout_abc123",
    });
  });

  it("fails explicitly when an entropy source cannot produce a free identity", () => {
    const factory = new AuthoringIdentityFactory(geographyWithIds(), () => "repeat", 2);
    expect(() => factory.createProvinceCityIdentity()).toThrow("Unable to generate a unique province identity");
  });
});
