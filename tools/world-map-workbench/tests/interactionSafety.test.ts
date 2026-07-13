import { describe, expect, it, vi } from "vitest";
import Feature from "ol/Feature.js";
import type Draw from "ol/interaction/Draw.js";
import { WorkbenchApp } from "../src/client/WorkbenchApp.js";
import type { LayerId } from "../src/shared/types.js";

interface InteractionHarness {
  mutationBefore: unknown;
  openDrawer: "inspector" | undefined;
  bindDrawMutation: (interaction: Draw) => void;
  finishNewFeature: (feature: Feature, label: string) => void;
  finishMutation: () => void;
  captureSnapshot: () => unknown;
  featureLayerId: (feature: Feature) => LayerId | undefined;
  clearSelectionForLayer: (layerId: LayerId) => boolean;
  select: { getFeatures: () => { getArray: () => Feature[]; clear: () => void; push: (feature: Feature) => void } };
  renderProperties: () => void;
  setDrawer: (drawer: undefined) => void;
  activateTool: () => void;
  setStatus: () => void;
}

interface PendingRollbackHarness {
  pendingCityRegion: unknown;
  mutationBefore: unknown;
  selectedLocationProvinceId: string;
  selectedGeometryLocationId: string;
  layers: { byId: Map<LayerId, { setVisible: (visible: boolean) => void }> };
  cancelPendingCityRegion: (message: string) => void;
  restoreSnapshot: (snapshot: unknown) => void;
  restoreDirtyState: (dirty: boolean, revision: number) => void;
  getLayerDefinition: (id: LayerId) => { visible: boolean };
  renderLayers: () => void;
  renderWorkspaceContext: () => void;
  setStatus: (message: string, kind: string) => void;
}

interface SelectionSyncHarness {
  provinces: Array<{ provinceId: string; name: string; layoutId: string }>;
  selectedLocationProvinceId: string;
  selectedGeometryLocationId: string;
  layers: { locationSource: { getFeatures: () => Feature[] } };
  select: { getFeatures: () => { item: (index: number) => Feature | undefined } };
  activateWorkspace: (workspace: string, preserveMapSelection?: boolean) => void;
  synchronizeCityWorkspaceFromFeature: (feature: Feature) => boolean;
  handleMapSelectionChanged: () => void;
  renderProperties: () => void;
}

