import { describe, expect, it } from "vitest";
import { toolUiDefinitions, workspaceForTool, workspaceUiDefinitions, type ToolId } from "../src/client/workspaceUi.js";

describe("task workspace information architecture", () => {
  it("exposes the five accepted authoring workspaces in production order", () => {
    expect(workspaceUiDefinitions.map((workspace) => workspace.id)).toEqual([
      "terrain",
      "networks",
      "locations",
      "regions",
      "review",
    ]);
  });

  it("assigns every authoring tool to one and only one workspace", () => {
    const authoringTools = Object.keys(toolUiDefinitions).filter((tool) => tool !== "select") as ToolId[];
    const assignedTools = workspaceUiDefinitions.flatMap((workspace) => workspace.tools);

    expect(assignedTools).toHaveLength(new Set(assignedTools).size);
    expect(new Set(assignedTools)).toEqual(new Set(authoringTools));
    for (const tool of authoringTools) expect(workspaceForTool(tool)).toBeDefined();
  });

  it("gives every workspace a valid default and actionable guidance", () => {
    for (const workspace of workspaceUiDefinitions) {
      expect(workspace.defaultTool === "select" || workspace.tools.includes(workspace.defaultTool)).toBe(true);
      expect(workspace.description.length).toBeGreaterThan(10);
      expect(workspace.steps).toHaveLength(3);
      for (const step of workspace.steps) expect(step.length).toBeGreaterThan(4);
    }

    for (const tool of Object.values(toolUiDefinitions)) {
      expect(tool.instruction.length).toBeGreaterThan(10);
      expect(tool.nextStep.length).toBeGreaterThan(6);
    }
  });

  it("enters object workspaces in a safe selection state", () => {
    for (const workspaceId of ["networks", "locations", "regions", "review"] as const) {
      expect(workspaceUiDefinitions.find((workspace) => workspace.id === workspaceId)?.defaultTool).toBe("select");
    }
  });
});
