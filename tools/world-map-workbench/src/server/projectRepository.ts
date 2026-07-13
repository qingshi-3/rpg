import crypto from "node:crypto";
import fs from "node:fs/promises";
import path from "node:path";
import sharp from "sharp";
import { createEmptyGeography, createDefaultProject } from "../shared/defaultProject.js";
import { expandProjectGrid, getProjectGridDimensions, validateMapGridDimensions } from "../shared/mapGrid.js";
import { geographyDocumentSchema, geographyDraftSchema, projectSchema } from "../shared/schema.js";
import type { ChunkDefinition, GeographyDocument, MapCatalog, MapCatalogEntry, TerrainChunkPayload, WorldProject } from "../shared/types.js";
import { compileLocationGeometryArtifacts, writeTerrainMask } from "./artifacts.js";
import { assertMapId, createPathPolicy, type ProjectPathPolicy } from "./pathPolicy.js";

const PROJECT_FILE = "workbench.project.json";
const GEOGRAPHY_FILE = "geography.json";
const CATALOG_FILE = "catalog.json";

async function exists(filePath: string): Promise<boolean> {
  try { await fs.access(filePath); return true; } catch { return false; }
}

async function atomicWriteJson(filePath: string, value: unknown): Promise<void> {
  await fs.mkdir(path.dirname(filePath), { recursive: true });
  const temporaryPath = `${filePath}.${process.pid}.${Date.now()}.tmp`;
  await fs.writeFile(temporaryPath, `${JSON.stringify(value, null, 2)}\n`, "utf8");
  await fs.rename(temporaryPath, filePath);
}

export interface ProjectBundle {
  initialized: boolean;
  project: WorldProject;
  geography: GeographyDocument;
  terrainChunks: TerrainChunkPayload[];
}

export class ProjectRepository {
  public readonly paths: ProjectPathPolicy;

  public constructor(projectRoot: string) { this.paths = createPathPolicy(projectRoot); }

  public async listMaps(): Promise<MapCatalog> {
    const catalogPath = this.paths.resolveConfig(path.join("maps", CATALOG_FILE));
    if (!(await exists(catalogPath))) return { version: 2, maps: [] };
    const parsed = JSON.parse(await fs.readFile(catalogPath, "utf8")) as Record<string, unknown>;
    if (!Array.isArray(parsed.maps)) throw new Error(`Invalid map catalog path=${catalogPath}`);

    // Version 1 is a read-only migration boundary; the next catalog mutation persists explicit editor routing metadata.
    const legacy = parsed.version === 1;
    if (!legacy && parsed.version !== 2) throw new Error(`Unsupported map catalog version path=${catalogPath}`);
    const maps = parsed.maps.map((value) => {
      const entry = value as Partial<MapCatalogEntry>;
      assertMapId(String(entry.mapId ?? ""));
      if (!entry.displayName || !Number.isInteger(entry.sourceRevision) || Number(entry.sourceRevision) < 1) {
        throw new Error(`Invalid map catalog entry MapId=${String(entry.mapId ?? "")}`);
      }
      const kind = legacy ? (entry.mapId === "fixture_north_pass" ? "fixture" : "authoring") : entry.kind;
      if (kind !== "authoring" && kind !== "fixture") throw new Error(`Invalid map catalog kind MapId=${entry.mapId}`);
      return { mapId: entry.mapId!, displayName: entry.displayName, sourceRevision: entry.sourceRevision!, kind };
    });
    const defaultMapId = legacy
      ? (maps.find((entry) => entry.mapId === "mock_qinghe_chiyan" && entry.kind === "authoring") ?? maps.find((entry) => entry.kind === "authoring"))?.mapId
      : typeof parsed.defaultMapId === "string" ? parsed.defaultMapId : undefined;
    if (!legacy && maps.some((entry) => entry.kind === "authoring") && !defaultMapId) {
      throw new Error(`Map catalog requires an explicit default authoring map path=${catalogPath}`);
    }
    if (defaultMapId && !maps.some((entry) => entry.mapId === defaultMapId && entry.kind === "authoring")) {
      throw new Error(`Map catalog default must identify an authoring map: ${defaultMapId}`);
    }
    return { version: 2, defaultMapId, maps };
  }

