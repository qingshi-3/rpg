import type { GeographyDocument, TerrainChunkPayload, WorldProject } from "../shared/types.js";

export interface ProjectBundle {
  initialized: boolean;
  project: WorldProject;
  geography: GeographyDocument;
  terrainChunks: TerrainChunkPayload[];
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(path, init);
  const body = await response.json().catch(() => undefined) as { error?: string } | undefined;
  if (!response.ok) throw new Error(body?.error ?? `${response.status} ${response.statusText}`);
  return body as T;
}

export const workbenchApi = {
  loadProject: () => request<ProjectBundle>("/api/project"),
  bootstrap: () => request<ProjectBundle>("/api/project/bootstrap", { method: "POST" }),
  saveProject: (project: WorldProject) => request<{ ok: true }>("/api/project", {
    method: "PUT",
    headers: { "content-type": "application/json" },
    body: JSON.stringify(project),
  }),
  saveGeography: (geography: GeographyDocument) => request<{ ok: true }>("/api/geography", {
    method: "PUT",
    headers: { "content-type": "application/json" },
    body: JSON.stringify(geography),
  }),
  saveTerrainMasks: (chunks: TerrainChunkPayload[]) => request<{ ok: true; savedChunkIds: string[] }>("/api/terrain/masks", {
    method: "POST",
    headers: { "content-type": "application/json" },
    body: JSON.stringify({ chunks }),
  }),
  compileRegions: (geography: GeographyDocument) => request<{ ok: true; outputs: Record<string, string> }>("/api/regions/compile", {
    method: "POST",
    headers: { "content-type": "application/json" },
    body: JSON.stringify(geography),
  }),
};
