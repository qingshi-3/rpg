import type { GeographyDocument, MapCatalog, PublishedMapPackage, PublishProfile, TerrainChunkPayload, WorldProject } from "../shared/types.js";

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
  listMaps: () => request<MapCatalog>("/api/maps"),
  loadProject: (mapId?: string) => request<ProjectBundle>(mapId ? `/api/maps/${encodeURIComponent(mapId)}` : "/api/project"),
  bootstrap: (mapId: string, columns: number, rows: number) => request<ProjectBundle>("/api/project/bootstrap", {
    method: "POST",
    headers: { "content-type": "application/json" },
    body: JSON.stringify({ mapId, columns, rows }),
  }),
  createMap: (mapId: string, displayName: string, columns: number, rows: number) => request<ProjectBundle>("/api/maps", {
    method: "POST",
    headers: { "content-type": "application/json" },
    body: JSON.stringify({ mapId, displayName, columns, rows }),
  }),
  duplicateMap: (sourceMapId: string, mapId: string, displayName: string) => request<ProjectBundle>(`/api/maps/${encodeURIComponent(sourceMapId)}/duplicate`, {
    method: "POST",
    headers: { "content-type": "application/json" },
    body: JSON.stringify({ mapId, displayName }),
  }),
  expandGrid: (mapId: string, columns: number, rows: number) => request<{ ok: true; project: WorldProject }>(`/api/maps/${encodeURIComponent(mapId)}/grid`, {
    method: "POST",
    headers: { "content-type": "application/json" },
    body: JSON.stringify({ columns, rows }),
  }),
  saveProject: (project: WorldProject) => request<{ ok: true }>("/api/project", {
    method: "PUT",
    headers: { "content-type": "application/json" },
    body: JSON.stringify(project),
  }),
  saveGeography: (mapId: string, geography: GeographyDocument) => request<{ ok: true }>(`/api/geography?mapId=${encodeURIComponent(mapId)}`, {
    method: "PUT",
    headers: { "content-type": "application/json" },
    body: JSON.stringify(geography),
  }),
  saveTerrainMasks: (mapId: string, chunks: TerrainChunkPayload[]) => request<{ ok: true; savedChunkIds: string[] }>(`/api/terrain/masks?mapId=${encodeURIComponent(mapId)}`, {
    method: "POST",
    headers: { "content-type": "application/json" },
    body: JSON.stringify({ chunks }),
  }),
  compileLocationGeometries: (mapId: string, geography: GeographyDocument) => request<{ ok: true; outputs: Record<string, string> }>(`/api/location-geometries/compile?mapId=${encodeURIComponent(mapId)}`, {
    method: "POST",
    headers: { "content-type": "application/json" },
    body: JSON.stringify(geography),
  }),
  publish: (mapId: string, profile: PublishProfile) => request<{ ok: true; package: PublishedMapPackage }>(`/api/maps/${encodeURIComponent(mapId)}/publish`, {
    method: "POST",
    headers: { "content-type": "application/json" },
    body: JSON.stringify({ profile }),
  }),
};
