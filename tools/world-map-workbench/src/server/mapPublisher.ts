import crypto from "node:crypto";
import fs from "node:fs/promises";
import path from "node:path";
import sharp from "sharp";
import { geographyDocumentSchema, projectSchema } from "../shared/schema.js";
import type { GeographyDocument, PublishedMapPackage, PublishProfile, WorldProject } from "../shared/types.js";
import { compileLocationGeometryArtifacts } from "./artifacts.js";
import type { ProjectRepository } from "./projectRepository.js";

async function exists(filePath: string): Promise<boolean> {
  try { await fs.access(filePath); return true; } catch { return false; }
}

const PACKAGE_SCHEMA_VERSION = 2 as const;

type ArtifactHash = PublishedMapPackage["artifactHashes"][number];

function compareOrdinal(left: string, right: string): number {
  return left < right ? -1 : left > right ? 1 : 0;
}

async function sha256File(filePath: string): Promise<string> {
  return crypto.createHash("sha256").update(await fs.readFile(filePath)).digest("hex");
}

function aggregateArtifactHash(entries: ArtifactHash[]): string {
  const canonical = [...entries]
    .sort((left, right) => compareOrdinal(left.kind, right.kind) || compareOrdinal(left.artifactId, right.artifactId))
    .map((entry) => `${entry.kind}\0${entry.artifactId}\0${entry.sha256}\n`)
    .join("");
  return crypto.createHash("sha256").update(canonical, "utf8").digest("hex");
}

function resourcePath(projectRoot: string, filePath: string): string {
  return `res://${path.relative(projectRoot, filePath).replaceAll("\\", "/")}`;
}

function resolveVisualSource(projectRoot: string, visualPath: string): string {
  const normalized = visualPath.replaceAll("\\", "/");
  const resolved = normalized.startsWith("res://")
    ? path.resolve(projectRoot, normalized.slice(6))
    : path.resolve(projectRoot, "assets", "textures", "world", normalized);
  const root = path.resolve(projectRoot);
  if (resolved !== root && !resolved.startsWith(`${root}${path.sep}`)) throw new Error(`Visual source escapes project root: ${visualPath}`);
  return resolved;
}

function resolveProjectResource(projectRoot: string, resourcePath: string): string {
  if (!resourcePath.startsWith("res://")) throw new Error(`Runtime resource path must use res://: ${resourcePath}`);
  const resolved = path.resolve(projectRoot, resourcePath.slice(6));
  const root = path.resolve(projectRoot);
  if (resolved !== root && !resolved.startsWith(`${root}${path.sep}`)) throw new Error(`Runtime resource escapes project root: ${resourcePath}`);
  return resolved;
}

function uidForPath(sourcePath: string): string {
  const alphabet = "abcdefghijklmnopqrstuvwxy012345678";
  const digest = crypto.createHash("sha256").update(`StrategicMapPackageImportUID\0${sourcePath}`).digest();
  let value = digest.readBigUInt64BE(0) & ((1n << 63n) - 1n);
  let encoded = "";
  do { encoded = alphabet[Number(value % 34n)]! + encoded; value /= 34n; } while (value > 0n);
  return `uid://${encoded}`;
}

async function writeTextureImport(texturePath: string, publishedTexturePath: string, projectRoot: string, mipmaps: boolean): Promise<void> {
  const source = resourcePath(projectRoot, publishedTexturePath);
  const cacheHash = crypto.createHash("md5").update(source).digest("hex");
  const cachePath = `res://.godot/imported/${path.basename(texturePath)}-${cacheHash}.ctex`;
  const contents = `[remap]\n\nimporter="texture"\ntype="CompressedTexture2D"\nuid="${uidForPath(source)}"\npath="${cachePath}"\nmetadata={\n"vram_texture": false\n}\n\n[deps]\n\nsource_file="${source}"\ndest_files=["${cachePath}"]\n\n[params]\n\ncompress/mode=0\ncompress/high_quality=false\ncompress/lossy_quality=0.7\ncompress/uastc_level=0\ncompress/rdo_quality_loss=0.0\ncompress/hdr_compression=1\ncompress/normal_map=0\ncompress/channel_pack=0\nmipmaps/generate=${mipmaps}\nmipmaps/limit=-1\nroughness/mode=0\nroughness/src_normal=""\nprocess/channel_remap/red=0\nprocess/channel_remap/green=1\nprocess/channel_remap/blue=2\nprocess/channel_remap/alpha=3\nprocess/fix_alpha_border=true\nprocess/premult_alpha=false\nprocess/normal_map_invert_y=false\nprocess/hdr_as_srgb=false\nprocess/hdr_clamp_exposure=false\nprocess/size_limit=0\ndetect_3d/compress_to=1\n`;
  await fs.writeFile(`${texturePath}.import`, contents, "utf8");
}

