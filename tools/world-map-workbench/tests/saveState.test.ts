import { afterEach, describe, expect, it, vi } from "vitest";
import { WorkbenchApp } from "../src/client/WorkbenchApp.js";
import { workbenchApi } from "../src/client/api.js";
import type { GeographyDocument, TerrainChunkPayload, WorldProject } from "../src/shared/types.js";

interface SaveHarness {
  saving: boolean;
  mutationBefore: undefined;
  mutationRevision: number;
  initialized: boolean;
  project: WorldProject;
  terrain: {
    exportDirtyChunks: () => TerrainChunkPayload[];
    markSaved: (chunkIds: string[]) => void;
  };
  saveAll: () => Promise<void>;
  setSaving: (saving: boolean) => void;
  setStatus: (message: string, kind: string) => void;
  setDirty: (dirty: boolean) => void;
  getGeography: () => GeographyDocument;
}

afterEach(() => {
  vi.restoreAllMocks();
});

function createHarness() {
  const app = Object.create(WorkbenchApp.prototype) as SaveHarness;
  app.saving = false;
  app.mutationBefore = undefined;
  app.mutationRevision = 4;
  app.initialized = true;
  app.project = {} as WorldProject;
  app.terrain = {
    exportDirtyChunks: vi.fn(() => []),
    markSaved: vi.fn(),
  };
  app.setSaving = vi.fn((saving: boolean) => { app.saving = saving; });
  app.setStatus = vi.fn();
  app.setDirty = vi.fn();
  app.getGeography = vi.fn(() => ({} as GeographyDocument));
  vi.spyOn(workbenchApi, "saveProject").mockResolvedValue({ ok: true });
  return app;
}

describe("save state coordination", () => {
  it("clears dirty state when the saved revision is still current", async () => {
    const app = createHarness();
    vi.spyOn(workbenchApi, "saveGeography").mockResolvedValue({ ok: true });

    await app.saveAll();

    expect(app.setDirty).toHaveBeenCalledWith(false);
    expect(app.saving).toBe(false);
  });

  it("keeps dirty state when a newer mutation appears before the request finishes", async () => {
    const app = createHarness();
    let resolveGeography!: (value: { ok: true }) => void;
    const pendingGeography = new Promise<{ ok: true }>((resolve) => { resolveGeography = resolve; });
    vi.spyOn(workbenchApi, "saveGeography").mockReturnValue(pendingGeography);

    const saving = app.saveAll();
    app.mutationRevision += 1;
    resolveGeography({ ok: true });
    await saving;

    expect(app.setDirty).not.toHaveBeenCalledWith(false);
    expect(app.setStatus).toHaveBeenCalledWith("保存完成，但期间出现了新修改，请再次保存", "warning");
    expect(app.saving).toBe(false);
  });
});
