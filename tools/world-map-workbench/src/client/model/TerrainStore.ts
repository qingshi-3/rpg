import { booleanPointInPolygon, point, polygon } from "@turf/turf";
import type { Coordinate, TerrainChunkPayload, ValidationItem, WorldProject } from "../../shared/types.js";

interface ChunkState {
  cells: Uint8Array;
  dirty: boolean;
}

export class TerrainStore {
  // The UI is continuous, but every addressed cell resolves to one manifest-owned chunk mask.
  private readonly project: WorldProject;
  private readonly cellsPerChunkX: number;
  private readonly cellsPerChunkY: number;
  private readonly chunks = new Map<string, ChunkState>();

  public constructor(project: WorldProject) {
    this.project = project;
    this.cellsPerChunkX = project.chunk.width / project.chunk.terrainCellSize;
    this.cellsPerChunkY = project.chunk.height / project.chunk.terrainCellSize;
    for (const chunk of project.chunks) {
      this.chunks.set(chunk.id, { cells: new Uint8Array(this.cellsPerChunkX * this.cellsPerChunkY), dirty: false });
    }
  }

  public loadChunk(chunkId: string, cells: Uint8Array, dirty = false): void {
    if (cells.length !== this.cellsPerChunkX * this.cellsPerChunkY) {
      throw new Error(`Terrain chunk ${chunkId} has invalid cell count ${cells.length}`);
    }
    if (!this.chunks.has(chunkId)) {
      throw new Error(`Unknown terrain chunk ${chunkId}`);
    }
    this.chunks.set(chunkId, { cells: new Uint8Array(cells), dirty });
  }

  public getTerrainIdAtWorld(x: number, y: number): number | undefined {
    const address = this.resolveAddress(x, y);
    return address ? address.state.cells[address.index] : undefined;
  }

  public setTerrainIdAtWorld(x: number, y: number, terrainId: number): void {
    const address = this.resolveAddress(x, y);
    if (!address || address.state.cells[address.index] === terrainId) return;
    address.state.cells[address.index] = terrainId;
    address.state.dirty = true;
  }

  public paintStroke(start: Coordinate, end: Coordinate, radius: number, terrainId: number): void {
    const distance = Math.hypot(end[0] - start[0], end[1] - start[1]);
    const step = Math.max(this.project.chunk.terrainCellSize / 2, radius / 3);
    const samples = Math.max(1, Math.ceil(distance / step));
    for (let sample = 0; sample <= samples; sample += 1) {
      const t = sample / samples;
      this.paintCircle([start[0] + (end[0] - start[0]) * t, start[1] + (end[1] - start[1]) * t], radius, terrainId);
    }
  }

  public paintCircle(center: Coordinate, radius: number, terrainId: number): void {
    const cellSize = this.project.chunk.terrainCellSize;
    const minX = Math.floor((center[0] - radius) / cellSize) * cellSize;
    const maxX = Math.ceil((center[0] + radius) / cellSize) * cellSize;
    const minY = Math.floor((center[1] - radius) / cellSize) * cellSize;
    const maxY = Math.ceil((center[1] + radius) / cellSize) * cellSize;
    for (let y = minY; y <= maxY; y += cellSize) {
      for (let x = minX; x <= maxX; x += cellSize) {
        const sampleX = x + cellSize / 2;
        const sampleY = y + cellSize / 2;
        if (Math.hypot(sampleX - center[0], sampleY - center[1]) <= radius) {
          this.setTerrainIdAtWorld(sampleX, sampleY, terrainId);
        }
      }
    }
  }