  public async createMap(mapId: string, displayName: string, columns: number, rows: number): Promise<ProjectBundle> {
    assertMapId(mapId);
    validateMapGridDimensions(columns, rows);
    const catalog = await this.listMaps();
    if (catalog.maps.some((entry) => entry.mapId === mapId)) throw new Error(`MapId already exists: ${mapId}`);
    const project = createDefaultProject(mapId, String(displayName ?? "").trim() || mapId, columns, rows);
    const geography = createEmptyGeography();
    const token = crypto.randomUUID().replaceAll("-", "");
    const configTarget = path.join(this.paths.mapSourceRoot, mapId);
    const textureTarget = path.join(this.paths.textureRoot, "maps", mapId);
    const configStage = path.join(this.paths.mapSourceRoot, `staging_${token}`);
    const textureStage = path.join(this.paths.textureRoot, "maps", `staging_${token}`);
    if (await exists(configTarget) || await exists(textureTarget)) throw new Error(`MapId filesystem ownership already exists: ${mapId}`);

    let configCommitted = false;
    let textureCommitted = false;
    try {
      await Promise.all([
        atomicWriteJson(path.join(configStage, "source", PROJECT_FILE), project),
        atomicWriteJson(path.join(configStage, "source", GEOGRAPHY_FILE), geography),
        this.writeBlankTerrainMasks(textureStage, project, project.chunks),
      ]);
      await fs.rename(configStage, configTarget);
      configCommitted = true;
      await fs.rename(textureStage, textureTarget);
      textureCommitted = true;
      catalog.maps.push({ mapId, displayName: project.displayName, sourceRevision: 1, kind: "authoring" });
      catalog.maps.sort((left, right) => left.mapId.localeCompare(right.mapId));
      catalog.defaultMapId ??= mapId;
      await atomicWriteJson(this.paths.resolveConfig(path.join("maps", CATALOG_FILE)), catalog);
    } catch (error) {
      await Promise.all([
        fs.rm(configCommitted ? configTarget : configStage, { recursive: true, force: true }),
        fs.rm(textureCommitted ? textureTarget : textureStage, { recursive: true, force: true }),
      ]);
      throw error;
    }
    return { initialized: true, project, geography, terrainChunks: [] };
  }

  public async duplicateMap(sourceMapId: string, targetMapId: string, displayName: string): Promise<ProjectBundle> {
    const source = await this.loadBundle(sourceMapId);
    const grid = getProjectGridDimensions(source.project);
    const created = await this.createMap(targetMapId, displayName, grid.columns, grid.rows);
    const project: WorldProject = structuredClone(source.project);
    project.mapId = targetMapId;
    project.displayName = created.project.displayName;
    await Promise.all([
      atomicWriteJson(this.paths.resolveMapSource(targetMapId, PROJECT_FILE), project),
      atomicWriteJson(this.paths.resolveMapSource(targetMapId, GEOGRAPHY_FILE), source.geography),
      fs.cp(this.paths.resolveMapTexture(sourceMapId, "draft"), this.paths.resolveMapTexture(targetMapId, "draft"), { recursive: true, force: true }),
    ]);
    return this.loadBundle(targetMapId);
  }

  public async loadBundle(mapId?: string): Promise<ProjectBundle> {
    const catalog = await this.listMaps();
    const selected = mapId
      ? catalog.maps.find((entry) => entry.mapId === assertMapId(mapId))
      : catalog.maps.find((entry) => entry.mapId === catalog.defaultMapId);
    if (mapId && !selected) throw new Error(`Unknown MapId: ${mapId}`);
    if (!selected) {
      const project = createDefaultProject();
      return { initialized: false, project, geography: createEmptyGeography(), terrainChunks: [] };
    }
    const project = projectSchema.parse(JSON.parse(await fs.readFile(this.paths.resolveMapSource(selected.mapId, PROJECT_FILE), "utf8"))) as WorldProject;
    if (project.mapId !== selected.mapId) throw new Error(`Map source identity mismatch catalog=${selected.mapId} source=${project.mapId}`);
    const geography = geographyDraftSchema.parse(JSON.parse(await fs.readFile(this.paths.resolveMapSource(selected.mapId, GEOGRAPHY_FILE), "utf8"))) as GeographyDocument;
    return { initialized: true, project, geography, terrainChunks: await this.loadTerrainChunks(project) };
  }

