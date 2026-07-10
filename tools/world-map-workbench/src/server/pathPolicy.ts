import path from "node:path";

export interface ProjectPathPolicy {
  readonly projectRoot: string;
  readonly configRoot: string;
  readonly textureRoot: string;
  resolveConfig(relativePath: string): string;
  resolveTexture(relativePath: string): string;
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
  return {
    projectRoot: normalizedProjectRoot,
    configRoot,
    textureRoot,
    resolveConfig: (relativePath) => resolveInside(configRoot, relativePath),
    resolveTexture: (relativePath) => resolveInside(textureRoot, relativePath),
  };
}
