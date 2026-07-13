import path from "node:path";

export interface ProjectPathPolicy {
  readonly projectRoot: string;
  readonly configRoot: string;
  readonly textureRoot: string;
  readonly mapSourceRoot: string;
  readonly publishedRoot: string;
  resolveConfig(relativePath: string): string;
  resolveTexture(relativePath: string): string;
  resolveMapSource(mapId: string, relativePath: string): string;
  resolvePublished(mapId: string, relativePath: string): string;
  resolveMapTexture(mapId: string, relativePath: string): string;
}

export function assertMapId(mapId: string): string {
  if (!/^[a-z0-9][a-z0-9_-]{1,63}$/.test(mapId)) throw new Error(`Invalid immutable MapId: ${mapId}`);
  return mapId;
}

function resolveInside(root: string, relativePath: string): string {
  // The browser never supplies an arbitrary filesystem root; all writes stay in one approved subtree.
  if (!relativePath || path.isAbsolute(relativePath)) {
    throw new Error(`Path must be relative to the configured project root: ${relativePath}`);
  }
  const resolved = path.resolve(root, relativePath);
  const normalizedRoot = path.resolve(root);
  if (resolved !== normalizedRoot && !resolved.startsWith(`${normalizedRoot}${path.sep}`)) {
    throw new Error(`Path resolves outside configured project root: ${relativePath}`);
  }
  return resolved;
}

export function createPathPolicy(projectRoot: string): ProjectPathPolicy {
  const normalizedProjectRoot = path.resolve(projectRoot);
  const configRoot = path.join(normalizedProjectRoot, "config", "world");
  const textureRoot = path.join(normalizedProjectRoot, "assets", "textures", "world");
  const mapSourceRoot = path.join(configRoot, "maps");
  const publishedRoot = path.join(configRoot, "published");
  return {
    projectRoot: normalizedProjectRoot,
    configRoot,
    textureRoot,
    mapSourceRoot,
    publishedRoot,
    resolveConfig: (relativePath) => resolveInside(configRoot, relativePath),
    resolveTexture: (relativePath) => resolveInside(textureRoot, relativePath),
    resolveMapSource: (mapId, relativePath) => resolveInside(path.join(mapSourceRoot, assertMapId(mapId), "source"), relativePath),
    resolvePublished: (mapId, relativePath) => resolveInside(path.join(publishedRoot, assertMapId(mapId)), relativePath),
    resolveMapTexture: (mapId, relativePath) => resolveInside(path.join(textureRoot, "maps", assertMapId(mapId)), relativePath),
  };
}
