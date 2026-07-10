import express, { type NextFunction, type Request, type Response } from "express";
import { ZodError } from "zod";
import type { TerrainChunkPayload } from "../shared/types.js";
import { ProjectRepository } from "./projectRepository.js";

export interface AppOptions {
  projectRoot: string;
  quiet?: boolean;
}

export function createApp(options: AppOptions) {
  const app = express();
  const repository = new ProjectRepository(options.projectRoot);
  const log = (message: string) => {
    if (!options.quiet) console.log(`[world-map-workbench] ${message}`);
  };

  app.disable("x-powered-by");
  app.use(express.json({ limit: "64mb" }));

  app.get("/api/health", (_request, response) => {
    response.json({ ok: true, projectRoot: repository.paths.projectRoot });
  });

  app.get("/api/project", async (_request, response) => {
    response.json(await repository.loadBundle());
  });

  app.post("/api/project/bootstrap", async (_request, response) => {
    const bundle = await repository.bootstrap();
    log(`bootstrapped project root=${repository.paths.projectRoot}`);
    response.status(201).json(bundle);
  });

  app.put("/api/project", async (request, response) => {
    const project = await repository.saveProject(request.body);
    log(`saved project id=${project.projectId}`);
    response.json({ ok: true, project });
  });

  app.put("/api/geography", async (request, response) => {
    const geography = await repository.saveGeography(request.body);
    log(`saved geography lines=${geography.linearFeatures.features.length} locations=${geography.strategicLocations.features.length} regions=${geography.regions.features.length}`);
    response.json({ ok: true });
  });

  app.post("/api/terrain/masks", async (request, response) => {
    const payloads = request.body?.chunks as TerrainChunkPayload[] | undefined;
    if (!Array.isArray(payloads)) throw new Error("Terrain mask request requires a chunks array");
    const savedChunkIds = await repository.saveTerrainChunks(payloads);
    log(`saved terrain masks chunks=${savedChunkIds.join(",")}`);
    response.json({ ok: true, savedChunkIds });
  });

  app.post("/api/regions/compile", async (request, response) => {
    const outputs = await repository.compileRegions(request.body);
    log(`compiled region artifacts directory=${repository.paths.resolveTexture("masks/territory")}`);
    response.json({ ok: true, outputs });
  });

  app.use("/project-assets", express.static(repository.paths.textureRoot, { fallthrough: false, index: false }));

  app.use((error: unknown, request: Request, response: Response, _next: NextFunction) => {
    const status = error instanceof ZodError || (error instanceof SyntaxError && "body" in error) ? 400 : 500;
    const message = error instanceof Error ? error.message : "Unknown service error";
    if (!options.quiet) console.error(`[world-map-workbench] request rejected method=${request.method} path=${request.path} reason=${message}`);
    response.status(status).json({ ok: false, error: message, issues: error instanceof ZodError ? error.issues : undefined });
  });

  return app;
}
