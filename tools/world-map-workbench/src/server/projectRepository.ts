import fs from "node:fs/promises";
import path from "node:path";
import sharp from "sharp";
import { createEmptyGeography, createDefaultProject } from "../shared/defaultProject.js";
import { geographyDocumentSchema, projectSchema } from "../shared/schema.js";
import type { GeographyDocument, TerrainChunkPayload, WorldProject } from "../shared/types.js";
import { compileRegionArtifacts, writeTerrainMask } from "./artifacts.js";
import { createPathPolicy, type ProjectPathPolicy } from "./pathPolicy.js";

const PROJECT_FILE = "workbench.project.json";
const GEOGRAPHY_FILE = "geography.json";

async function exists(filePath: string): Promise<boolean> {
  try {
    await fs.access(filePath);
    return true;
  } catch {
    return false;
  }
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

  public constructor(projectRoot: string) {
    this.paths = createPathPolicy(projectRoot);
  }

  public async loadBundle(): Promise<ProjectBundle> {
    const projectPath = this.paths.resolveConfig(PROJECT_FILE);
    const initialized = await exists(projectPath);
    const project = initialized
      ? projectSchema.parse(JSON.parse(await fs.readFile(projectPath, "utf8"))) as WorldProject
      : createDefaultProject();
    const geographyPath = this.paths.resolveConfig(GEOGRAPHY_FILE);
    const geography = (await exists(geographyPath))
      ? geographyDocumentSchema.parse(JSON.parse(await fs.readFile(geographyPath, "utf8"))) as GeographyDocument
      : createEmptyGeography();
    const terrainChunks = initialized ? await this.loadTerrainChunks(project) : [];
    return { initialized, project, geography, terrainChunks };
  }

  public async bootstrap(): Promise<ProjectBundle> {
    const existing = await this.loadBundle();
    if (existing.initialized) return existing;
    await Promise.all([
      atomicWriteJson(this.paths.resolveConfig(PROJECT_FILE), existing.project),
      atomicWriteJson(this.paths.resolveConfig(GEOGRAPHY_FILE), existing.geography),
    ]);
    await fs.mkdir(this.paths.textureRoot, { recursive: true });
    return { ...existing, initialized: true };
  }

  public async saveProject(input: unknown): Promise<WorldProject> {
    const project = projectSchema.parse(input) as WorldProject;
    await atomicWriteJson(this.paths.resolveConfig(PROJECT_FILE), project);
    return project;
  }

  public async saveGeography(input: unknown): Promise<GeographyDocument> {
    const geography = geographyDocumentSchema.parse(input) as GeographyDocument;
    await atomicWriteJson(this.paths.resolveConfig(GEOGRAPHY_FILE), geography);
    return geography;
  }

  public async saveTerrainChunks(payloads: TerrainChunkPayload[]): Promise<string[]> {
    const bundle = await this.loadBundle();
    const expectedWidth = bundle.project.chunk.width / bundle.project.chunk.terrainCellSize;
    const expectedHeight = bundle.project.chunk.height / bundle.project.chunk.terrainCellSize;
    const saved: string[] = [];
    const snapshots = new Map<string, Buffer | undefined>();
    try {
      for (const payload of payloads) {
        const chunk = bundle.project.chunks.find((candidate) => candidate.id === payload.chunkId);
        if (!chunk) throw new Error(`Unknown terrain chunk ${payload.chunkId}`);
        if (payload.width !== expectedWidth || payload.height !== expectedHeight) {
          throw new Error(`Terrain chunk ${payload.chunkId} dimensions do not match project contract`);
        }
        const cells = Buffer.from(payload.cellsBase64, "base64");
        if (cells.length !== payload.width * payload.height) {
          throw new Error(`Terrain chunk ${payload.chunkId} payload length does not match dimensions`);
        }
        const outputPath = this.paths.resolveTexture(chunk.terrainMaskPath);
        snapshots.set(outputPath, (await exists(outputPath)) ? await fs.readFile(outputPath) : undefined);
        await writeTerrainMask(outputPath, payload.width, payload.height, cells);
        saved.push(payload.chunkId);
      }
      return saved;
    } catch (error) {
      // A cross-chunk stroke is one logical edit, so restore every pre-save mask on any batch failure.
      await Promise.all([...snapshots.entries()].map(async ([filePath, contents]) => {
        if (contents) {
          await fs.mkdir(path.dirname(filePath), { recursive: true });
          await fs.writeFile(filePath, contents);
        } else {
          await fs.rm(filePath, { force: true });
        }
      }));
      throw error;
    }
  }

  public async compileRegions(input: unknown) {
    const geography = geographyDocumentSchema.parse(input) as GeographyDocument;
    const bundle = await this.loadBundle();
    const outputDirectory = this.paths.resolveTexture("masks/territory");
    return compileRegionArtifacts(outputDirectory, geography.regions, {
      width: bundle.project.world.width,
      height: bundle.project.world.height,
      maskScale: bundle.project.chunk.territoryMaskScale,
    });
  }

  private async loadTerrainChunks(project: WorldProject): Promise<TerrainChunkPayload[]> {
    const chunks: TerrainChunkPayload[] = [];
    const expectedWidth = project.chunk.width / project.chunk.terrainCellSize;
    const expectedHeight = project.chunk.height / project.chunk.terrainCellSize;
    for (const chunk of project.chunks) {
      const filePath = this.paths.resolveTexture(chunk.terrainMaskPath);
      if (!(await exists(filePath))) continue;
      const decoded = await sharp(filePath).extractChannel(0).raw().toBuffer({ resolveWithObject: true });
      if (decoded.info.width !== expectedWidth || decoded.info.height !== expectedHeight) {
        throw new Error(`Terrain mask grid mismatch chunk=${chunk.id} path=${filePath}`);
      }
      chunks.push({
        chunkId: chunk.id,
        width: decoded.info.width,
        height: decoded.info.height,
        cellsBase64: decoded.data.toString("base64"),
      });
    }
    return chunks;
  }
}
