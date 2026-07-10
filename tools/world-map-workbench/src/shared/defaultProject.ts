import type { GeographyDocument, LayerDefinition, WorldProject } from "./types.js";

const layers: LayerDefinition[] = [
  { id: "reference-map", label: "真实参考地图", kind: "reference", visible: true, locked: true, opacity: 0.55 },
  { id: "terrain", label: "地貌分类", kind: "canonical", visible: true, locked: false, opacity: 0.7 },
  { id: "water", label: "水系", kind: "canonical", visible: true, locked: false, opacity: 1 },
  { id: "mountains", label: "山脉与高地", kind: "canonical", visible: true, locked: false, opacity: 1 },
  { id: "roads", label: "道路", kind: "canonical", visible: true, locked: false, opacity: 1 },
  { id: "strategic-locations", label: "战略地点", kind: "canonical", visible: true, locked: false, opacity: 1 },
  { id: "territories", label: "城域与小区域", kind: "canonical", visible: true, locked: false, opacity: 0.8 },
  { id: "final-chunk-art", label: "正式 Chunk 美术", kind: "reference", visible: false, locked: true, opacity: 1 },
  { id: "region-masks", label: "区域 Mask", kind: "derived", visible: false, locked: true, opacity: 0.55 },
  { id: "validation", label: "校验信息", kind: "derived", visible: true, locked: true, opacity: 1 },
];

export function createDefaultProject(): WorldProject {
  const chunkWidth = 1024;
  const chunkHeight = 1024;
  const columns = 4;
  const rows = 2;
  const chunks = [];

  for (let y = 0; y < rows; y += 1) {
    for (let x = 0; x < columns; x += 1) {
      const id = `chunk_${x}_${y}`;
      chunks.push({
        id,
        coordinate: [x, y] as [number, number],
        worldOrigin: [x * chunkWidth, y * chunkHeight] as [number, number],
        terrainMaskPath: `masks/terrain/${id}.png`,
        territoryMaskPath: `masks/territory/${id}.png`,
        navigationScenePath: `res://scenes/world/navigation/${id}.tscn`,
      });
    }
  }

  return {
    version: 1,
    projectId: "strategic_world",
    displayName: "大世界地理工作台",
    world: { width: chunkWidth * columns, height: chunkHeight * rows },
    chunk: { width: chunkWidth, height: chunkHeight, terrainCellSize: 16, territoryMaskScale: 0.25 },
    layers: layers.map((layer) => ({ ...layer })),
    terrainTypes: [
      { id: 1, key: "grassland", label: "草原", color: "#88a85d" },
      { id: 2, key: "forest", label: "森林", color: "#3f6e4a" },
      { id: 3, key: "marsh", label: "沼泽", color: "#5d766d" },
      { id: 4, key: "wasteland", label: "荒地", color: "#9a8262" },
      { id: 5, key: "desert", label: "沙漠", color: "#d1b36d" },
      { id: 6, key: "snow", label: "雪地", color: "#dce8e8" },
    ],
    chunks,
  };
}

export function createEmptyGeography(): GeographyDocument {
  return {
    version: 1,
    linearFeatures: { type: "FeatureCollection", features: [] },
    waterAnchors: { type: "FeatureCollection", features: [] },
    strategicLocations: { type: "FeatureCollection", features: [] },
    regions: { type: "FeatureCollection", features: [] },
  };
}