  public async bootstrap(mapId: string, columns: number, rows: number): Promise<ProjectBundle> {
    const catalog = await this.listMaps();
    return catalog.maps.length > 0 ? this.loadBundle(mapId && catalog.maps.some((entry) => entry.mapId === mapId) ? mapId : undefined) : this.createMap(mapId, "新地图草稿", columns, rows);
  }

  public async expandGrid(mapId: string, columns: number, rows: number): Promise<WorldProject> {
    const bundle = await this.loadBundle(mapId);
    const expanded = projectSchema.parse(expandProjectGrid(bundle.project, columns, rows)) as WorldProject;
    const existingIds = new Set(bundle.project.chunks.map((chunk) => chunk.id));
    const additions = expanded.chunks.filter((chunk) => !existingIds.has(chunk.id));
    const projectPath = this.paths.resolveMapSource(mapId, PROJECT_FILE);
    const originalProject = await fs.readFile(projectPath);
    const token = crypto.randomUUID().replaceAll("-", "");
    const stagedMasks = additions.map((chunk) => {
      const target = this.paths.resolveMapTexture(mapId, path.join("draft", chunk.terrainMaskPath!));
      return { chunk, target, stage: `${target}.${token}.tmp.png` };
    });
    for (const { target } of stagedMasks) {
      if (await exists(target)) throw new Error(`Grid expansion refuses to overwrite existing Chunk media: ${target}`);
    }

    const committedMasks: string[] = [];
    let projectCommitted = false;
    try {
      const width = expanded.chunk.width / expanded.chunk.terrainCellSize;
      const height = expanded.chunk.height / expanded.chunk.terrainCellSize;
      await Promise.all(stagedMasks.map(({ stage }) => writeTerrainMask(stage, width, height, Buffer.alloc(width * height))));
      for (const { stage, target } of stagedMasks) {
        await fs.rename(stage, target);
        committedMasks.push(target);
      }
      await atomicWriteJson(projectPath, expanded);
      projectCommitted = true;
      await this.bumpCatalog(mapId, expanded.displayName);
      return expanded;
    } catch (error) {
      if (projectCommitted) await atomicWriteJson(projectPath, JSON.parse(originalProject.toString("utf8")));
      await Promise.all([
        ...committedMasks.map((filePath) => fs.rm(filePath, { force: true })),
        ...stagedMasks.map(({ stage }) => fs.rm(stage, { force: true })),
      ]);
      throw error;
    }
  }

  public async saveProject(mapId: string, input: unknown): Promise<WorldProject> {
    const project = projectSchema.parse(input) as WorldProject;
    if (project.mapId !== assertMapId(mapId)) throw new Error(`MapId is immutable expected=${mapId} actual=${project.mapId}`);
    await atomicWriteJson(this.paths.resolveMapSource(mapId, PROJECT_FILE), project);
    await this.bumpCatalog(mapId, project.displayName);
    return project;
  }

  public async saveGeography(mapId: string, input: unknown): Promise<GeographyDocument> {
    const geography = geographyDraftSchema.parse(input) as GeographyDocument;
    await atomicWriteJson(this.paths.resolveMapSource(assertMapId(mapId), GEOGRAPHY_FILE), geography);
    await this.bumpCatalog(mapId);
    return geography;
  }