export class MapPublisher {
  public constructor(private readonly repository: ProjectRepository) {}

  public async publish(mapId: string, profile: PublishProfile): Promise<PublishedMapPackage> {
    if (!(["visual-preview", "region-interactive", "strategic-runtime"] as string[]).includes(profile)) throw new Error(`Unknown publish profile: ${String(profile)}`);
    const bundle = await this.repository.loadBundle(mapId);
    const project = projectSchema.parse(bundle.project) as WorldProject;
    const geography = profile === "visual-preview"
      ? bundle.geography
      : geographyDocumentSchema.parse(bundle.geography) as GeographyDocument;
    const visualSources = new Map<string, string>();
    const expectedColumns = project.world.width / project.chunk.width;
    const expectedRows = project.world.height / project.chunk.height;
    if (!Number.isInteger(expectedColumns) || !Number.isInteger(expectedRows) || project.chunks.length !== expectedColumns * expectedRows) {
      throw new Error(`Chunk grid does not exactly cover world MapId=${mapId}`);
    }
    for (const chunk of project.chunks) {
      if (!chunk.visualTexturePath) throw new Error(`Publish profile=${profile} requires visual chunk=${chunk.id}`);
      const source = resolveVisualSource(this.repository.paths.projectRoot, chunk.visualTexturePath);
      if (!(await exists(source))) throw new Error(`Publish visual is missing chunk=${chunk.id} path=${chunk.visualTexturePath}`);
      const metadata = await sharp(source).metadata();
      if (metadata.width !== project.chunk.width || metadata.height !== project.chunk.height) {
        throw new Error(`Publish visual dimensions mismatch chunk=${chunk.id} expected=${project.chunk.width}x${project.chunk.height} actual=${metadata.width}x${metadata.height}`);
      }
      visualSources.set(chunk.id, source);
    }
    const terrainSources = new Map<string, string>();
    const navigationSources = new Map<string, string>();
    if (profile === "strategic-runtime") {
      for (const chunk of project.chunks) {
        const terrainPath = this.repository.paths.resolveMapTexture(mapId, path.join("draft", chunk.terrainMaskPath!));
        if (!(await exists(terrainPath))) throw new Error(`Strategic runtime publish requires terrain mask chunk=${chunk.id}`);
        const terrainMetadata = await sharp(terrainPath).metadata();
        const expectedWidth = project.chunk.width / project.chunk.terrainCellSize;
        const expectedHeight = project.chunk.height / project.chunk.terrainCellSize;
        if (terrainMetadata.width !== expectedWidth || terrainMetadata.height !== expectedHeight) throw new Error(`Terrain mask dimensions mismatch chunk=${chunk.id}`);
        terrainSources.set(chunk.id, terrainPath);
        if (!chunk.navigationScenePath) {
          throw new Error(`Strategic runtime publish requires navigation scene chunk=${chunk.id}`);
        }
        const navigationPath = resolveProjectResource(this.repository.paths.projectRoot, chunk.navigationScenePath);
        if (!(await exists(navigationPath))) throw new Error(`Strategic runtime publish requires navigation scene chunk=${chunk.id}`);
        navigationSources.set(chunk.id, navigationPath);
      }
    }

    const visualHashes = Object.fromEntries(await Promise.all([...visualSources.entries()].map(async ([chunkId, filePath]) => [
      chunkId,
      crypto.createHash("sha256").update(await fs.readFile(filePath)).digest("hex"),
    ])));
    const terrainHashes = Object.fromEntries(await Promise.all([...terrainSources.entries()].map(async ([chunkId, filePath]) => [
      chunkId,
      crypto.createHash("sha256").update(await fs.readFile(filePath)).digest("hex"),
    ])));
    const navigationHashes = Object.fromEntries(await Promise.all([...navigationSources.entries()].map(async ([chunkId, filePath]) => [
      chunkId,
      crypto.createHash("sha256").update(await fs.readFile(filePath)).digest("hex"),
    ])));
    const sourceHash = crypto.createHash("sha256").update(JSON.stringify({
      packageSchemaVersion: PACKAGE_SCHEMA_VERSION,
      integrityContract: "sha256-artifact-set-v1",
      project,
      geography,
      profile,
      visualHashes,
      terrainHashes,
      navigationHashes,
    })).digest("hex");
    const revision = `r-${sourceHash.slice(0, 16)}`;
    const configRevision = this.repository.paths.resolvePublished(mapId, revision);
    const assetRevision = this.repository.paths.resolveMapTexture(mapId, revision);
    const configStage = `${configRevision}.staging-${process.pid}-${Date.now()}`;
    const assetStage = `${assetRevision}.staging-${process.pid}-${Date.now()}`;
    await Promise.all([fs.mkdir(configStage, { recursive: true }), fs.mkdir(assetStage, { recursive: true })]);
    try {
      const publishedProject: WorldProject = structuredClone(project);
      for (const chunk of publishedProject.chunks) {
        const target = path.join(assetStage, "visual", `${chunk.id}.png`);
        await fs.mkdir(path.dirname(target), { recursive: true });
        await fs.copyFile(visualSources.get(chunk.id)!, target);
        chunk.visualTexturePath = resourcePath(this.repository.paths.projectRoot, path.join(assetRevision, "visual", `${chunk.id}.png`));
        chunk.referenceTexturePath = undefined;
        if (profile === "strategic-runtime") {
          const terrainTarget = path.join(assetStage, "terrain", `${chunk.id}.png`);
          await fs.mkdir(path.dirname(terrainTarget), { recursive: true });
          await fs.copyFile(terrainSources.get(chunk.id)!, terrainTarget);
          chunk.terrainMaskPath = resourcePath(this.repository.paths.projectRoot, path.join(assetRevision, "terrain", `${chunk.id}.png`));
          const navigationTarget = path.join(configStage, "navigation", `${chunk.id}.tscn`);
          await fs.mkdir(path.dirname(navigationTarget), { recursive: true });
          await fs.copyFile(navigationSources.get(chunk.id)!, navigationTarget);
          chunk.navigationScenePath = resourcePath(this.repository.paths.projectRoot, path.join(configRevision, "navigation", `${chunk.id}.tscn`));
        } else {
          chunk.terrainMaskPath = undefined;
          chunk.navigationScenePath = undefined;
        }
        if (profile === "visual-preview") chunk.territoryMaskPath = undefined;
      }

      let regionOutputs: Awaited<ReturnType<typeof compileLocationGeometryArtifacts>> | undefined;
      if (profile !== "visual-preview") {
        regionOutputs = await compileLocationGeometryArtifacts(path.join(assetStage, "regions"), geography.locationGeometries, {
          width: project.world.width,
          height: project.world.height,
          maskScale: project.chunk.territoryMaskScale,
          chunks: project.chunks.map((chunk) => ({ id: chunk.id, worldOrigin: chunk.worldOrigin, width: project.chunk.width, height: project.chunk.height })),
        });
        for (const chunk of publishedProject.chunks) {
          chunk.territoryMaskPath = resourcePath(this.repository.paths.projectRoot, path.join(assetRevision, "regions", "chunks", `${chunk.id}.png`));
        }
      }

      const projectPath = path.join(configStage, "chunk-manifest.json");
      const geographyPath = path.join(configStage, "geography.json");
      await Promise.all([
        fs.writeFile(projectPath, `${JSON.stringify(publishedProject, null, 2)}\n`, "utf8"),
        fs.writeFile(geographyPath, `${JSON.stringify(geography, null, 2)}\n`, "utf8"),
      ]);
      const capabilities = profile === "visual-preview" ? ["visual"] : profile === "strategic-runtime" ? ["visual", "regions", "strategic-runtime"] : ["visual", "regions"];
      const artifactHashes: ArtifactHash[] = [
        { kind: "chunk-manifest", artifactId: "manifest", sha256: await sha256File(projectPath) },
        { kind: "geography", artifactId: "geography", sha256: await sha256File(geographyPath) },
      ];
      for (const chunk of project.chunks) {
        artifactHashes.push({ kind: "visual", artifactId: chunk.id, sha256: await sha256File(path.join(assetStage, "visual", `${chunk.id}.png`)) });
        if (regionOutputs) artifactHashes.push({ kind: "region-mask", artifactId: chunk.id, sha256: await sha256File(path.join(assetStage, "regions", "chunks", `${chunk.id}.png`)) });
        if (profile === "strategic-runtime") {
          artifactHashes.push({ kind: "terrain", artifactId: chunk.id, sha256: await sha256File(path.join(assetStage, "terrain", `${chunk.id}.png`)) });
          artifactHashes.push({ kind: "navigation", artifactId: chunk.id, sha256: await sha256File(path.join(configStage, "navigation", `${chunk.id}.tscn`)) });
        }
      }
      if (regionOutputs) {
        artifactHashes.push({ kind: "region-lookup", artifactId: "lookup", sha256: await sha256File(regionOutputs.lookupPath) });
        artifactHashes.push({ kind: "region-outlines", artifactId: "outlines", sha256: await sha256File(regionOutputs.outlinesPath) });
      }
      artifactHashes.sort((left, right) => compareOrdinal(left.kind, right.kind) || compareOrdinal(left.artifactId, right.artifactId));
      const packageDocument: PublishedMapPackage = {
        schemaVersion: PACKAGE_SCHEMA_VERSION,
        mapId,
        revision,
        compatibilityRevision: 1,
        contentHash: aggregateArtifactHash(artifactHashes),
        publishProfile: profile,
        capabilities,
        chunkManifestPath: resourcePath(this.repository.paths.projectRoot, path.join(configRevision, "chunk-manifest.json")),
        geographyPath: resourcePath(this.repository.paths.projectRoot, path.join(configRevision, "geography.json")),
        artifactHashes,
        regionArtifacts: regionOutputs ? {
          encoding: "rgb24-location-code-v1",
          lookupPath: resourcePath(this.repository.paths.projectRoot, path.join(assetRevision, "regions", "region_lookup.json")),
          outlinesPath: resourcePath(this.repository.paths.projectRoot, path.join(assetRevision, "regions", "region_outlines.json")),
          chunks: project.chunks.map((chunk, index) => ({
            chunkId: chunk.id,
            worldOrigin: chunk.worldOrigin,
            worldWidth: project.chunk.width,
            worldHeight: project.chunk.height,
            maskTexturePath: resourcePath(this.repository.paths.projectRoot, path.join(assetRevision, "regions", "chunks", `${chunk.id}.png`)),
          })),
        } : undefined,
      };
      const packagePath = path.join(configStage, "package.json");
      await fs.writeFile(packagePath, `${JSON.stringify(packageDocument, null, 2)}\n`, "utf8");

      for (const chunk of project.chunks) {
        await writeTextureImport(path.join(assetStage, "visual", `${chunk.id}.png`), path.join(assetRevision, "visual", `${chunk.id}.png`), this.repository.paths.projectRoot, true);
        if (regionOutputs) await writeTextureImport(path.join(assetStage, "regions", "chunks", `${chunk.id}.png`), path.join(assetRevision, "regions", "chunks", `${chunk.id}.png`), this.repository.paths.projectRoot, false);
        if (profile === "strategic-runtime") await writeTextureImport(path.join(assetStage, "terrain", `${chunk.id}.png`), path.join(assetRevision, "terrain", `${chunk.id}.png`), this.repository.paths.projectRoot, false);
      }

      if (regionOutputs && (!(await exists(regionOutputs.lookupPath)) || regionOutputs.maskPaths.length !== project.chunks.length)) throw new Error(`Published region artifacts are incomplete MapId=${mapId}`);
      if (!(await exists(configRevision))) await fs.rename(configStage, configRevision); else await fs.rm(configStage, { recursive: true, force: true });
      if (!(await exists(assetRevision))) await fs.rename(assetStage, assetRevision); else await fs.rm(assetStage, { recursive: true, force: true });
      const pointer = { version: 1, mapId, revision, packageManifestPath: resourcePath(this.repository.paths.projectRoot, path.join(configRevision, "package.json")) };
      const pointerPath = this.repository.paths.resolvePublished(mapId, "current.json");
      const pointerStage = `${pointerPath}.${process.pid}.${Date.now()}.tmp`;
      await fs.writeFile(pointerStage, `${JSON.stringify(pointer, null, 2)}\n`, "utf8");
      await fs.rename(pointerStage, pointerPath);
      return packageDocument;
    } catch (error) {
      await Promise.all([fs.rm(configStage, { recursive: true, force: true }), fs.rm(assetStage, { recursive: true, force: true })]);
      throw error;
    }
  }
}
