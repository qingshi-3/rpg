import { describe, expect, it, vi } from "vitest";
import { WorkbenchApp } from "../src/client/WorkbenchApp.js";
import type { LayerId, ValidationItem } from "../src/shared/types.js";
import type { ToolId, WorkspaceId } from "../src/client/workspaceUi.js";

interface DiagnosticHarness {
  activeWorkspace: WorkspaceId;
  layers: { byId: Map<LayerId, { setVisible: (visible: boolean) => void }> };
  map: { getView: () => { getZoom: () => number; animate: (options: unknown) => void } };
  locateDiagnostic: (item: ValidationItem) => void;
  getLayerDefinition: (id: LayerId) => { visible: boolean };
  renderLayers: () => void;
  activateTool: (tool: ToolId) => void;
  setStatus: (message: string, severity: string) => void;
}

function createHarness() {
  const setVisible = vi.fn();
  const animate = vi.fn();
  const activateTool = vi.fn();
  const definition = { visible: false };
  const app = Object.create(WorkbenchApp.prototype) as DiagnosticHarness;
  app.activeWorkspace = "review";
  app.layers = { byId: new Map([["terrain", { setVisible }]]) };
  app.map = { getView: () => ({ getZoom: () => 1, animate }) };
  app.getLayerDefinition = vi.fn(() => definition);
  app.renderLayers = vi.fn();
  app.activateTool = activateTool;
  app.setStatus = vi.fn();
  return { app, activateTool, animate, definition, setVisible };
}

describe("diagnostic workspace navigation", () => {
  it("synchronizes a terrain workspace without requiring an object id", () => {
    const { app, activateTool, definition, setVisible } = createHarness();

    app.locateDiagnostic({
      code: "TERRAIN_UNCLASSIFIED",
      severity: "error",
      message: "存在未分类地貌",
      layerId: "terrain",
    });

    expect(app.activeWorkspace).toBe("terrain");
    expect(activateTool).toHaveBeenCalledOnce();
    expect(activateTool).toHaveBeenCalledWith("select");
    expect(definition.visible).toBe(true);
    expect(setVisible).toHaveBeenCalledWith(true);
  });

  it("centers an isolated terrain problem after entering safe selection mode", () => {
    const { app, activateTool, animate } = createHarness();

    app.locateDiagnostic({
      code: "TERRAIN_ISOLATED",
      severity: "warning",
      message: "存在孤立地貌",
      layerId: "terrain",
      coordinate: [128, 256],
    });

    expect(activateTool).toHaveBeenCalledWith("select");
    expect(animate).toHaveBeenCalledOnce();
  });
});