  public floodFill(start: Coordinate, terrainId: number): void {
    const cellSize = this.project.chunk.terrainCellSize;
    const startCellX = Math.floor(start[0] / cellSize);
    const startCellY = Math.floor(start[1] / cellSize);
    const sourceId = this.getTerrainIdAtWorld(startCellX * cellSize + cellSize / 2, startCellY * cellSize + cellSize / 2);
    if (sourceId === undefined || sourceId === terrainId) return;

    const maxX = Math.ceil(this.project.world.width / cellSize);
    const maxY = Math.ceil(this.project.world.height / cellSize);
    const queue: Array<[number, number]> = [[startCellX, startCellY]];
    const visited = new Set<string>();
    let queueIndex = 0;
    while (queueIndex < queue.length) {
      const current = queue[queueIndex++];
      if (!current) break;
      const [cellX, cellY] = current;
      const key = `${cellX},${cellY}`;
      if (visited.has(key) || cellX < 0 || cellY < 0 || cellX >= maxX || cellY >= maxY) continue;
      visited.add(key);
      const worldX = cellX * cellSize + cellSize / 2;
      const worldY = cellY * cellSize + cellSize / 2;
      if (this.getTerrainIdAtWorld(worldX, worldY) !== sourceId) continue;
      this.setTerrainIdAtWorld(worldX, worldY, terrainId);
      queue.push([cellX - 1, cellY], [cellX + 1, cellY], [cellX, cellY - 1], [cellX, cellY + 1]);
    }
  }

  public fillPolygon(ring: Coordinate[], terrainId: number): void {
    const area = polygon([ring]);
    const xs = ring.map((coordinate) => coordinate[0]);
    const ys = ring.map((coordinate) => coordinate[1]);
    const cellSize = this.project.chunk.terrainCellSize;
    const minX = Math.floor(Math.min(...xs) / cellSize) * cellSize;
    const maxX = Math.ceil(Math.max(...xs) / cellSize) * cellSize;
    const minY = Math.floor(Math.min(...ys) / cellSize) * cellSize;
    const maxY = Math.ceil(Math.max(...ys) / cellSize) * cellSize;
    for (let y = minY; y < maxY; y += cellSize) {
      for (let x = minX; x < maxX; x += cellSize) {
        const coordinate: Coordinate = [x + cellSize / 2, y + cellSize / 2];
        if (booleanPointInPolygon(point(coordinate), area)) {
          this.setTerrainIdAtWorld(coordinate[0], coordinate[1], terrainId);
        }
      }
    }
  }

  public getDirtyChunkIds(): string[] {
    return [...this.chunks.entries()].filter(([, state]) => state.dirty).map(([chunkId]) => chunkId);
  }

  public exportDirtyChunks(): TerrainChunkPayload[] {
    return this.getDirtyChunkIds().map((chunkId) => {
      const state = this.chunks.get(chunkId);
      if (!state) throw new Error(`Unknown chunk ${chunkId}`);
      return {
        chunkId,
        width: this.cellsPerChunkX,
        height: this.cellsPerChunkY,
        cellsBase64: this.toBase64(state.cells),
      };
    });
  }

  public exportAllChunks(): TerrainChunkPayload[] {
    return [...this.chunks.entries()].map(([chunkId, state]) => ({
      chunkId,
      width: this.cellsPerChunkX,
      height: this.cellsPerChunkY,
      cellsBase64: this.toBase64(state.cells),
    }));
  }

  public markSaved(chunkIds: string[]): void {
    for (const chunkId of chunkIds) {
      const state = this.chunks.get(chunkId);
      if (state) state.dirty = false;
    }
  }

  public forEachVisibleCell(
    extent: [number, number, number, number],
    callback: (worldX: number, worldY: number, terrainId: number) => void,
  ): void {
    const cellSize = this.project.chunk.terrainCellSize;
    const minX = Math.max(0, Math.floor(extent[0] / cellSize) * cellSize);
    const minY = Math.max(0, Math.floor(extent[1] / cellSize) * cellSize);
    const maxX = Math.min(this.project.world.width, Math.ceil(extent[2] / cellSize) * cellSize);
    const maxY = Math.min(this.project.world.height, Math.ceil(extent[3] / cellSize) * cellSize);
    for (let y = minY; y < maxY; y += cellSize) {
      for (let x = minX; x < maxX; x += cellSize) {
        callback(x, y, this.getTerrainIdAtWorld(x + cellSize / 2, y + cellSize / 2) ?? 0);
      }
    }
  }

