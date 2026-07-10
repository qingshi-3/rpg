import { describe, expect, it, vi } from "vitest";
import { WorkbenchApp } from "../src/client/WorkbenchApp.js";

interface KeyboardHarness {
  saving: boolean;
  handleGlobalKeyDown: (event: KeyboardEvent) => void;
  saveAll: () => Promise<void>;
  undo: () => void;
  redo: () => void;
  deleteSelectedFeature: () => void;
  activateTool: () => void;
  activeTool: "select";
}

describe("global keyboard shortcuts", () => {
  it("commits the focused editor before capturing a Ctrl+S save", () => {
    const app = Object.create(WorkbenchApp.prototype) as KeyboardHarness;
    const calls: string[] = [];
    const target = {
      matches: () => true,
      blur: vi.fn(() => calls.push("blur")),
    };
    const event = {
      target,
      ctrlKey: true,
      metaKey: false,
      key: "s",
      preventDefault: vi.fn(),
    } as unknown as KeyboardEvent;
    app.saving = false;
    app.activeTool = "select";
    app.saveAll = vi.fn(async () => { calls.push("save"); });
    app.undo = vi.fn();
    app.redo = vi.fn();
    app.deleteSelectedFeature = vi.fn();
    app.activateTool = vi.fn();

    app.handleGlobalKeyDown(event);

    expect(event.preventDefault).toHaveBeenCalledOnce();
    expect(calls).toEqual(["blur", "save"]);
  });

  it("does not run object shortcuts while an editor owns the keyboard", () => {
    const app = Object.create(WorkbenchApp.prototype) as KeyboardHarness;
    const event = {
      target: { matches: () => true, blur: vi.fn() },
      ctrlKey: false,
      metaKey: false,
      key: "Delete",
      preventDefault: vi.fn(),
    } as unknown as KeyboardEvent;
    app.saving = false;
    app.activeTool = "select";
    app.saveAll = vi.fn(async () => undefined);
    app.undo = vi.fn();
    app.redo = vi.fn();
    app.deleteSelectedFeature = vi.fn();
    app.activateTool = vi.fn();

    app.handleGlobalKeyDown(event);

    expect(app.deleteSelectedFeature).not.toHaveBeenCalled();
  });
});
