import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { createApp } from "./app.js";

function findProjectRoot(start: string): string {
  let current = path.resolve(start);
  while (true) {
    if (fs.existsSync(path.join(current, "project.godot"))) return current;
    const parent = path.dirname(current);
    if (parent === current) throw new Error(`Unable to locate Godot project root from ${start}`);
    current = parent;
  }
}

const moduleDirectory = path.dirname(fileURLToPath(import.meta.url));
const projectRoot = process.env.WORLD_MAP_PROJECT_ROOT
  ? path.resolve(process.env.WORLD_MAP_PROJECT_ROOT)
  : findProjectRoot(process.cwd() === moduleDirectory ? moduleDirectory : process.cwd());
const port = Number(process.env.WORLD_MAP_WORKBENCH_PORT ?? 4174);
const app = createApp({ projectRoot });

const productionClient = path.resolve(moduleDirectory, "../../client");
if (fs.existsSync(productionClient)) {
  app.use(expressStatic(productionClient));
}

app.listen(port, "127.0.0.1", () => {
  console.log(`[world-map-workbench] listening=http://127.0.0.1:${port} projectRoot=${projectRoot}`);
});

function expressStatic(directory: string) {
  return async (request: import("express").Request, response: import("express").Response, next: import("express").NextFunction) => {
    if (request.path.startsWith("/api") || request.path.startsWith("/project-assets")) return next();
    const relative = request.path === "/" ? "index.html" : request.path.replace(/^\//, "");
    const resolved = path.resolve(directory, relative);
    if (!resolved.startsWith(`${directory}${path.sep}`) && resolved !== path.join(directory, "index.html")) return next();
    response.sendFile(resolved, (error) => {
      if (error) next();
    });
  };
}