  public async saveTerrainChunks(mapId: string, payloads: TerrainChunkPayload[]): Promise<string[]> {
    const bundle = await this.loadBundle(mapId);
    const expectedWidth = bundle.project.chunk.width / bundle.project.chunk.terrainCellSize;
    const expectedHeight = bundle.project.chunk.height / bundle.project.chunk.terrainCellSize;
    const saved: string[] = [];
    const snapshots = new Map<string, Buffer | undefined>();
    try {
      for (const payload of payloads) {
        const chunk = bundle.project.chunks.find((candidate) => candidate.id === payload.chunkId);
        if (!chunk) throw new Error(`Unknown terrain chunk ${payload.chunkId}`);
        if (payload.width !== expectedWidth || payload.height !== expectedHeight) throw new Error(`Terrain chunk ${payload.chunkId} dimensions do not match project contract`);
        const cells = Buffer.from(payload.cellsBase64, "base64");
        if (cells.length !== payload.width * payload.height) throw new Error(`Terrain chunk ${payload.chunkId} payload length does not match dimensions`);
        const outputPath = this.paths.resolveMapTexture(mapId, path.join("draft", chunk.terrainMaskPath!));
        snapshots.set(outputPath, (await exists(outputPath)) ? await fs.readFile(outputPath) : undefined);
        await writeTerrainMask(outputPath, payload.width, payload.height, cells);
        saved.push(payload.chunkId);
      }
      return saved;
    } catch (error) {
      await Promise.all([...snapshots.entries()].map(async ([filePath, contents]) => contents ? fs.writeFile(filePath, contents) : fs.rm(filePath, { force: true })));
      throw error;
    }
  }

  public async compileLocationGeometries(mapId: string, input: unknown) {
    const geography = geographyDocumentSchema.parse(input) as GeographyDocument;
    const bundle = await this.loadBundle(mapId);
    const outputDirectory = this.paths.resolveMapTexture(mapId, path.join("draft", "regions"));
    return compileLocationGeometryArtifacts(outputDirectory, geography.locationGeometries, {
      width: bundle.project.world.width,
      height: bundle.project.world.height,
      maskScale: bundle.project.chunk.territoryMaskScale,
      chunks: bundle.project.chunks.map((chunk) => ({ id: chunk.id, worldOrigin: chunk.worldOrigin, width: bundle.project.chunk.width, height: bundle.project.chunk.height })),
    });
  }

  private async bumpCatalog(mapId: string, displayName?: string): Promise<void> {
    const catalog = await this.listMaps();
    const entry = catalog.maps.find((candidate) => candidate.mapId === mapId);
    if (!entry) throw new Error(`Unknown MapId: ${mapId}`);
    entry.sourceRevision += 1;
    if (displayName) entry.displayName = displayName;
    await atomicWriteJson(this.paths.resolveConfig(path.join("maps", CATALOG_FILE)), catalog);
  }

  private async loadTerrainChunks(project: WorldProject): Promise<TerrainChunkPayload[]> {
    const chunks: TerrainChunkPayload[] = [];
    const expectedWidth = project.chunk.width / project.chunk.terrainCellSize;
    const expectedHeight = project.chunk.height / project.chunk.terrainCellSize;
    for (const chunk of project.chunks) {
      const filePath = this.paths.resolveMapTexture(project.mapId, path.join("draft", chunk.terrainMaskPath!));
      if (!(await exists(filePath))) continue;
      const decoded = await sharp(filePath).extractChannel(0).raw().toBuffer({ resolveWithObject: true });
      if (decoded.info.width !== expectedWidth || decoded.info.height !== expectedHeight) throw new Error(`Terrain mask grid mismatch chunk=${chunk.id} path=${filePath}`);
      chunks.push({ chunkId: chunk.id, width: decoded.info.width, height: decoded.info.height, cellsBase64: decoded.data.toString("base64") });
    }
    return chunks;
  }

  private async writeBlankTerrainMasks(textureMapRoot: string, project: WorldProject, chunks: ChunkDefinition[]): Promise<void> {
    const width = project.chunk.width / project.chunk.terrainCellSize;
    const height = project.chunk.height / project.chunk.terrainCellSize;
    const cells = Buffer.alloc(width * height);
    await Promise.all(chunks.map((chunk) => writeTerrainMask(path.join(textureMapRoot, "draft", chunk.terrainMaskPath!), width, height, cells)));
  }
}

export type { MapCatalogEntry };