describe("authoring interaction safety", () => {
  it("clears an unfinished mutation when a Draw sketch is aborted", () => {
    const app = Object.create(WorkbenchApp.prototype) as InteractionHarness;
    const handlers = new Map<string, () => void>();
    const interaction = {
      on: vi.fn((type: string, handler: () => void) => { handlers.set(type, handler); }),
    } as unknown as Draw;
    const snapshot = { geography: {}, terrainChunks: [] };
    app.mutationBefore = undefined;
    app.captureSnapshot = vi.fn(() => snapshot);

    app.bindDrawMutation(interaction);
    handlers.get("drawstart")?.();
    expect(app.mutationBefore).toBe(snapshot);

    handlers.get("drawabort")?.();
    expect(app.mutationBefore).toBeUndefined();
  });

  it("rolls a pending province and city draw back without committing history", () => {
    const app = Object.create(WorkbenchApp.prototype) as PendingRollbackHarness;
    const before = { geography: { provinces: [] }, terrainChunks: [] };
    const territoryVisibility = vi.fn();
    const locationVisibility = vi.fn();
    const definitions = new Map<LayerId, { visible: boolean }>([
      ["territories", { visible: false }],
      ["strategic-locations", { visible: true }],
    ]);
    app.pendingCityRegion = {
      before,
      location: { locationId: "location_pending", name: "location_pending", locationType: "main-city", provinceId: "province_pending" },
      provinceWasCreated: true,
      previousProvinceId: "province_existing",
      previousGeometryLocationId: "location_existing",
      previousDirty: false,
      previousMutationRevision: 7,
      previousLayerVisibility: { territories: false, "strategic-locations": true },
    };
    app.mutationBefore = before;
    app.selectedLocationProvinceId = "province_pending";
    app.selectedGeometryLocationId = "location_pending";
    app.layers = { byId: new Map([
      ["territories", { setVisible: territoryVisibility }],
      ["strategic-locations", { setVisible: locationVisibility }],
    ]) };
    app.restoreSnapshot = vi.fn();
    app.restoreDirtyState = vi.fn();
    app.getLayerDefinition = vi.fn((id) => definitions.get(id)!);
    app.renderLayers = vi.fn();
    app.renderWorkspaceContext = vi.fn();
    app.setStatus = vi.fn();

    app.cancelPendingCityRegion("cancelled");

    expect(app.pendingCityRegion).toBeUndefined();
    expect(app.mutationBefore).toBeUndefined();
    expect(app.restoreSnapshot).toHaveBeenCalledWith(before);
    expect(app.selectedLocationProvinceId).toBe("province_existing");
    expect(app.selectedGeometryLocationId).toBe("location_existing");
    expect(territoryVisibility).toHaveBeenCalledWith(false);
    expect(locationVisibility).toHaveBeenCalledWith(true);
    expect(app.restoreDirtyState).toHaveBeenCalledWith(false, 7);
  });

  it("retains layer ownership when a stable id is temporarily blank", () => {
    const app = Object.create(WorkbenchApp.prototype) as InteractionHarness;
    const location = new Feature({ locationId: "", locationType: "main-city", provinceId: "province_a" });
    const region = new Feature({ locationId: "", provinceId: "province_a", direction: "center" });

    expect(app.featureLayerId(location)).toBe("strategic-locations");
    expect(app.featureLayerId(region)).toBe("territories");
  });

  it("synchronizes region and marker selections without replacing the clicked map feature", () => {
    const app = Object.create(WorkbenchApp.prototype) as SelectionSyncHarness;
    const mainMarker = new Feature({ locationId: "city_main", locationType: "main-city", provinceId: "province_a", name: "甲城" });
    const auxiliaryMarker = new Feature({ locationId: "city_aux", locationType: "auxiliary-city", provinceId: "province_a", name: "乙城" });
    const auxiliaryRegion = new Feature({ locationId: "city_aux", provinceId: "province_a", direction: "east" });
    app.provinces = [{ provinceId: "province_a", name: "甲州", layoutId: "layout_a" }];
    app.layers = { locationSource: { getFeatures: () => [mainMarker, auxiliaryMarker] } };
    app.activateWorkspace = vi.fn();

    expect(app.synchronizeCityWorkspaceFromFeature(auxiliaryRegion)).toBe(true);
    expect(app.selectedLocationProvinceId).toBe("province_a");
    expect(app.selectedGeometryLocationId).toBe("city_aux");
    expect(app.activateWorkspace).toHaveBeenLastCalledWith("regions", true);

    expect(app.synchronizeCityWorkspaceFromFeature(mainMarker)).toBe(true);
    expect(app.selectedGeometryLocationId).toBe("city_main");
    expect(app.activateWorkspace).toHaveBeenLastCalledWith("regions", true);
  });

  it("preserves the last province and city context when the map selection becomes empty", () => {
    const app = Object.create(WorkbenchApp.prototype) as SelectionSyncHarness;
    app.selectedLocationProvinceId = "province_keep";
    app.selectedGeometryLocationId = "city_keep";
    app.select = { getFeatures: () => ({ item: () => undefined }) };
    app.synchronizeCityWorkspaceFromFeature = vi.fn();
    app.renderProperties = vi.fn();

    app.handleMapSelectionChanged();

    expect(app.synchronizeCityWorkspaceFromFeature).not.toHaveBeenCalled();
    expect(app.selectedLocationProvinceId).toBe("province_keep");
    expect(app.selectedGeometryLocationId).toBe("city_keep");
    expect(app.renderProperties).toHaveBeenCalledOnce();
  });

  it("defers vector refresh until after the drawend insertion stack", async () => {
    const app = Object.create(WorkbenchApp.prototype) as InteractionHarness;
    const calls: string[] = [];
    const selected: Feature[] = [];
    app.finishMutation = vi.fn(() => calls.push("refresh"));
    app.activateTool = vi.fn(() => calls.push("select-tool"));
    app.select = { getFeatures: () => ({
      getArray: () => selected,
      clear: () => { selected.length = 0; },
      push: (feature) => { selected.push(feature); },
    }) };
    app.renderProperties = vi.fn(() => calls.push("properties"));
    app.setStatus = vi.fn(() => calls.push("status"));
    const feature = new Feature({ locationId: "city_a", locationType: "main-city", provinceId: "province_a" });

    app.finishNewFeature(feature, "战略地点");
    expect(calls).toEqual([]);

    await Promise.resolve();
    expect(calls).toEqual(["refresh", "select-tool", "properties", "status"]);
    expect(selected).toEqual([feature]);
  });

  it("clears the full multiselection when any selected object belongs to a locked layer", () => {
    const app = Object.create(WorkbenchApp.prototype) as InteractionHarness;
    const selected = [
      new Feature({ featureId: "road_a", featureType: "road" }),
      new Feature({ locationId: "city_a", locationType: "main-city", provinceId: "province_a" }),
    ];
    const clear = vi.fn(() => { selected.length = 0; });
    app.openDrawer = "inspector";
    app.select = { getFeatures: () => ({ getArray: () => selected, clear, push: (feature) => { selected.push(feature); } }) };
    app.renderProperties = vi.fn();
    app.setDrawer = vi.fn();

    expect(app.clearSelectionForLayer("strategic-locations")).toBe(true);
    expect(clear).toHaveBeenCalledOnce();
    expect(selected).toHaveLength(0);
    expect(app.setDrawer).toHaveBeenCalledWith(undefined);
  });
});
