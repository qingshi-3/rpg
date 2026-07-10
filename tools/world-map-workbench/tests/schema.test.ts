import { describe, expect, it } from "vitest";
import { createDefaultProject } from "../src/shared/defaultProject.js";
import { projectSchema } from "../src/shared/schema.js";

describe("project schema", () => {
  it("accepts the bootstrap project", () => {
    expect(projectSchema.parse(createDefaultProject()).version).toBe(1);
  });

  it("rejects duplicate chunk ids and coordinates", () => {
    const project = createDefaultProject();
    project.chunks[1] = {
      ...project.chunks[0]!,
      worldOrigin: [project.chunk.width, 0],
    };

    const result = projectSchema.safeParse(project);
    expect(result.success).toBe(false);
    expect(result.error?.issues.map((issue) => issue.message).join(" ")).toContain("Duplicate chunk id");
  });

  it("rejects unknown or missing accepted layers", () => {
    const project = createDefaultProject();
    project.layers = project.layers.filter((layer) => layer.id !== "validation");
    project.layers.push({
      id: "not-authoritative" as never,
      label: "bad",
      kind: "derived",
      visible: true,
      locked: true,
      opacity: 1,
    });

    expect(projectSchema.safeParse(project).success).toBe(false);
  });
});
