import express, { type NextFunction, type Request, type Response } from "express";
import { ZodError } from "zod";
import type { TerrainChunkPayload } from "../shared/types.js";
import { ProjectRepository } from "./projectRepository.js";
import { MapPublisher } from "./mapPublisher.js";
import type { PublishProfile } from "../shared/types.js";

export interface AppOptions {
  projectRoot: string;
  quiet?: boolean;
}

export function createApp(options: AppOptions) {
  const app = express();
  const repository = new ProjectRepository(options.projectRoot);
  const publisher = new MapPublisher(repository);
  const log = (message: string) => {
    if (!options.quiet) console.log(`[world-map-workbench] ${message}`);
  };

  app.disable("x-powered-by");
  app.use(express.json({ limit: "64mb" }));

  app.get("/api/health", (_request, response) => {
    response.json({ ok: true, projectRoot: repository.paths.projectRoot });
  });

  app.get("/api/maps", async (_request, response) => {
    response.json(await repository.listMaps());
  });

  app.post("/api/maps", async (request, response) => {
    response.status(201).json(await repository.createMap(request.body?.mapId, request.body?.displayName, request.body?.columns, request.body?.rows));
  });

  app.post("/api/maps/:mapId/duplicate", async (request, response) => {
    response.status(201).json(await repository.duplicateMap(request.params.mapId, request.body?.mapId, request.body?.displayName));
  });

  app.post("/api/maps/:mapId/grid", async (request, response) => {
    const project = await repository.expandGrid(request.params.mapId, request.body?.columns, request.body?.rows);
    log(`expanded Chunk grid MapId=${project.mapId} world=${project.world.width}x${project.world.height}`);
    response.json({ ok: true, project });
  });

  app.get("/api/maps/:mapId", async (request, response) => {
    response.json(await repository.loadBundle(request.params.mapId));
  });

  app.get("/api/project", async (request, response) => {
    response.json(await repository.loadBundle(typeof request.query.mapId === "string" ? request.query.mapId : undefined));
  });

  app.post("/api/project/bootstrap", async (request, response) => {
    const bundle = await repository.bootstrap(request.body?.mapId, request.body?.columns, request.body?.rows);
    log(`bootstrapped project root=${repository.paths.projectRoot}`);
    response.status(201).json(bundle);
  });

  app.put("/api/project", async (request, response) => {
    const mapId = request.body?.mapId;
    const project = await repository.saveProject(mapId, request.body);
    log(`saved project MapId=${project.mapId}`);
    response.json({ ok: true, project });
  });

  app.put("/api/geography", async (request, response) => {
    const mapId = String(request.query.mapId ?? "");
    const geography = await repository.saveGeography(mapId, request.body);
    log(`saved geography provinces=${geography.provinces.length} lines=${geography.linearFeatures.features.length} locations=${geography.strategicLocations.features.length} geometries=${geography.locationGeometries.features.length}`);
    response.json({ ok: true });
  });

  app.post("/api/terrain/masks", async (request, response) => {
    const payloads = request.body?.chunks as TerrainChunkPayload[] | undefined;
    if (!Array.isArray(payloads)) throw new Error("Terrain mask request requires a chunks array");
    const savedChunkIds = await repository.saveTerrainChunks(String(request.query.mapId ?? ""), payloads);
    log(`saved terrain masks chunks=${savedChunkIds.join(",")}`);
    response.json({ ok: true, savedChunkIds });
  });

  app.post("/api/location-geometries/compile", async (request, response) => {
    const outputs = await repository.compileLocationGeometries(String(request.query.mapId ?? ""), request.body);
    log(`compiled location geometry artifacts MapId=${String(request.query.mapId ?? "")}`);
    response.json({ ok: true, outputs });
  });

  app.post("/api/maps/:mapId/publish", async (request, response) => {
    const packageDocument = await publisher.publish(request.params.mapId, request.body?.profile as PublishProfile);
    log(`published MapId=${packageDocument.mapId} revision=${packageDocument.revision} profile=${packageDocument.publishProfile}`);
    response.status(201).json({ ok: true, package: packageDocument });
  });

  app.use("/project-assets", express.static(repository.paths.textureRoot, { fallthrough: false, index: false }));

  app.use((error: unknown, request: Request, response: Response, _next: NextFunction) => {
    const status = error instanceof ZodError || error instanceof RangeError || (error instanceof SyntaxError && "body" in error) ? 400 : 500;
    const message = error instanceof Error ? error.message : "Unknown service error";
    if (!options.quiet) console.error(`[world-map-workbench] request rejected method=${request.method} path=${request.path} reason=${message}`);
    response.status(status).json({ ok: false, error: message, issues: error instanceof ZodError ? error.issues : undefined });
  });

  return app;
}