  public validateTerrain(isolatedThreshold = 4): ValidationItem[] {
    const diagnostics: ValidationItem[] = [];
    const knownIds = new Set(this.project.terrainTypes.map((terrain) => terrain.id));
    let unclassified = 0;
    let unknown = 0;
    for (const state of this.chunks.values()) {
      for (const terrainId of state.cells) {
        if (terrainId === 0) unclassified += 1;
        else if (!knownIds.has(terrainId)) unknown += 1;
      }
    }
    if (unclassified > 0) {
      diagnostics.push({ code: "TERRAIN_UNCLASSIFIED", severity: "warning", message: `存在 ${unclassified} 个未分类地貌格`, layerId: "terrain" });
    }
    if (unknown > 0) {
      diagnostics.push({ code: "TERRAIN_UNKNOWN_ID", severity: "error", message: `存在 ${unknown} 个未知地貌 ID`, layerId: "terrain" });
    }

    const visited = new Set<string>();
    const cellSize = this.project.chunk.terrainCellSize;
    const maxX = Math.ceil(this.project.world.width / cellSize);
    const maxY = Math.ceil(this.project.world.height / cellSize);
    for (let y = 0; y < maxY; y += 1) {
      for (let x = 0; x < maxX; x += 1) {
        const startKey = `${x},${y}`;
        if (visited.has(startKey)) continue;
        const terrainId = this.getTerrainIdAtWorld(x * cellSize + cellSize / 2, y * cellSize + cellSize / 2) ?? 0;
        if (terrainId === 0 || !knownIds.has(terrainId)) {
          visited.add(startKey);
          continue;
        }
        const component: Array<[number, number]> = [];
        const queue: Array<[number, number]> = [[x, y]];
        let queueIndex = 0;
        while (queueIndex < queue.length) {
          const current = queue[queueIndex++];
          if (!current) break;
          const [cellX, cellY] = current;
          const key = `${cellX},${cellY}`;
          if (visited.has(key) || cellX < 0 || cellY < 0 || cellX >= maxX || cellY >= maxY) continue;
          const currentTerrain = this.getTerrainIdAtWorld(cellX * cellSize + cellSize / 2, cellY * cellSize + cellSize / 2) ?? 0;
          if (currentTerrain !== terrainId) continue;
          visited.add(key);
          component.push([cellX, cellY]);
          queue.push([cellX - 1, cellY], [cellX + 1, cellY], [cellX, cellY - 1], [cellX, cellY + 1]);
        }
        if (component.length > 0 && component.length <= isolatedThreshold) {
          const [cellX, cellY] = component[0]!;
          diagnostics.push({
            code: "TERRAIN_ISOLATED",
            severity: "warning",
            message: `发现 ${component.length} 格的孤立地貌区域，ID=${terrainId}`,
            coordinate: [cellX * cellSize + cellSize / 2, cellY * cellSize + cellSize / 2],
            layerId: "terrain",
          });
        }
      }
    }
    return diagnostics;
  }

  private resolveAddress(x: number, y: number): { state: ChunkState; index: number } | undefined {
    if (x < 0 || y < 0 || x >= this.project.world.width || y >= this.project.world.height) return undefined;
    const chunkX = Math.floor(x / this.project.chunk.width);
    const chunkY = Math.floor(y / this.project.chunk.height);
    const chunk = this.project.chunks.find((candidate) => candidate.coordinate[0] === chunkX && candidate.coordinate[1] === chunkY);
    if (!chunk) return undefined;
    const state = this.chunks.get(chunk.id);
    if (!state) return undefined;
    const localX = Math.floor((x - chunk.worldOrigin[0]) / this.project.chunk.terrainCellSize);
    const localY = Math.floor((y - chunk.worldOrigin[1]) / this.project.chunk.terrainCellSize);
    return { state, index: localY * this.cellsPerChunkX + localX };
  }

  private toBase64(cells: Uint8Array): string {
    if (typeof Buffer !== "undefined") return Buffer.from(cells).toString("base64");
    let binary = "";
    for (const value of cells) binary += String.fromCharCode(value);
    return btoa(binary);
  }
}
