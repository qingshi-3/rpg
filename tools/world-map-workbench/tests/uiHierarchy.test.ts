import fs from "node:fs/promises";
import { describe, expect, it } from "vitest";
import { featureCollection, point } from "@turf/turf";
import { WorkbenchApp } from "../src/client/WorkbenchApp.js";
import { createEmptyGeography } from "../src/shared/defaultProject.js";
import type { GeographyDocument, StrategicLocationFeature } from "../src/shared/types.js";

describe("workbench UI hierarchy and responsive ownership", () => {
  it("keeps unified province and city-region creation inside the city-region workspace", async () => {
    const source = await fs.readFile(new URL("../src/client/WorkbenchApp.ts", import.meta.url), "utf8");
    const toolbar = source.slice(source.indexOf('<header class="toolbar">'), source.indexOf("</header>", source.indexOf('<header class="toolbar">')));
    const provinceCreation = source.slice(source.indexOf("private createProvince"), source.indexOf("private deleteProvince"));

    expect(toolbar).not.toContain("new-province");
    expect(toolbar).not.toContain("edit-province");
    expect(toolbar).not.toContain("delete-province");
    expect(source).toContain('data-ui-scope="city-regions-provinces"');
    expect(source).toContain('data-province-action="create"');
    expect(source).toContain('data-province-action="add-auxiliary"');
    expect(source).toContain('data-province-action="cancel-pending"');
    expect(source).not.toContain('data-province-action="edit"');
    expect(provinceCreation).not.toContain("window.prompt");
    expect(provinceCreation).toContain("AuthoringIdentityFactory");
    expect(provinceCreation).toContain('locationType: "main-city"');
    expect(provinceCreation).toContain('locationType: "auxiliary-city"');
    expect(source).toContain("centroid(authoredGeometry)");
    expect(source).toContain("高级身份信息（只读）");
    expect(source).toContain("省份拥有的 LayoutId");
    expect(source).toContain("辅城成员");
  });

  it("uses structured grid forms and refreshes OpenLayers from real resize signals", async () => {
    const source = await fs.readFile(new URL("../src/client/WorkbenchApp.ts", import.meta.url), "utf8");
    const styles = await fs.readFile(new URL("../src/client/styles.css", import.meta.url), "utf8");

    for (const id of ["new-map-form", "new-map-id", "new-map-name", "new-map-columns", "new-map-rows", "map-settings-form", "map-settings-columns", "map-settings-rows"]) {
      expect(source).toContain(`id="${id}"`);
    }
    expect(source).toContain("new ResizeObserver");
    expect(source).toContain('window.addEventListener("resize"');
    expect(source).toContain("this.map.updateSize()");
    expect(styles).toContain("html, body, #app { width: 100%; height: 100%");
    expect(styles).toContain(".map { position: absolute; inset: 0; }");
  });

  it("renders the selected main/auxiliary hierarchy with the dashed add card last", () => {
    const geography = createEmptyGeography();
    geography.provinces = [{ provinceId: "province_a", name: "甲州", layoutId: "layout_a" }];
    geography.strategicLocations = featureCollection([
      point([10, 10], { provinceId: "province_a", locationId: "city_main", name: "甲城", locationType: "main-city" }) as StrategicLocationFeature,
      point([20, 20], { provinceId: "province_a", locationId: "city_aux", name: "乙城", locationType: "auxiliary-city" }) as StrategicLocationFeature,
    ]);
    const app = Object.create(WorkbenchApp.prototype) as unknown as {
      provinces: GeographyDocument["provinces"];
      selectedLocationProvinceId: string;
      selectedGeometryLocationId: string;
      pendingCityRegion?: unknown;
      getGeography: () => GeographyDocument;
      renderProvinceManagementHtml: () => string;
    };
    app.provinces = geography.provinces;
    app.selectedLocationProvinceId = "province_a";
    app.selectedGeometryLocationId = "city_aux";
    app.getGeography = () => geography;

    const html = app.renderProvinceManagementHtml();
    expect(html).toContain('class="city-card city-card-main ');
    expect(html).toContain('data-city-location-id="city_main" aria-pressed="false"');
    expect(html).toContain('data-city-location-id="city_aux" aria-pressed="true"');
    expect(html).toContain('class="city-add-card"');
    expect(html).not.toContain("选择并编辑名称");
    expect(html.indexOf('data-city-location-id="city_main"')).toBeLessThan(html.indexOf('data-city-location-id="city_aux"'));
    expect(html.indexOf('data-city-location-id="city_aux"')).toBeLessThan(html.indexOf('class="city-add-card"'));
  });
});
