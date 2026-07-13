import type { ChunkDefinition, WorldProject } from "./types.js";

export const FIXED_NEW_MAP_CHUNK_SIZE = 1024;
export const MAX_GRID_AXIS = 32;
export const MAX_GRID_CHUNKS = 256;

export interface MapGridDimensions {
  columns: number;
  rows: number;
}

export function validateMapGridDimensions(columns: number, rows: number): MapGridDimensions {
  if (!Number.isInteger(columns) || !Number.isInteger(rows) || columns < 1 || rows < 1) {
    throw new RangeError("Chunk columns and rows must be positive integers");
  }
  if (columns > MAX_GRID_AXIS || rows > MAX_GRID_AXIS || columns * rows > MAX_GRID_CHUNKS) {
    throw new RangeError(`Chunk grid exceeds the supported limit: axis<=${MAX_GRID_AXIS}, chunks<=${MAX_GRID_CHUNKS}`);
  }
  return { columns, rows };
}

export function createGridChunk(x: number, y: number, chunkWidth: number, chunkHeight: number): ChunkDefinition {
  const id = `chunk_${x}_${y}`;
  return {
    id,
    coordinate: [x, y],
    worldOrigin: [x * chunkWidth, y * chunkHeight],
    terrainMaskPath: `masks/terrain/${id}.png`,
    territoryMaskPath: `masks/territory/${id}.png`,
    navigationScenePath: `res://scenes/world/navigation/${id}.tscn`,
  };
}

export function getProjectGridDimensions(project: WorldProject): MapGridDimensions {
  const columns = project.world.width / project.chunk.width;
  const rows = project.world.height / project.chunk.height;
  validateMapGridDimensions(columns, rows);
  if (project.chunks.length !== columns * rows) {
    throw new Error(`Project Chunk grid is incomplete: expected=${columns * rows} actual=${project.chunks.length}`);
  }

  const byCoordinate = new Map<string, ChunkDefinition>();
  for (const chunk of project.chunks) {
    const key = `${chunk.coordinate[0]},${chunk.coordinate[1]}`;
    if (byCoordinate.has(key)) throw new Error(`Project Chunk grid has duplicate coordinate ${key}`);
    byCoordinate.set(key, chunk);
  }
  for (let y = 0; y < rows; y += 1) {
    for (let x = 0; x < columns; x += 1) {
      const chunk = byCoordinate.get(`${x},${y}`);
      if (!chunk) throw new Error(`Project Chunk grid is missing coordinate ${x},${y}`);
      if (chunk.worldOrigin[0] !== x * project.chunk.width || chunk.worldOrigin[1] !== y * project.chunk.height) {
        throw new Error(`Project Chunk ${chunk.id} would relocate from its derived origin`);
      }
    }
  }
  return { columns, rows };
}

export function expandProjectGrid(project: WorldProject, targetColumns: number, targetRows: number): WorldProject {
  const target = validateMapGridDimensions(targetColumns, targetRows);
  const current = getProjectGridDimensions(project);
  if (target.columns < current.columns || target.rows < current.rows) {
    throw new RangeError(`Chunk grid shrinking or relocation is unsupported: current=${current.columns}x${current.rows} requested=${target.columns}x${target.rows}`);
  }
  if (target.columns === current.columns && target.rows === current.rows) {
    throw new RangeError(`Chunk grid is already ${current.columns}x${current.rows}`);
  }

  const expanded = structuredClone(project);
  const occupiedIds = new Set(expanded.chunks.map((chunk) => chunk.id));
  const occupiedCoordinates = new Set(expanded.chunks.map((chunk) => chunk.coordinate.join(",")));
  for (let y = 0; y < target.rows; y += 1) {
    for (let x = 0; x < target.columns; x += 1) {
      if (occupiedCoordinates.has(`${x},${y}`)) continue;
      const chunk = createGridChunk(x, y, project.chunk.width, project.chunk.height);
      if (occupiedIds.has(chunk.id)) throw new Error(`New derived ChunkId conflicts with existing identity: ${chunk.id}`);
      expanded.chunks.push(chunk);
      occupiedIds.add(chunk.id);
      occupiedCoordinates.add(`${x},${y}`);
    }
  }
  expanded.world = {
    width: target.columns * project.chunk.width,
    height: target.rows * project.chunk.height,
  };
  return expanded;
}
