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

  it("retains layer ownership when a stable id is temporarily blank", () => {
    const app = Object.create(WorkbenchApp.prototype) as InteractionHarness;
    const location = new Feature({ locationId: "", locationType: "city" });
    const region = new Feature({ regionId: "", cityId: "city_a", role: "territory" });

    expect(app.featureLayerId(location)).toBe("strategic-locations");
    expect(app.featureLayerId(region)).toBe("territories");
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
    const feature = new Feature({ locationId: "city_a", locationType: "city" });

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
      new Feature({ locationId: "city_a", locationType: "city" }),
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
